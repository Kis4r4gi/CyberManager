#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Registra el servidor de CyberManager para que arranque con Windows y abre el puerto 5080.

.DESCRIPTION
    Se ejecuta desde el instalador. Crea una tarea programada llamada CyberManagerServer que
    inicia el servidor como SYSTEM al encender el equipo y, opcionalmente, agrega la regla de
    Firewall para que las estaciones de la red local puedan conectarse.

.PARAMETER RegistrarTarea
    Crea o actualiza la tarea programada CyberManagerServer y la inicia de inmediato.

.PARAMETER AbrirFirewall
    Crea la regla de entrada TCP 5080 para todos los perfiles de red. Windows suele clasificar
    como "Pública" la red del local, así que limitar la regla a Dominio/Privado impediría que las
    estaciones se conectaran.
#>
[CmdletBinding()]
param(
    [switch]$RegistrarTarea,
    [switch]$AbrirFirewall
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
$serverDir = Join-Path $root "Server"
$exe = Join-Path $serverDir "CyberManager.Server.exe"
if (-not (Test-Path $exe)) {
    throw "No se encontró el servidor en $exe."
}

if ($RegistrarTarea) {
    Write-Step "Registrando la tarea programada CyberManagerServer..."

    $action = New-ScheduledTaskAction -Execute $exe -WorkingDirectory $serverDir
    $trigger = New-ScheduledTaskTrigger -AtStartup
    $principalTask = New-ScheduledTaskPrincipal -UserId "SYSTEM" `
        -LogonType ServiceAccount -RunLevel Highest
    $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries `
        -DontStopIfGoingOnBatteries -StartWhenAvailable `
        -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1) `
        -ExecutionTimeLimit ([TimeSpan]::Zero)

    Register-ScheduledTask -TaskName "CyberManagerServer" -Action $action `
        -Trigger $trigger -Principal $principalTask -Settings $settings `
        -Description "Servidor central de CyberManager (API, SignalR y base de datos)." `
        -Force | Out-Null

    Write-Step "Iniciando el servidor..."
    Start-ScheduledTask -TaskName "CyberManagerServer"
    Start-Sleep -Seconds 2
}

if ($AbrirFirewall) {
    Write-Step "Abriendo el puerto TCP 5080 en el Firewall de Windows..."
    $ruleName = "CyberManager Server 5080"
    Remove-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue
    New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Protocol TCP `
        -LocalPort 5080 -Action Allow -Profile Domain,Private,Public `
        -Description "Acceso de las estaciones al servidor de CyberManager." | Out-Null
}

Write-Host ""
Write-Host "Servidor configurado." -ForegroundColor Green
Write-Host "  Ejecutable: $exe"
Write-Host "  API:        http://127.0.0.1:5080"
Write-Host "  Base:       $(Join-Path $serverDir 'cybermanager.db')"
Write-Host ""
Write-Host "Si el servidor no responde, ejecute el acceso directo 'CyberManager Servidor' y revise la ventana." -ForegroundColor Yellow
