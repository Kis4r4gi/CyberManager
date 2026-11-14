<#
.SYNOPSIS
    Importa productos desde un archivo de stock (CSV o HTML) a la base de datos de CyberManager.

.DESCRIPTION
    Lee una lista con las columnas Código, Descripción, Precio, %IVA, Stock y Mínimo
    (el formato que exporta el sistema anterior) y crea o actualiza los productos de
    CyberManager usando el código como SKU.

    - Los productos nuevos se crean activos y sin categoría.
    - Si el SKU ya existe se actualizan nombre, precio, stock, mínimo, IVA y tipo.
    - El %IVA del archivo se ignora: se aplica el valor de -TaxRate (16 % por defecto).
    - Antes de escribir se guarda un respaldo de la base en la carpeta respaldos.

    El archivo puede ser CSV separado por comas o una tabla HTML (HTM/HTML). Se aceptan
    descripciones con comas: las columnas extra se vuelven a unir a la descripción.

.PARAMETER File
    Archivo CSV, HTM o HTML exportado con la lista de productos.

.PARAMETER Database
    Ruta del archivo cybermanager.db. Si se omite se busca en la instalación del servidor.

.PARAMETER ServerDir
    Carpeta del servidor instalado que contiene cybermanager.db y las DLL de SQLite.
    Por defecto C:\CyberManager\Servidor\Server.

.PARAMETER TaxRate
    IVA (%) que se aplica a todos los productos importados. Por defecto 16.

.PARAMETER ServiceMode
    Auto (por defecto): marca como servicio los productos sin stock, sin mínimo y con
    precio mayor que cero. None: todos controlan inventario. All: todos son servicios.

.PARAMETER DeleteDemoProducts
    Elimina los 16 productos de ejemplo del sembrado inicial (BEB-001, SNK-001, etc.)
    siempre que no tengan ventas, compras ni movimientos asociados.

.PARAMETER SkipBackup
    No crea el respaldo previo de la base de datos.

.PARAMETER DryRun
    Muestra lo que haría sin escribir nada en la base de datos.

.EXAMPLE
    .\import-stock.ps1 -File ..\resources\stock.csv -DryRun

.EXAMPLE
    .\import-stock.ps1 -File ..\resources\stock.csv -DeleteDemoProducts

.EXAMPLE
    .\import-stock.ps1 -File ..\resources\stock.htm -TaxRate 0 -ServiceMode None
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$File,

    [string]$Database,
    [string]$ServerDir = "C:\CyberManager\Servidor\Server",

    [ValidateRange(0, 100)]
    [decimal]$TaxRate = 16,

    [ValidateSet("Auto", "None", "All")]
    [string]$ServiceMode = "Auto",

    [switch]$DeleteDemoProducts,
    [switch]$SkipBackup,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

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
# Lectura y normalización del archivo
# ---------------------------------------------------------------------------

function Read-TextFile([string]$Path) {
    $bytes = [System.IO.File]::ReadAllBytes($Path)

    # Quita el BOM UTF-8 si existe.
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
        $bytes = $bytes[3..($bytes.Length - 1)]
    }

    try {
        # El archivo del sistema anterior suele estar en Windows-1252: si no es
        # UTF-8 válido se decodifica como 1252 para conservar los acentos.
        $utf8 = New-Object System.Text.UTF8Encoding($false, $true)
        return $utf8.GetString($bytes)
    }
    catch {
        try { return [System.Text.Encoding]::GetEncoding(1252).GetString($bytes) }
        catch { return [System.Text.Encoding]::Latin1.GetString($bytes) }
    }
}

