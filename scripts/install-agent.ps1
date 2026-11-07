#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Instala el agente de CyberManager como servicio de Windows en el PC del equipo.

.DESCRIPTION
    Publica CyberManager.Agent, copia los binarios a la carpeta de instalación,
    escribe appsettings.json con el ID y token del equipo y registra el servicio
    CyberManagerAgent para que arranque con Windows.

.PARAMETER StationId
    ID numérico del equipo (se muestra en Administración > Equipos > Configuración / token).

.PARAMETER Token
    Token del equipo. Se obtiene en la misma ventana de la consola de administración.

.PARAMETER ServerUrl
    URL del servidor accesible desde la red local, por ejemplo http://192.168.1.10:5080.

.EXAMPLE
    .\install-agent.ps1 -StationId 1 -Token "abc123..." -ServerUrl "http://192.168.1.10:5080"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][int]$StationId,
    [Parameter(Mandatory = $true)][string]$Token,
    [Parameter(Mandatory = $true)][string]$ServerUrl,
    [string]$InstallDir = "C:\CyberManager\Agent",
    [string]$ServiceName = "CyberManagerAgent",
    [switch]$FrameworkDependent
)

$ErrorActionPreference = "Stop"

function Write-Step([string]$Message) {
    Write-Host "==> $Message" -ForegroundColor Cyan
}

# ---------- Comprobaciones ----------
$principal = New-Object Security.Principal.WindowsPrincipal(
    [Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Ejecute este script en una consola de PowerShell como administrador."
}

if ($StationId -le 0) { throw "StationId debe ser mayor que cero." }
if ([string]::IsNullOrWhiteSpace($Token)) { throw "Debe indicar el token del equipo." }
if ([string]::IsNullOrWhiteSpace($ServerUrl)) { throw "Debe indicar la URL del servidor." }

$ServerUrl = $ServerUrl.TrimEnd('/')
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "src\CyberManager.Agent\CyberManager.Agent.csproj"

if (-not (Test-Path $project)) {
    throw "No se encontró el proyecto del agente en $project."
}

# ---------- Localizar dotnet ----------
$dotnet = Join-Path $env:LOCALAPPDATA "Microsoft\dotnet\dotnet.exe"
if (-not (Test-Path $dotnet)) {
    $command = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($command) { $dotnet = $command.Source }
}
if (-not $dotnet -or -not (Test-Path $dotnet)) {
    throw "No se encontró dotnet.exe. Instale el SDK de .NET 8."
}

# ---------- Publicar ----------
$publishDir = Join-Path $env:TEMP ("cybermanager-agent-" + [Guid]::NewGuid().ToString("N"))
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

Write-Step "Publicando el agente (esto puede tardar unos minutos)..."
& $dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw "Falló la publicación del agente." }

# ---------- Servicio existente ----------
$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Step "Deteniendo el servicio $ServiceName..."
    if ($existing.Status -ne "Stopped") {
        Stop-Service -Name $ServiceName -Force
        $existing.WaitForStatus("Stopped", [TimeSpan]::FromSeconds(20))
    }

    Write-Step "Eliminando el servicio anterior..."
    & sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

# ---------- Copiar archivos ----------
Write-Step "Copiando archivos a $InstallDir..."
New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
Get-ChildItem -Path $InstallDir -Force | Remove-Item -Recurse -Force
Copy-Item -Path (Join-Path $publishDir "*") -Destination $InstallDir -Recurse -Force

# ---------- Configuración ----------
$settings = [ordered]@{
    Logging = @{
        LogLevel = @{
            Default = "Information"
            "Microsoft.Hosting.Lifetime" = "Information"
        }
    }
    Agent = @{
        ServerUrl = $ServerUrl
        StationId = $StationId
        Token = $Token
        HeartbeatSeconds = 10
    }
}

$settingsPath = Join-Path $InstallDir "appsettings.json"
$settings | ConvertTo-Json -Depth 6 | Set-Content -Path $settingsPath -Encoding UTF8
Write-Step "Configuración escrita en $settingsPath"

# ---------- Registrar el servicio ----------
$exePath = Join-Path $InstallDir "CyberManager.Agent.exe"
if (-not (Test-Path $exePath)) { throw "No se encontró $exePath después de copiar los archivos." }

Write-Step "Registrando el servicio $ServiceName..."
& sc.exe create $ServiceName binPath= "`"$exePath`"" start= auto DisplayName= "CyberManager Agent" | Out-Null
if ($LASTEXITCODE -ne 0) { throw "No se pudo crear el servicio." }

& sc.exe description $ServiceName "CyberManager - agente de control del equipo (bloqueo, apagado, reinicio, mensajes)." | Out-Null
# Reiniciar automáticamente si el servicio falla.
& sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/10000/restart/30000 | Out-Null

Write-Step "Iniciando el servicio..."
& sc.exe start $ServiceName | Out-Null

Start-Sleep -Seconds 3
$service = Get-Service -Name $ServiceName
Write-Host ""
Write-Host "Servicio: $($service.Name) - Estado: $($service.Status)" -ForegroundColor Green
Write-Host "Equipo:   $StationId"
Write-Host "Servidor: $ServerUrl"
Write-Host ""
Write-Host "Si el estado no es 'Running', revise el registro de eventos de Windows (Origen: CyberManagerAgent)." -ForegroundColor Yellow

# ---------- Limpieza ----------
Remove-Item -Path $publishDir -Recurse -Force -ErrorAction SilentlyContinue
