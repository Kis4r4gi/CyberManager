<#
.SYNOPSIS
    Asigna iconos .bmp a los productos de CyberManager emparejando por nombre.

.DESCRIPTION
    Lee una carpeta con imágenes .bmp (por ejemplo resources\BMP SNACKS) y los productos
    de la base de datos. Para cada imagen calcula la mejor coincidencia por nombre,
    ignorando mayúsculas, acentos y signos de puntuación.

    Por defecto solo genera un informe: no escribe nada en la base. Con -Apply guarda
    las coincidencias fuertes en la tabla ProductIcons y actualiza Products.IconUpdatedUtc.
    Las coincidencias dudosas se muestran como "Revisar" y no se aplican, salvo que se
    indique -ApplyFuzzy.

    Antes de escribir se crea un respaldo de la base de datos.

.PARAMETER Folder
    Carpeta con los archivos .bmp. Por defecto ..\resources\BMP SNACKS.

.PARAMETER Database
    Ruta del archivo cybermanager.db. Si se omite se busca en la instalación del servidor.

.PARAMETER ServerDir
    Carpeta del servidor instalado que contiene cybermanager.db y las DLL de SQLite.
    Por defecto C:\CyberManager\Servidor\Server.

.PARAMETER MinScore
    Puntaje mínimo (0 a 1) para considerar una imagen como candidata. Por defecto 0.70.

.PARAMETER ApplyScore
    Puntaje mínimo para aplicar una coincidencia automáticamente. Por defecto 0.86.

.PARAMETER Apply
    Escribe las coincidencias fuertes en la base de datos.

.PARAMETER ApplyFuzzy
    Aplica también las coincidencias dudosas (puntaje entre MinScore y ApplyScore).

.PARAMETER SkipBackup
    No crea el respaldo previo de la base de datos.

.PARAMETER Report
    Ruta del informe CSV. Por defecto scripts\iconos-emparejamiento.csv.

.EXAMPLE
    .\import-product-icons.ps1

.EXAMPLE
    .\import-product-icons.ps1 -Apply

.EXAMPLE
    .\import-product-icons.ps1 -Apply -ApplyFuzzy -Report C:\Temp\iconos.csv
#>
[CmdletBinding()]
param(
    [string]$Folder,
    [string]$Database,
    [string]$ServerDir = "C:\CyberManager\Servidor\Server",

    [ValidateRange(0, 1)]
    [double]$MinScore = 0.70,

    [ValidateRange(0, 1)]
    [double]$ApplyScore = 0.86,

    [switch]$Apply,
    [switch]$ApplyFuzzy,
    [switch]$SkipBackup,

    [string]$Mapping,
    [switch]$NoMapping,

    [string]$Report
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

# $PSScriptRoot no siempre está disponible al evaluar los valores por defecto de param().
$scriptDir = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($scriptDir)) { $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path }
if ([string]::IsNullOrWhiteSpace($Folder)) { $Folder = Join-Path $scriptDir "..\resources\BMP SNACKS" }
if ([string]::IsNullOrWhiteSpace($Report)) { $Report = Join-Path $scriptDir "iconos-emparejamiento.csv" }
if ([string]::IsNullOrWhiteSpace($Mapping)) { $Mapping = Join-Path $scriptDir "iconos-mapeo.csv" }