function ConvertFrom-CsvLine([string]$Line) {
    $fields = New-Object System.Collections.Generic.List[string]
    $buffer = New-Object System.Text.StringBuilder
    $inQuotes = $false
    $fieldStart = $true

    for ($i = 0; $i -lt $Line.Length; $i++) {
        $ch = $Line[$i]
        if ($inQuotes) {
            if ($ch -eq '"') {
                if ($i + 1 -lt $Line.Length -and $Line[$i + 1] -eq '"') {
                    [void]$buffer.Append('"')
                    $i++
                }
                else {
                    $inQuotes = $false
                }
            }
            else {
                [void]$buffer.Append($ch)
            }
        }
        elseif ($ch -eq '"' -and $fieldStart) {
            # Solo se abre una comilla al inicio del campo. Las comillas sueltas
            # dentro de una descripción (por ejemplo 1") se copian tal cual.
            $inQuotes = $true
            $fieldStart = $false
        }
        elseif ($ch -eq ',') {
            $fields.Add($buffer.ToString())
            [void]$buffer.Clear()
            $fieldStart = $true
        }
        else {
            [void]$buffer.Append($ch)
            $fieldStart = $false
        }
    }

    $fields.Add($buffer.ToString())
    return ,$fields.ToArray()
}

function ConvertFrom-HtmlTable([string]$Html) {
    $records = New-Object System.Collections.Generic.List[object]

    foreach ($row in [regex]::Matches($Html, '<tr[^>]*>(.*?)</tr>', 'Singleline,IgnoreCase')) {
        $cells = New-Object System.Collections.Generic.List[string]
        foreach ($cell in [regex]::Matches($row.Groups[1].Value, '<td[^>]*>(.*?)</td>', 'Singleline,IgnoreCase')) {
            $value = $cell.Groups[1].Value -replace '<[^>]+>', ''
            $cells.Add([System.Net.WebUtility]::HtmlDecode($value))
        }
        if ($cells.Count -gt 0) { $records.Add($cells.ToArray()) }
    }

    return ,$records.ToArray()
}

function ConvertTo-CleanText([string]$Value) {
    if ($null -eq $Value) { return "" }
    $Value = $Value -replace '[\u00A0\u2007\u202F]', ' '
    $Value = $Value -replace '\s+', ' '
    return $Value.Trim()
}

function ConvertTo-Number([string]$Value, [string]$Field, [string]$Code) {
    $text = (ConvertTo-CleanText $Value) -replace '\s', ''
    if ($text -eq "") { return [decimal]0 }

    $number = [decimal]0
    $style = [System.Globalization.NumberStyles]::Number
    $invariant = [System.Globalization.CultureInfo]::InvariantCulture

    if ([decimal]::TryParse($text, $style, $invariant, [ref]$number)) { return $number }

    # Algunos exports usan coma decimal (1,15).
    $spanish = [System.Globalization.CultureInfo]::GetCultureInfo('es-VE')
    if ([decimal]::TryParse($text, $style, $spanish, [ref]$number)) { return $number }

    throw "El campo $Field no es un número válido ('$Value') en el código '$Code'."
}

