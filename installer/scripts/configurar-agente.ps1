#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Configura y registra el agente de CyberManager como servicio de Windows.

.DESCRIPTION
    Escribe appsettings.json con la URL del servidor, el ID y el token del equipo; registra el
    servicio CyberManagerAgent, configura su reinicio automático y lo inicia. Si se ejecuta sin
    parámetros, solicita los datos de forma interactiva.

.PARAMETER StationId
    ID numérico del equipo (Administración > Equipos > Configuración / token).

.PARAMETER Token
    Token del equipo mostrado en la consola de administración.

.PARAMETER ServerUrl
    URL del servidor en la red local, por ejemplo http://192.168.1.10:5080.
#>
[CmdletBinding()]
param(
    [int]$StationId = 0,
    [string]$Token = "",
    [string]$ServerUrl = "",
    [string]$ServiceName = "CyberManagerAgent"
)

$ErrorActionPreference = "Stop"

function Write-Step([string]$Message) {
    Write-Host "==> $Message" -ForegroundColor Cyan
}

$principal = New-Object Security.Principal.WindowsPrincipal(
    [Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Ejecute este script como administrador."
}

$root = Split-Path -Parent $PSScriptRoot
$agentDir = Join-Path $root "Agent"
$exe = Join-Path $agentDir "CyberManager.Agent.exe"
if (-not (Test-Path $exe)) {
    throw "No se encontró el agente en $exe. Instale el componente 'Agente de Windows'."
}

Write-Host ""
Write-Host "Configuración del agente de CyberManager" -ForegroundColor Cyan
Write-Host "Obtenga el ID y el token en Administración > Equipos > Configuración / token." -ForegroundColor DarkGray
Write-Host ""

if ($StationId -le 0) {
    $StationId = [int](Read-Host "ID del equipo")
}
if ([string]::IsNullOrWhiteSpace($Token)) {
    $Token = Read-Host "Token del equipo"
}
if ([string]::IsNullOrWhiteSpace($ServerUrl)) {
    $ServerUrl = Read-Host "URL del servidor (ej. http://192.168.1.10:5080)"
}

if ($StationId -le 0) { throw "El ID del equipo debe ser mayor que cero." }
if ([string]::IsNullOrWhiteSpace($Token)) { throw "Debe indicar el token del equipo." }
if ([string]::IsNullOrWhiteSpace($ServerUrl)) { throw "Debe indicar la URL del servidor." }

$ServerUrl = $ServerUrl.Trim().TrimEnd("/")
if (-not $ServerUrl.StartsWith("http://") -and -not $ServerUrl.StartsWith("https://")) {
    $ServerUrl = "http://$ServerUrl"
}

# ---------- Servicio anterior ----------
$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Step "Deteniendo el servicio anterior..."
    if ($existing.Status -ne "Stopped") {
        Stop-Service -Name $ServiceName -Force
        $existing.WaitForStatus("Stopped", [TimeSpan]::FromSeconds(20))
    }
    & sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

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

$settingsPath = Join-Path $agentDir "appsettings.json"
$settings | ConvertTo-Json -Depth 6 | Set-Content -Path $settingsPath -Encoding UTF8
Write-Step "Configuración escrita en $settingsPath"

# ---------- Servicio ----------
Write-Step "Registrando el servicio $ServiceName..."
& sc.exe create $ServiceName binPath= "`"$exe`"" start= auto DisplayName= "CyberManager Agent" | Out-Null
if ($LASTEXITCODE -ne 0) { throw "No se pudo crear el servicio." }

& sc.exe description $ServiceName "CyberManager - agente de control del equipo (bloqueo, apagado, reinicio, mensajes)." | Out-Null
& sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/10000/restart/30000 | Out-Null

Write-Step "Iniciando el servicio..."
& sc.exe start $ServiceName | Out-Null
Start-Sleep -Seconds 3

$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
Write-Host ""
if ($service -and $service.Status -eq "Running") {
    Write-Host "Agente instalado y en ejecución." -ForegroundColor Green
} else {
    Write-Host "El servicio no está en ejecución. Revise el Visor de eventos de Windows." -ForegroundColor Yellow
}
Write-Host "  Equipo:   $StationId"
Write-Host "  Servidor: $ServerUrl"
Write-Host "  Servicio: $ServiceName"
Write-Host ""