function Write-Step([string]$Message) {
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Write-Ok([string]$Message) {
    Write-Host "  $Message" -ForegroundColor Green
}

function Write-Warn([string]$Message) {
    Write-Host "  AVISO: $Message" -ForegroundColor Yellow
}

# ---------------------------------------------------------------------------
# Normalización y emparejamiento
# ---------------------------------------------------------------------------

function ConvertTo-Key([string]$Text) {
    if ($null -eq $Text) { return "" }
    $text = $Text.Normalize([System.Text.NormalizationForm]::FormD)
    $text = $text -replace '\p{Mn}', ''
    $text = $text.ToLowerInvariant()
    $text = $text -replace '[^a-z0-9]+', ' '
    return ($text -replace '\s+', ' ').Trim()
}

# Correcciones para nombres que el sistema anterior escribía de forma distinta.
$aliases = @{
    "cheesetris"        = "chestris"
    "cheesetris gr"     = "chestris 150gr"
    "boligarfo"         = "boligrafo"
    "chilces adans"     = "chicles adans"
    "chocotale surtidos" = "chocolates surtidos"
    "crake as"          = "crakeas"
    "freegellspeq"      = "freegells peq"
    "jugoyukeybotella"  = "jugo yukey botella"
}

$stopwords = @(
    "de", "del", "la", "el", "los", "las", "con", "sin", "y", "o",
    "para", "por", "un", "una", "tipo"
)

function Get-Tokens([string]$Key) {
    return @($Key -split ' ' | Where-Object { $_.Length -ge 3 -and $stopwords -notcontains $_ })
}

function Get-Levenshtein([string]$A, [string]$B) {
    if ($A -eq $B) { return 0 }
    if ($A.Length -eq 0) { return $B.Length }
    if ($B.Length -eq 0) { return $A.Length }

    $previous = New-Object int[] ($B.Length + 1)
    $current = New-Object int[] ($B.Length + 1)
    for ($j = 0; $j -le $B.Length; $j++) { $previous[$j] = $j }

    for ($i = 1; $i -le $A.Length; $i++) {
        $current[0] = $i
        for ($j = 1; $j -le $B.Length; $j++) {
            $cost = if ($A[$i - 1] -eq $B[$j - 1]) { 0 } else { 1 }
            $deletion = $previous[$j] + 1
            $insertion = $current[$j - 1] + 1
            $substitution = $previous[$j - 1] + $cost
            $current[$j] = [Math]::Min([Math]::Min($deletion, $insertion), $substitution)
        }
        $swap = $previous
        $previous = $current
        $current = $swap
    }

    return $previous[$B.Length]
}

function Get-Similarity([string]$A, [string]$B) {
    if ($A -eq $B) { return 1.0 }
    if ([string]::IsNullOrEmpty($A) -or [string]::IsNullOrEmpty($B)) { return 0.0 }
    $max = [Math]::Max($A.Length, $B.Length)
    return 1.0 - ([double](Get-Levenshtein $A $B) / $max)
}

function Get-BmpSize([byte[]]$Data) {
    if ($Data.Length -lt 26) { return @{ Width = 0; Height = 0 } }
    return @{
        Width  = [BitConverter]::ToInt32($Data, 18)
        Height = [Math]::Abs([BitConverter]::ToInt32($Data, 22))
    }
}

# ---------------------------------------------------------------------------
# Base de datos (mismas rutas que import-stock.ps1)
# ---------------------------------------------------------------------------

function Find-SqliteDirectory([string]$Preferred) {
    $candidates = @(
        $Preferred,
        (Join-Path $PSScriptRoot "..\src\CyberManager.Server\bin\Release\net8.0"),
        (Join-Path $PSScriptRoot "..\src\CyberManager.Server\bin\Debug\net8.0")
    )

    foreach ($candidate in $candidates) {
        if ([string]::IsNullOrWhiteSpace($candidate)) { continue }
        if (Test-Path (Join-Path $candidate "Microsoft.Data.Sqlite.dll")) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    $found = Get-ChildItem -Path (Join-Path $PSScriptRoot "..\src\CyberManager.Server\bin") `
        -Recurse -Filter "Microsoft.Data.Sqlite.dll" -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($found) { return $found.Directory.FullName }

    throw "No se encontró Microsoft.Data.Sqlite.dll. Indique -ServerDir con la carpeta del servidor."
}

function Find-DatabaseFile([string]$Explicit, [string]$ServerDir) {
    $candidates = @(
        $Explicit,
        (Join-Path $ServerDir "cybermanager.db"),
        (Join-Path $PSScriptRoot "..\src\CyberManager.Server\cybermanager.db"),
        (Join-Path $PSScriptRoot "..\src\CyberManager.Server\bin\Release\net8.0\cybermanager.db"),
        (Join-Path $PSScriptRoot "..\src\CyberManager.Server\bin\Debug\net8.0\cybermanager.db")
    )

    foreach ($candidate in $candidates) {
        if ([string]::IsNullOrWhiteSpace($candidate)) { continue }
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    throw "No se encontró cybermanager.db. Indique -Database con la ruta del archivo."
}

function New-SqliteCommand($Connection, [string]$Sql) {
    $command = $Connection.CreateCommand()
    $command.CommandText = $Sql
    return $command
}

function Test-Column($Connection, [string]$Table, [string]$Column) {
    $command = New-SqliteCommand $Connection "PRAGMA table_info(""$Table"");"
    $reader = $command.ExecuteReader()
    try {
        while ($reader.Read()) {
            if ([string]::Equals($reader.GetString(1), $Column, [StringComparison]::OrdinalIgnoreCase)) {
                return $true
            }
        }
        return $false
    }
    finally { $reader.Close() }
}

# ---------------------------------------------------------------------------
# Ejecución
# ---------------------------------------------------------------------------

$folderPath = (Resolve-Path -LiteralPath $Folder).Path
$files = @(Get-ChildItem -LiteralPath $folderPath -File -Filter "*.bmp" | Sort-Object Name)
if ($files.Count -eq 0) { throw "No se encontraron archivos .bmp en $folderPath." }

$databasePath = Find-DatabaseFile $Database $ServerDir
$sqliteDir = Find-SqliteDirectory $ServerDir

foreach ($dll in @("SQLitePCLRaw.core.dll", "SQLitePCLRaw.provider.e_sqlite3.dll", "SQLitePCLRaw.batteries_v2.dll", "Microsoft.Data.Sqlite.dll")) {
    $dllPath = Join-Path $sqliteDir $dll
    if (-not (Test-Path $dllPath)) { throw "Falta $dll en $sqliteDir." }
    Add-Type -Path $dllPath
}
[SQLitePCL.Batteries_V2]::Init()

Write-Step "Imágenes: $folderPath ($($files.Count))"
Write-Step "Base de datos: $databasePath"

$mode = if ($Apply) { "ReadWrite" } else { "ReadOnly" }
$connection = New-Object Microsoft.Data.Sqlite.SqliteConnection "Data Source=$databasePath;Mode=$mode;Default Timeout=30"
$connection.Open()

try {
    $command = New-SqliteCommand $connection "PRAGMA busy_timeout=30000"
    [void]$command.ExecuteNonQuery()

    # ---- productos ----
    $products = New-Object System.Collections.Generic.List[object]
    $command = New-SqliteCommand $connection "SELECT Id, Sku, Name FROM Products ORDER BY Name"
    $reader = $command.ExecuteReader()
    try {
        while ($reader.Read()) {
            $name = $reader.GetString(2)
            $key = ConvertTo-Key $name
            $tokens = @(Get-Tokens $key)
            $products.Add([pscustomobject]@{
                Id     = $reader.GetInt32(0)
                Sku    = $reader.GetString(1)
                Name   = $name
                Key    = $key
                Tokens = $tokens
            })
        }
    }
    finally { $reader.Close() }

    Write-Ok "Productos leídos: $($products.Count)"

    # ---- mapeo verificado (BMP -> SKU) ----
    # El archivo permite corregir a mano los casos en que el nombre del BMP no coincide
    # exactamente con la descripción del producto. Tiene prioridad sobre el puntaje.
    $forcedByKey = @{}
    if (-not $NoMapping -and (Test-Path -LiteralPath $Mapping -PathType Leaf)) {
        $bySku = @{}
        foreach ($product in $products) { $bySku[$product.Sku] = $product }

        $rows = @(Import-Csv -LiteralPath $Mapping -Encoding UTF8)
        foreach ($row in $rows) {
            $bmpName = [string]$row.Bmp
            $sku = [string]$row.Sku
            if ([string]::IsNullOrWhiteSpace($bmpName) -or [string]::IsNullOrWhiteSpace($sku)) { continue }

            $bmpKey = ConvertTo-Key $bmpName
            if (-not $bySku.ContainsKey($sku)) {
                Write-Warn "El mapeo '$bmpName' apunta al SKU '$sku', que no existe."
                continue
            }

            $forcedByKey[$bmpKey] = $bySku[$sku]
        }

        Write-Ok "Mapeo verificado: $($forcedByKey.Count) imágenes"
    }

    # Índice por prefijo de 3 letras para no comparar todas las parejas.
    $prefixIndex = @{}
    foreach ($product in $products) {
        foreach ($token in $product.Tokens) {
            $prefix = $token.Substring(0, [Math]::Min(3, $token.Length))
            if (-not $prefixIndex.ContainsKey($prefix)) {
                $prefixIndex[$prefix] = New-Object System.Collections.Generic.List[object]
            }
            $prefixIndex[$prefix].Add($product)
        }
    }

    # ---- puntajes ----
    $pairs = New-Object System.Collections.Generic.List[object]
    $bmpInfo = @{}

    foreach ($file in $files) {
        $baseName = [System.IO.Path]::GetFileNameWithoutExtension($file.Name)
        $key = ConvertTo-Key $baseName
        if ($aliases.ContainsKey($key)) { $key = $aliases[$key] }

        $tokens = @(Get-Tokens $key)
        $bmpInfo[$file.Name] = [pscustomobject]@{
            File   = $file
            Base   = $baseName
            Key    = $key
            Tokens = $tokens
        }

        # Mapeo verificado: se aplica sin discusión y no participa del puntaje.
        if ($forcedByKey.ContainsKey((ConvertTo-Key $baseName))) {
            $product = $forcedByKey[(ConvertTo-Key $baseName)]
            $pairs.Add([pscustomobject]@{
                Bmp       = $file.Name
                Base      = $baseName
                ProductId = $product.Id
                Product   = $product.Name
                Sku       = $product.Sku
                Score     = 1.0
                Similar   = 1.0
                Coverage  = 1.0
                Forced    = $true
            })
            continue
        }

        $candidates = @{}
        foreach ($token in $tokens) {
            $prefix = $token.Substring(0, [Math]::Min(3, $token.Length))
            if ($prefixIndex.ContainsKey($prefix)) {
                foreach ($product in $prefixIndex[$prefix]) { $candidates[$product.Id] = $product }
            }
        }

        foreach ($product in $candidates.Values) {
            $similarity = Get-Similarity $key $product.Key
            $shared = 0
            foreach ($token in $tokens) {
                if ($product.Tokens -contains $token) { $shared++ }
            }
            $coverage = if ($tokens.Count -gt 0) { $shared / $tokens.Count } else { 0.0 }

            $score = [Math]::Max($similarity, $coverage)
            if ($coverage -ge 1.0) { $score = [Math]::Min(1.0, $score + 0.05) }

            # Si ambos tienen números de tamaño distintos, baja la confianza.
            $bmpNumbers = @($tokens | Where-Object { $_ -match '^\d+$' })
            $productNumbers = @($product.Tokens | Where-Object { $_ -match '^\d+$' })
            if ($bmpNumbers.Count -gt 0 -and $productNumbers.Count -gt 0) {
                $common = @($bmpNumbers | Where-Object { $productNumbers -contains $_ })
                if ($common.Count -eq 0) { $score -= 0.20 }
            }

            if ($score -ge $MinScore) {
                $pairs.Add([pscustomobject]@{
                    Bmp       = $file.Name
                    Base      = $baseName
                    ProductId = $product.Id
                    Product   = $product.Name
                    Sku       = $product.Sku
                    Score     = [Math]::Round($score, 4)
                    Similar   = [Math]::Round($similarity, 4)
                    Coverage  = [Math]::Round($coverage, 4)
                    Forced    = $false
                })
            }
        }
    }

    # ---- asignación: cada imagen y cada producto se usan una sola vez ----
    # Primero el mapeo verificado; después las coincidencias automáticas por puntaje.
    $assignedBmp = @{}
    $assignedProduct = @{}
    $matches = New-Object System.Collections.Generic.List[object]

    # Si dos productos empatan para la misma imagen, no se aplica: queda para revisión.
    $scoresByBmp = @{}
    foreach ($pair in $pairs) {
        if (-not $scoresByBmp.ContainsKey($pair.Bmp)) {
            $scoresByBmp[$pair.Bmp] = New-Object System.Collections.Generic.List[double]
        }
        $scoresByBmp[$pair.Bmp].Add([double]$pair.Score)
    }

    $ambiguous = @{}
    foreach ($entry in $scoresByBmp.GetEnumerator()) {
        if ($entry.Value.Count -lt 2) { continue }
        $sorted = @($entry.Value | Sort-Object -Descending)
        if (($sorted[0] - $sorted[1]) -lt 0.02) { $ambiguous[$entry.Key] = $true }
    }

    $ordered = @(
        @($pairs | Where-Object { $_.Forced }) +
        @($pairs | Where-Object { -not $_.Forced } | Sort-Object -Property Score -Descending)
    )

    foreach ($pair in $ordered) {
        if ($assignedBmp.ContainsKey($pair.Bmp)) { continue }
        if ($assignedProduct.ContainsKey($pair.ProductId)) { continue }

        $assignedBmp[$pair.Bmp] = $true
        $assignedProduct[$pair.ProductId] = $true

        $action = if ($pair.Forced) { "Aplicar" }
                  elseif ($ambiguous.ContainsKey($pair.Bmp)) { "Revisar" }
                  elseif ($pair.Score -ge $ApplyScore) { "Aplicar" }
                  elseif ($ApplyFuzzy) { "Aplicar" }
                  else { "Revisar" }

        $matches.Add([pscustomobject]@{
            Bmp       = $pair.Bmp
            ProductId = $pair.ProductId
            Producto  = $pair.Product
            Sku       = $pair.Sku
            Puntaje   = $pair.Score
            Similitud = $pair.Similar
            Cobertura = $pair.Coverage
            Origen    = if ($pair.Forced) { "Mapeo" }
                        elseif ($ambiguous.ContainsKey($pair.Bmp)) { "Empate" }
                        else { "Automático" }
            Accion    = $action
        })
    }

    $unmatched = @($files | Where-Object { -not $assignedBmp.ContainsKey($_.Name) } | ForEach-Object { $_.Name })
    $applyList = @($matches | Where-Object { $_.Accion -eq "Aplicar" })
    $reviewList = @($matches | Where-Object { $_.Accion -eq "Revisar" })

    Write-Host ""
    Write-Step "Resultado del emparejamiento"
    Write-Host ("  Coincidencias fuertes (se aplican): {0}" -f $applyList.Count) -ForegroundColor Green
    Write-Host ("  Coincidencias dudosas (revisar):    {0}" -f $reviewList.Count) -ForegroundColor Yellow
    Write-Host ("  Imágenes sin coincidencia:           {0}" -f $unmatched.Count) -ForegroundColor DarkGray

    if ($matches.Count -gt 0) {
        Write-Host ""
        $matches | Select-Object Bmp, Producto, Sku, Puntaje, Origen, Accion |
            Format-Table -AutoSize | Out-String -Width 200 | Write-Host
    }

    if ($reviewList.Count -gt 0) {
        Write-Host "Coincidencias dudosas (no se aplican salvo -ApplyFuzzy):" -ForegroundColor Yellow
        $reviewList | Select-Object Bmp, Producto, Sku, Puntaje |
            Format-Table -AutoSize | Out-String -Width 200 | Write-Host
    }

    if ($unmatched.Count -gt 0) {
        Write-Host "Imágenes sin coincidencia:" -ForegroundColor DarkGray
        $unmatched | ForEach-Object { Write-Host "  $_" -ForegroundColor DarkGray }
    }

    # ---- informe CSV ----
    if (-not [string]::IsNullOrWhiteSpace($Report)) {
        $reportDir = Split-Path -Parent $Report
        if (-not [string]::IsNullOrWhiteSpace($reportDir)) {
            New-Item -ItemType Directory -Force -Path $reportDir | Out-Null
        }

        $rows = New-Object System.Collections.Generic.List[object]
        foreach ($match in $matches) { $rows.Add($match) }
        foreach ($name in $unmatched) {
            $rows.Add([pscustomobject]@{
                Bmp = $name; Producto = ""; Sku = ""; Puntaje = 0;
                Similitud = 0; Cobertura = 0; Origen = ""; Accion = "Sin coincidencia"
            })
        }

        $rows | Export-Csv -LiteralPath $Report -NoTypeInformation -Encoding UTF8
        Write-Ok "Informe guardado en $Report"
    }

    if (-not $Apply) {
        Write-Host ""
        Write-Host "Simulación completada: no se escribió nada en la base." -ForegroundColor Yellow
        Write-Host "Ejecute de nuevo con -Apply para guardar las coincidencias fuertes." -ForegroundColor Yellow
        return
    }

    if ($applyList.Count -eq 0) {
        Write-Warn "No hay coincidencias para aplicar."
        return
    }

    # ---- respaldo ----
    if (-not $SkipBackup) {
        $backupDir = Join-Path (Split-Path -Parent $databasePath) "respaldos"
        New-Item -ItemType Directory -Force -Path $backupDir | Out-Null
        $backupPath = Join-Path $backupDir ("cybermanager_{0}_pre-iconos.db" -f (Get-Date -Format "yyyyMMdd_HHmmss"))

        Write-Step "Creando respaldo en $backupPath"
        $backup = New-Object Microsoft.Data.Sqlite.SqliteConnection "Data Source=$backupPath"
        $backup.Open()
        try { $connection.BackupDatabase($backup) }
        finally { $backup.Close() }
        Write-Ok "Respaldo creado."
    }

    # ---- esquema mínimo (por si el servidor aún no se ha actualizado) ----
    $command = New-SqliteCommand $connection @"
CREATE TABLE IF NOT EXISTS "ProductIcons" (
    "ProductId" INTEGER NOT NULL CONSTRAINT "PK_ProductIcons" PRIMARY KEY,
    "ContentType" TEXT NOT NULL,
    "Extension" TEXT NOT NULL,
    "Data" BLOB NOT NULL,
    "Width" INTEGER NOT NULL,
    "Height" INTEGER NOT NULL,
    "SizeBytes" INTEGER NOT NULL,
    "Sha256" TEXT NOT NULL,
    "UpdatedUtc" TEXT NOT NULL,
    CONSTRAINT "FK_ProductIcons_Products_ProductId"
        FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE CASCADE
);
"@
    [void]$command.ExecuteNonQuery()

    if (-not (Test-Column $connection "Products" "IconUpdatedUtc")) {
        $command = New-SqliteCommand $connection 'ALTER TABLE "Products" ADD COLUMN "IconUpdatedUtc" TEXT NULL;'
        [void]$command.ExecuteNonQuery()
    }

    # ---- escritura ----
    $written = 0
    $transaction = $connection.BeginTransaction()
    try {
        foreach ($match in $applyList) {
            $info = $bmpInfo[$match.Bmp]
            $data = [System.IO.File]::ReadAllBytes($info.File.FullName)
            $size = Get-BmpSize $data
            $updated = [DateTime]::UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fffffff",
                [System.Globalization.CultureInfo]::InvariantCulture)

            $sha = [System.Security.Cryptography.SHA256]::Create()
            try { $hash = [BitConverter]::ToString($sha.ComputeHash($data)).Replace("-", "").ToLowerInvariant() }
            finally { $sha.Dispose() }

            $command = New-SqliteCommand $connection @"
INSERT OR REPLACE INTO "ProductIcons"
    ("ProductId", "ContentType", "Extension", "Data", "Width", "Height", "SizeBytes", "Sha256", "UpdatedUtc")
VALUES
    (@productId, 'image/bmp', '.bmp', @data, @width, @height, @sizeBytes, @sha, @updated);
"@
            $command.Transaction = $transaction
            [void]$command.Parameters.AddWithValue("@productId", [int]$match.ProductId)
            [void]$command.Parameters.AddWithValue("@data", $data)
            [void]$command.Parameters.AddWithValue("@width", [int]$size.Width)
            [void]$command.Parameters.AddWithValue("@height", [int]$size.Height)
            [void]$command.Parameters.AddWithValue("@sizeBytes", [long]$data.Length)
            [void]$command.Parameters.AddWithValue("@sha", $hash)
            [void]$command.Parameters.AddWithValue("@updated", $updated)
            [void]$command.ExecuteNonQuery()

            $command = New-SqliteCommand $connection "UPDATE Products SET IconUpdatedUtc = @updated WHERE Id = @id"
            $command.Transaction = $transaction
            [void]$command.Parameters.AddWithValue("@updated", $updated)
            [void]$command.Parameters.AddWithValue("@id", [int]$match.ProductId)
            [void]$command.ExecuteNonQuery()

            $written++
        }

        $transaction.Commit()
    }
    catch {
        $transaction.Rollback()
        throw
    }
    finally {
        $transaction.Dispose()
    }

    Write-Host ""
    Write-Host "Importación de iconos completada." -ForegroundColor Green
    Write-Host ("  Imágenes guardadas: {0}" -f $written)
}
finally {
    $connection.Close()
}