function ConvertTo-ProductRows($Records) {
    $products = New-Object System.Collections.Generic.List[object]
    $seen = @{}
    $line = 0

    foreach ($record in $Records) {
        $line++
        $fields = @($record)

        if ((@($fields | Where-Object { (ConvertTo-CleanText $_) -ne "" })).Count -eq 0) { continue }

        $code = ConvertTo-CleanText $fields[0]

        # Fila de título (",Stock - Fecha ...") y cabecera ("Código").
        if ($code -eq "" -or $code -match '^(c[oó]digo|sku)$') { continue }

        if ($fields.Count -lt 6) {
            Write-Warn "Línea $line ignorada: se esperaban al menos 6 columnas y hay $($fields.Count) ('$code')."
            continue
        }

        # Si la descripción tenía comas, el export la parte en varias columnas.
        # Se vuelven a unir y los últimos 4 valores son Precio, %IVA, Stock y Mínimo.
        $nameParts = @($fields[1..($fields.Count - 5)] | ForEach-Object { ConvertTo-CleanText $_ })
        $name = $nameParts[0]
        for ($i = 1; $i -lt $nameParts.Count; $i++) {
            $extra = $nameParts[$i]
            if ($extra -match '^\d+$') { $name += ",$extra" }
            else { $name += ", $extra" }
        }
        $name = ConvertTo-CleanText $name

        if ($name -eq "") {
            Write-Warn "Línea $line ignorada: el código '$code' no tiene descripción."
            continue
        }

        $values = @($fields[($fields.Count - 4)..($fields.Count - 1)])

        try {
            $price = ConvertTo-Number $values[0] "Precio" $code
            $sourceIva = ConvertTo-Number $values[1] "%IVA" $code
            $stock = ConvertTo-Number $values[2] "Stock" $code
            $minStock = ConvertTo-Number $values[3] "Mínimo" $code
        }
        catch {
            Write-Warn "Línea $line ignorada: $($_.Exception.Message)"
            continue
        }

        if ($price -lt 0 -or $stock -lt 0 -or $minStock -lt 0) {
            Write-Warn "Línea $line ignorada: precio, stock o mínimo negativo ('$code')."
            continue
        }

        $key = $code.ToLowerInvariant()
        if ($seen.ContainsKey($key)) {
            Write-Warn "Línea $line ignorada: el código '$code' ya apareció antes."
            continue
        }
        $seen[$key] = $true

        $isService = switch ($ServiceMode) {
            "All" { $true }
            "None" { $false }
            default { $stock -eq 0 -and $minStock -eq 0 -and $price -gt 0 }
        }

        $products.Add([pscustomobject]@{
            Code      = $code
            Name      = $name
            Price     = $price
            SourceIva = $sourceIva
            Stock     = $stock
            MinStock  = $minStock
            IsService = [bool]$isService
        })
    }

    return ,$products.ToArray()
}

# ---------------------------------------------------------------------------
# Base de datos
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

function Get-ReferenceCount($Connection, [int]$ProductId) {
    $command = New-SqliteCommand $Connection @"
SELECT (SELECT COUNT(*) FROM SaleItems WHERE ProductId = @id)
     + (SELECT COUNT(*) FROM PurchaseItems WHERE ProductId = @id)
     + (SELECT COUNT(*) FROM StockMovements WHERE ProductId = @id)
"@
    [void]$command.Parameters.AddWithValue("@id", $ProductId)
    return [int]$command.ExecuteScalar()
}

# ---------------------------------------------------------------------------
# Ejecución
# ---------------------------------------------------------------------------

$sourcePath = (Resolve-Path -LiteralPath $File).Path
$databasePath = Find-DatabaseFile $Database $ServerDir
$sqliteDir = Find-SqliteDirectory $ServerDir

Write-Step "Leyendo $sourcePath"
$text = Read-TextFile $sourcePath

if ($sourcePath -match '\.(htm|html)$') {
    $records = ConvertFrom-HtmlTable $text
}
else {
    $recordList = New-Object System.Collections.Generic.List[object]
    foreach ($line in ($text -split "\r?\n")) {
        if ($line.Trim() -eq "") { continue }
        $recordList.Add((ConvertFrom-CsvLine $line))
    }
    $records = $recordList.ToArray()
}

$products = ConvertTo-ProductRows $records
if ($products.Count -eq 0) { throw "El archivo no contiene productos válidos." }

Write-Ok "Productos leídos: $($products.Count)"
Write-Ok "Servicios detectados: $(($products | Where-Object IsService).Count)"

foreach ($dll in @("SQLitePCLRaw.core.dll", "SQLitePCLRaw.provider.e_sqlite3.dll", "SQLitePCLRaw.batteries_v2.dll", "Microsoft.Data.Sqlite.dll")) {
    $dllPath = Join-Path $sqliteDir $dll
    if (-not (Test-Path $dllPath)) { throw "Falta $dll en $sqliteDir." }
    Add-Type -Path $dllPath
}
[SQLitePCL.Batteries_V2]::Init()

Write-Step "Base de datos: $databasePath"

$mode = if ($DryRun) { "ReadOnly" } else { "ReadWrite" }
$connection = New-Object Microsoft.Data.Sqlite.SqliteConnection "Data Source=$databasePath;Mode=$mode;Default Timeout=30"
$connection.Open()

