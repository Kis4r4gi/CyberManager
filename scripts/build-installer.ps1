<#
.SYNOPSIS
    Publica CyberManager y genera los instaladores unificados de servidor y estación.

.DESCRIPTION
    Crea distribuciones autocontenidas para Windows x64 y x86 (no requieren .NET instalado
    en el equipo destino), copia los scripts de configuración y compila dos instaladores con
    Inno Setup 6: CyberManager-Servidor y CyberManager-Estacion. Cada instalador incluye los
    payloads de dist\x64 y dist\x86 y elige automáticamente el adecuado en tiempo de
    instalación, por lo que se mantiene la separación entre servidor y estación.

.PARAMETER Version
    Versión explícita (por ejemplo 1.0.5). Si se omite, el número de compilación
    (patch) se incrementa automáticamente en version.props antes de publicar.

.PARAMETER NoBump
    Usa la versión actual de version.props sin incrementarla.

.PARAMETER Architecture
    Payloads a publicar: all (x64 y x86, valor por defecto), x64 o x86. Los instaladores
    unificados requieren ambas arquitecturas; use x64 o x86 junto con -SkipInstaller para
    publicaciones de diagnóstico.

.PARAMETER Configuration
    Configuración de compilación. Por defecto Release.

.PARAMETER SkipPublish
    Reutiliza las carpetas dist\<arch> existentes y solo compila los instaladores.

.PARAMETER SkipInstaller
    Publica los archivos pero no genera los instaladores.

.PARAMETER InstallInnoSetup
    Si no encuentra Inno Setup, intenta instalarlo con winget.

.PARAMETER Clean
    Al terminar, elimina las carpetas bin y obj de los proyectos para reducir el tamaño en disco.

.EXAMPLE
    .\scripts\build-installer.ps1

.EXAMPLE
    .\scripts\build-installer.ps1 -Version 1.1.0

.EXAMPLE
    .\scripts\build-installer.ps1 -Architecture x86 -SkipInstaller
#>
[CmdletBinding()]
param(
    [string]$Version,
    [switch]$NoBump,
    [ValidateSet("x64", "x86", "all")]
    [string]$Architecture = "all",
    [string]$Configuration = "Release",
    [switch]$SkipPublish,
    [switch]$SkipInstaller,
    [switch]$InstallInnoSetup,
    [switch]$Clean
)

$ErrorActionPreference = "Stop"

function Write-Step([string]$Message) {
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Find-Dotnet {
    $candidate = Join-Path $env:LOCALAPPDATA "Microsoft\dotnet\dotnet.exe"
    if (Test-Path $candidate) { return $candidate }

    $command = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }

    throw "No se encontró dotnet.exe. Instale el SDK de .NET 8."
}

