#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Publica el cliente de CyberManager y lo deja iniciando con la sesión de Windows.

.DESCRIPTION
    Publica CyberManager.Client (WPF), copia los binarios a la carpeta de instalación,
    escribe la configuración del equipo (%LOCALAPPDATA%\CyberManager\Client\config.json)
    y registra la tarea programada CyberManagerClient para que el cliente se abra con
    permisos elevados al iniciar sesión (sin UAC en cada arranque).

.PARAMETER StationId
    ID numérico del equipo (Administración > Equipos > Configuración / token).

.PARAMETER Token
    Token del equipo.

.PARAMETER ServerUrl
    URL del servidor accesible desde la red local, por ejemplo http://192.168.1.10:5080.

.EXAMPLE
    .\install-client-startup.ps1 -StationId 1 -Token "abc123..." -ServerUrl "http://192.168.1.10:5080"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][int]$StationId,
    [Parameter(Mandatory = $true)][string]$Token,
    [Parameter(Mandatory = $true)][string]$ServerUrl,
    [string]$InstallDir = "C:\CyberManager\Client",
    [switch]$FrameworkDependent,
    [switch]$NoPublish
)

$ErrorActionPreference = "Stop"

function Write-Step([string]$Message) {
    Write-Host "==> $Message" -ForegroundColor Cyan
}

# ---------- Comprobaciones ----------
$principal = New-Object Security.Principal.WindowsPrincipal(
    [Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Ejecute este script en una consola de PowerShell como administrador para instalar en C:\CyberManager."
}

if ($StationId -le 0) { throw "StationId debe ser mayor que cero." }
if ([string]::IsNullOrWhiteSpace($Token)) { throw "Debe indicar el token del equipo." }

$ServerUrl = $ServerUrl.TrimEnd('/')
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "src\CyberManager.Client\CyberManager.Client.csproj"
$exePath = Join-Path $InstallDir "CyberManager.Client.exe"

# ---------- Publicar ----------
if (-not $NoPublish) {
    if (-not (Test-Path $project)) { throw "No se encontró el proyecto del cliente en $project." }

    $dotnet = Join-Path $env:LOCALAPPDATA "Microsoft\dotnet\dotnet.exe"
    if (-not (Test-Path $dotnet)) {
        $command = Get-Command dotnet -ErrorAction SilentlyContinue
        if ($command) { $dotnet = $command.Source }
    }
    if (-not $dotnet -or -not (Test-Path $dotnet)) {
        throw "No se encontró dotnet.exe. Instale el SDK de .NET 8."
    }

    $publishDir = Join-Path $env:TEMP ("cybermanager-client-" + [Guid]::NewGuid().ToString("N"))
    $publishArgs = @(
        "publish", $project,
        "-c", "Release",
        "-r", "win-x64",
        "-o", $publishDir,
        "--nologo"
    )
    if ($FrameworkDependent) {
        $publishArgs += @("--self-contained", "false")
    } else {
        $publishArgs += @("--self-contained", "true")
    }

    Write-Step "Publicando el cliente (esto puede tardar unos minutos)..."
    & $dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) { throw "Falló la publicación del cliente." }

    Write-Step "Copiando archivos a $InstallDir..."
    New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
    Get-ChildItem -Path $InstallDir -Force | Remove-Item -Recurse -Force
    Copy-Item -Path (Join-Path $publishDir "*") -Destination $InstallDir -Recurse -Force
    Remove-Item -Path $publishDir -Recurse -Force -ErrorAction SilentlyContinue
}

if (-not (Test-Path $exePath)) { throw "No se encontró $exePath." }

# ---------- Configuración del equipo ----------
$configDir = Join-Path $env:LOCALAPPDATA "CyberManager\Client"
$configPath = Join-Path $configDir "config.json"
New-Item -ItemType Directory -Force -Path $configDir | Out-Null

$config = [ordered]@{
    ServerUrl = $ServerUrl
    LastUsername = $null
    StationId = $StationId
    StationToken = $Token
}
$config | ConvertTo-Json | Set-Content -Path $configPath -Encoding UTF8
Write-Step "Configuración escrita en $configPath"

# ---------- Inicio automático con la tarea elevada ----------
$taskScript = Join-Path $repoRoot "installer\scripts\configurar-cliente.ps1"
if (-not (Test-Path $taskScript)) {
    throw "No se encontró $taskScript. Actualice el repositorio e inténtelo de nuevo."
}

& $taskScript -ClientExe $exePath

Write-Host ""
Write-Host "Cliente instalado correctamente." -ForegroundColor Green
Write-Host "Equipo:   $StationId"
Write-Host "Servidor: $ServerUrl"
Write-Host "Ejecutable: $exePath"
Write-Host ""
Write-Host "Cierre la sesión y vuelva a entrar para comprobar el inicio automático." -ForegroundColor Yellow
Write-Host "También puede abrirlo ahora desde el menú Inicio o con: schtasks /Run /TN CyberManagerClient" -ForegroundColor Yellow