try {
    $command = New-SqliteCommand $connection "PRAGMA busy_timeout=30000"
    [void]$command.ExecuteNonQuery()

    # Productos existentes por SKU (sin distinguir mayúsculas para no duplicar).
    $existing = @{}
    $command = New-SqliteCommand $connection "SELECT Id, Sku FROM Products"
    $reader = $command.ExecuteReader()
    try {
        while ($reader.Read()) {
            $existing[$reader.GetString(1)] = [pscustomobject]@{
                Id  = $reader.GetInt32(0)
                Sku = $reader.GetString(1)
            }
        }
    }
    finally { $reader.Close() }

    $toInsert = New-Object System.Collections.Generic.List[object]
    $toUpdate = New-Object System.Collections.Generic.List[object]

    foreach ($product in $products) {
        if ($existing.ContainsKey($product.Code)) {
            $toUpdate.Add([pscustomobject]@{ Product = $product; Id = $existing[$product.Code].Id })
        }
        else {
            $toInsert.Add($product)
        }
    }

    Write-Step "Nuevos: $($toInsert.Count) | Actualizados: $($toUpdate.Count)"

    if ($DryRun) {
        Write-Host ""
        Write-Host "  Primeros productos que se importarían:" -ForegroundColor DarkGray
        $products | Select-Object -First 8 Code, Name, Price, Stock, MinStock, IsService |
            Format-Table -AutoSize | Out-String -Width 160 | Write-Host
        Write-Host "Simulación completada: no se escribió nada en la base." -ForegroundColor Yellow
        return
    }

    if (-not $SkipBackup) {
        $backupDir = Join-Path (Split-Path -Parent $databasePath) "respaldos"
        New-Item -ItemType Directory -Force -Path $backupDir | Out-Null
        $backupPath = Join-Path $backupDir ("cybermanager_{0}_pre-importacion.db" -f (Get-Date -Format "yyyyMMdd_HHmmss"))

        Write-Step "Creando respaldo en $backupPath"
        $backup = New-Object Microsoft.Data.Sqlite.SqliteConnection "Data Source=$backupPath"
        $backup.Open()
        try { $connection.BackupDatabase($backup) }
        finally { $backup.Close() }
        Write-Ok "Respaldo creado."
    }

    $demoSkus = @(
        "BEB-001", "BEB-002", "BEB-003", "BEB-004", "BEB-005",
        "SNK-001", "SNK-002", "SNK-003", "SNK-004",
        "COM-001", "COM-002", "COM-003",
        "SRV-001", "SRV-002", "SRV-003", "SRV-004"
    )

    $deletedDemos = 0
    $skippedDemos = 0
    $createdUtc = [DateTime]::UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fffffff", [System.Globalization.CultureInfo]::InvariantCulture)
    $inserted = 0
    $updated = 0

    $transaction = $connection.BeginTransaction()
    try {
        if ($DeleteDemoProducts) {
            Write-Step "Eliminando productos de ejemplo"
            foreach ($sku in $demoSkus) {
                if (-not $existing.ContainsKey($sku)) { continue }
                $id = $existing[$sku].Id

                if ((Get-ReferenceCount $connection $id) -gt 0) {
                    Write-Warn "El producto de ejemplo $sku tiene ventas o movimientos: se conserva."
                    $skippedDemos++
                    continue
                }

                $command = New-SqliteCommand $connection "DELETE FROM Products WHERE Id = @id"
                $command.Transaction = $transaction
                [void]$command.Parameters.AddWithValue("@id", $id)
                [void]$command.ExecuteNonQuery()
                $deletedDemos++
            }
            Write-Ok "Productos de ejemplo eliminados: $deletedDemos (conservados: $skippedDemos)"
        }

        Write-Step "Importando productos"
        $index = 0
        foreach ($product in $toInsert) {
            $command = New-SqliteCommand $connection @"
INSERT INTO Products
    (Sku, Name, CategoryId, Price, Cost, Stock, MinStock, Unit, IsService, TaxRate, Active, ImagePath, Icon, CreatedUtc)
VALUES
    (@sku, @name, NULL, @price, '0', @stock, @minStock, 'pza', @isService, @taxRate, 1, NULL, NULL, @createdUtc)
"@
            $command.Transaction = $transaction
            [void]$command.Parameters.AddWithValue("@sku", $product.Code)
            [void]$command.Parameters.AddWithValue("@name", $product.Name)
            [void]$command.Parameters.AddWithValue("@price", $product.Price.ToString([System.Globalization.CultureInfo]::InvariantCulture))
            [void]$command.Parameters.AddWithValue("@stock", $product.Stock.ToString([System.Globalization.CultureInfo]::InvariantCulture))
            [void]$command.Parameters.AddWithValue("@minStock", $product.MinStock.ToString([System.Globalization.CultureInfo]::InvariantCulture))
            [void]$command.Parameters.AddWithValue("@isService", [int]$product.IsService)
            [void]$command.Parameters.AddWithValue("@taxRate", $TaxRate.ToString([System.Globalization.CultureInfo]::InvariantCulture))
            [void]$command.Parameters.AddWithValue("@createdUtc", $createdUtc)
            [void]$command.ExecuteNonQuery()

            $idCommand = New-SqliteCommand $connection "SELECT last_insert_rowid()"
            $idCommand.Transaction = $transaction
            $productId = [int]$idCommand.ExecuteScalar()

            if ($product.Stock -gt 0) {
                $movement = New-SqliteCommand $connection @"
INSERT INTO StockMovements
    (ProductId, Type, Quantity, UnitCost, BalanceAfter, Reference, UserId, Notes, CreatedUtc)
VALUES
    (@productId, 0, @quantity, '0', @balance, 'IMPORT', NULL, 'Stock inicial (importación)', @createdUtc)
"@
                $movement.Transaction = $transaction
                [void]$movement.Parameters.AddWithValue("@productId", $productId)
                [void]$movement.Parameters.AddWithValue("@quantity", $product.Stock.ToString([System.Globalization.CultureInfo]::InvariantCulture))
                [void]$movement.Parameters.AddWithValue("@balance", $product.Stock.ToString([System.Globalization.CultureInfo]::InvariantCulture))
                [void]$movement.Parameters.AddWithValue("@createdUtc", $createdUtc)
                [void]$movement.ExecuteNonQuery()
            }

            $inserted++
            $index++
            if ($index % 50 -eq 0) { Write-Host "  $index / $($toInsert.Count)" -ForegroundColor DarkGray }
        }

        foreach ($item in $toUpdate) {
            $product = $item.Product
            $command = New-SqliteCommand $connection @"
UPDATE Products
SET Name = @name, Price = @price, Stock = @stock, MinStock = @minStock,
    IsService = @isService, TaxRate = @taxRate
WHERE Id = @id
"@
            $command.Transaction = $transaction
            [void]$command.Parameters.AddWithValue("@name", $product.Name)
            [void]$command.Parameters.AddWithValue("@price", $product.Price.ToString([System.Globalization.CultureInfo]::InvariantCulture))
            [void]$command.Parameters.AddWithValue("@stock", $product.Stock.ToString([System.Globalization.CultureInfo]::InvariantCulture))
            [void]$command.Parameters.AddWithValue("@minStock", $product.MinStock.ToString([System.Globalization.CultureInfo]::InvariantCulture))
            [void]$command.Parameters.AddWithValue("@isService", [int]$product.IsService)
            [void]$command.Parameters.AddWithValue("@taxRate", $TaxRate.ToString([System.Globalization.CultureInfo]::InvariantCulture))
            [void]$command.Parameters.AddWithValue("@id", $item.Id)
            [void]$command.ExecuteNonQuery()
            $updated++
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
    Write-Host "Importación completada." -ForegroundColor Green
    Write-Host ("  Nuevos:      {0}" -f $inserted)
    Write-Host ("  Actualizados:{0}" -f $updated)
    if ($DeleteDemoProducts) {
        Write-Host ("  Ejemplos eliminados: {0} (conservados: {1})" -f $deletedDemos, $skippedDemos)
    }
}
finally {
    $connection.Close()
}