function Find-InnoSetup {
    $candidates = @(
        (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"),
        (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe")
    )

    foreach ($path in $candidates) {
        if ($path -and (Test-Path $path)) { return $path }
    }

    $command = Get-Command iscc -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }

    return $null
}

# Elimina archivos que no se usan en ejecución para reducir el tamaño del instalador.
function Remove-PublishClutter([string]$Path) {
    if (-not (Test-Path $Path)) { return }

    Get-ChildItem -Path $Path -Recurse -File -Filter *.pdb -Force -ErrorAction SilentlyContinue |
        Remove-Item -Force -ErrorAction SilentlyContinue

    # Documentación XML: solo se elimina cuando acompaña a una DLL (no borra appsettings.xml).
    Get-ChildItem -Path $Path -Recurse -File -Filter *.xml -Force -ErrorAction SilentlyContinue |
        ForEach-Object {
            $dll = [System.IO.Path]::ChangeExtension($_.FullName, ".dll")
            if (Test-Path $dll) { Remove-Item -Path $_.FullName -Force -ErrorAction SilentlyContinue }
        }

    # Recursos satélite de idiomas no soportados (la aplicación solo usa español e inglés).
    $otherCultures = @(
        "cs", "da", "de", "el", "fi", "fr", "hu", "it", "ja", "ko", "nb", "nl", "pl",
        "pt", "pt-BR", "pt-PT", "ro", "ru", "sk", "sv", "tr", "uk", "zh", "zh-CN",
        "zh-Hans", "zh-Hant", "zh-TW"
    )
    Get-ChildItem -Path $Path -Recurse -Directory -Force -ErrorAction SilentlyContinue |
        Where-Object { $otherCultures -contains $_.Name } |
        Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$distRoot = Join-Path $repoRoot "dist"
$artifacts = Join-Path $repoRoot "artifacts"
$installerDir = Join-Path $repoRoot "installer"
$versionFile = Join-Path $repoRoot "version.props"
$dotnet = Find-Dotnet

# ---------- Versión de compilación ----------
function Read-ProjectVersion([string]$Path) {
    if (-not (Test-Path $Path)) { return "1.0.0" }

    [xml]$xml = Get-Content -Path $Path -Raw
    $value = $xml.Project.PropertyGroup.CyberManagerVersion
    if ($value -is [array]) { $value = $value[0] }
    if ([string]::IsNullOrWhiteSpace($value)) {
        throw "No se encontró CyberManagerVersion en $Path."
    }
    return ([string]$value).Trim()
}

function Write-ProjectVersion([string]$Path, [string]$Value) {
    $content = @"
<Project>
  <!--
    Versión de todos los ensamblados. ``scripts/build-installer.ps1`` incrementa el
    número de compilación (patch) automáticamente en cada empaquetado.
  -->
  <PropertyGroup>
    <CyberManagerVersion>$Value</CyberManagerVersion>
  </PropertyGroup>
</Project>
"@

    # UTF-8 sin BOM para que MSBuild y PowerShell lean el archivo sin problemas.
    [System.IO.File]::WriteAllText($Path, $content, [System.Text.UTF8Encoding]::new($false))
}

function Get-NextPatchVersion([string]$Current) {
    $parts = $Current.Split('.')
    if ($parts.Count -lt 2) { throw "Versión no válida en version.props: $Current" }
    $major = [int]$parts[0]
    $minor = [int]$parts[1]
    $patch = if ($parts.Count -ge 3) { [int]$parts[2] } else { 0 }
    return "$major.$minor.$($patch + 1)"
}

$currentVersion = Read-ProjectVersion $versionFile

if ([string]::IsNullOrWhiteSpace($Version)) {
    # Al republicar solo los instaladores no se incrementa la versión para que el
    # rótulo coincida con los binarios ya publicados. -NoBump conserva el valor actual.
    if ($NoBump -or $SkipPublish) {
        $Version = $currentVersion
    }
    else {
        $Version = Get-NextPatchVersion $currentVersion
    }
}

if ($Version -notmatch '^\d+(\.\d+){1,3}$') {
    throw "Versión no válida: $Version. Use el formato 1.0.0."
}

if ($Version -ne $currentVersion) {
    Write-ProjectVersion $versionFile $Version
    Write-Host "==> Versión de compilación: $currentVersion -> $Version" -ForegroundColor Cyan
}
elseif (-not $NoBump) {
    Write-Host "==> Versión de compilación: $Version" -ForegroundColor Cyan
}

$architectures = if ($Architecture -eq "all") { @("x64", "x86") } else { @($Architecture) }

if (-not $SkipInstaller -and $architectures.Count -lt 2) {
    throw "Los instaladores unificados incluyen x64 y x86. Use -Architecture all o agregue -SkipInstaller para publicar una sola arquitectura."
}

$installers = @(
    @{ Iss = Join-Path $installerDir "CyberManager.Servidor.iss"; Name = "Servidor"; Display = "Servidor" },
    @{ Iss = Join-Path $installerDir "CyberManager.Estacion.iss"; Name = "Estacion"; Display = "Estación" }
)

$projects = [ordered]@{
    Server = Join-Path $repoRoot "src\CyberManager.Server\CyberManager.Server.csproj"
    Admin  = Join-Path $repoRoot "src\CyberManager.Admin\CyberManager.Admin.csproj"
    Client = Join-Path $repoRoot "src\CyberManager.Client\CyberManager.Client.csproj"
    Agent  = Join-Path $repoRoot "src\CyberManager.Agent\CyberManager.Agent.csproj"
}

$uninstallerProject = Join-Path $repoRoot "src\CyberManager.Uninstaller\CyberManager.Uninstaller.csproj"

if (-not $SkipPublish) {
    Write-Step "Limpiando las carpetas dist..."
    if (Test-Path $distRoot) { Remove-Item -Path $distRoot -Recurse -Force }

    foreach ($arch in $architectures) {
        $runtime = "win-$arch"
        $distArch = Join-Path $distRoot $arch

        foreach ($item in $projects.GetEnumerator()) {
            $name = $item.Key
            $project = $item.Value
            $output = Join-Path $distArch $name

            if (-not (Test-Path $project)) { throw "No se encontró el proyecto $project." }

            Write-Step "Publicando $name para $arch ($runtime, autocontenido)..."
            & $dotnet publish $project `
                -c $Configuration `
                -r $runtime `
                --self-contained true `
                -o $output `
                --nologo `
                -p:DebugType=None `
                -p:DebugSymbols=false `
                -p:PublishSingleFile=false

            if ($LASTEXITCODE -ne 0) { throw "Falló la publicación de $name para $arch." }

            Remove-PublishClutter $output
        }

        # Desinstalador independiente: un único ejecutable ligero que localiza las
        # instalaciones de Inno Setup y las desinstala en orden.
        $uninstallerOutput = Join-Path $distArch "Uninstaller"
        if (-not (Test-Path $uninstallerProject)) {
            throw "No se encontró el proyecto $uninstallerProject."
        }

        Write-Step "Publicando Uninstaller para $arch ($runtime, autocontenido)..."
        & $dotnet publish $uninstallerProject `
            -c $Configuration `
            -r $runtime `
            --self-contained true `
            -o $uninstallerOutput `
            --nologo `
            -p:DebugType=None `
            -p:DebugSymbols=false `
            -p:PublishSingleFile=true `
            -p:PublishTrimmed=true `
            -p:TrimMode=partial `
            -p:EnableCompressionInSingleFile=true `
            -p:SuppressTrimAnalysisWarnings=true

        if ($LASTEXITCODE -ne 0) { throw "Falló la publicación de Uninstaller para $arch." }

        Remove-PublishClutter $uninstallerOutput

        Write-Step "Copiando scripts de configuración para $arch..."
        $scriptsTarget = Join-Path $distArch "Scripts"
        New-Item -ItemType Directory -Force -Path $scriptsTarget | Out-Null
        Copy-Item -Path (Join-Path $installerDir "scripts\*") -Destination $scriptsTarget -Recurse -Force
        Copy-Item -Path (Join-Path $repoRoot "README.md") -Destination $distArch -Force
    }
}
else {
    foreach ($arch in $architectures) {
        $distArch = Join-Path $distRoot $arch
        if (-not (Test-Path $distArch)) {
            throw "No existe la carpeta dist\$arch. Ejecute el script sin -SkipPublish."
        }

        Write-Step "Actualizando scripts de configuración en dist\$arch..."
        $scriptsTarget = Join-Path $distArch "Scripts"
        New-Item -ItemType Directory -Force -Path $scriptsTarget | Out-Null
        Copy-Item -Path (Join-Path $installerDir "scripts\*") -Destination $scriptsTarget -Recurse -Force
        Copy-Item -Path (Join-Path $repoRoot "README.md") -Destination $distArch -Force
    }
}

if ($SkipInstaller) {
    Write-Host ""
    Write-Host "Publicación lista en: $distRoot" -ForegroundColor Green
}
else {
    $iscc = Find-InnoSetup
    if (-not $iscc -and $InstallInnoSetup) {
        $winget = Get-Command winget -ErrorAction SilentlyContinue
        if (-not $winget) {
            throw "No se encontró Inno Setup ni winget. Instale Inno Setup 6 desde https://jrsoftware.org/isdl.php"
        }

        Write-Step "Instalando Inno Setup 6 con winget..."
        & winget install --id JRSoftware.InnoSetup -e --silent `
            --accept-source-agreements --accept-package-agreements
        $iscc = Find-InnoSetup
    }

    if (-not $iscc) {
        Write-Host ""
        Write-Host "No se encontró Inno Setup 6 (ISCC.exe)." -ForegroundColor Yellow
        Write-Host "Instálelo con:" -ForegroundColor Yellow
        Write-Host "  winget install --id JRSoftware.InnoSetup -e" -ForegroundColor Yellow
        Write-Host "o descárguelo de https://jrsoftware.org/isdl.php y vuelva a ejecutar:" -ForegroundColor Yellow
        Write-Host "  .\scripts\build-installer.ps1 -SkipPublish" -ForegroundColor Yellow
        throw "Inno Setup no está instalado."
    }

    if (-not (Test-Path $artifacts)) {
        New-Item -ItemType Directory -Force -Path $artifacts | Out-Null
    }

    foreach ($arch in @("x64", "x86")) {
        if (-not (Test-Path (Join-Path $distRoot $arch))) {
            throw "No existe dist\$arch. Los instaladores unificados requieren ambos payloads; ejecute el script con -Architecture all."
        }

        if (-not (Test-Path (Join-Path $distRoot "$arch\Uninstaller"))) {
            throw "No existe dist\$arch\Uninstaller. Ejecute el script sin -SkipPublish para publicar el desinstalador."
        }
    }

    # Los instaladores por arquitectura quedaron reemplazados por los unificados.
    Get-ChildItem -Path $artifacts -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match '^CyberManager-(Servidor|Estacion)-.+-x(64|86)\.exe$' } |
        Remove-Item -Force -ErrorAction SilentlyContinue

    Write-Step "Generando los instaladores unificados (x64 + x86) con Inno Setup..."
    $generated = @()

    foreach ($installer in $installers) {
        if (-not (Test-Path $installer.Iss)) { throw "No se encontró $($installer.Iss)." }

        $setup = Join-Path $artifacts "CyberManager-$($installer.Name)-$Version.exe"
        if (Test-Path $setup) { Remove-Item -Path $setup -Force }

        Write-Step "Compilando instalador unificado de $($installer.Display)..."
        & $iscc "/DMyAppVersion=$Version" $installer.Iss
        if ($LASTEXITCODE -ne 0) { throw "Falló la compilación del instalador de $($installer.Display)." }

        if (-not (Test-Path $setup)) { throw "No se generó el instalador esperado en $setup." }
        $generated += $setup
    }

    Write-Host ""
    Write-Host "Instaladores generados:" -ForegroundColor Green
    foreach ($setup in $generated) {
        $sizeMb = [Math]::Round((Get-Item $setup).Length / 1MB, 1)
        Write-Host "  $setup ($sizeMb MB)" -ForegroundColor Green
    }
}

if ($Clean) {
    Write-Step "Eliminando carpetas bin y obj..."
    Get-ChildItem -Path (Join-Path $repoRoot "src") -Recurse -Directory -Force -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -in @("bin", "obj") } |
        Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "Limpieza completada." -ForegroundColor Green
}
