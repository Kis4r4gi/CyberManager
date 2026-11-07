#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Detiene y elimina los servicios de CyberManager según el instalador que se desinstala.

.DESCRIPTION
    Se ejecuta durante la desinstalación. Con -Servidor elimina la tarea del servidor, la regla de
    firewall y los procesos del servidor. Con -Agente elimina el servicio del agente. Sin
    parámetros elimina ambos (compatibilidad con instalaciones anteriores).

    No borra la base de datos para no perder la información del negocio.
#>
[CmdletBinding()]
param(
    [switch]$Servidor,
    [switch]$Agente
)

$ErrorActionPreference = "SilentlyContinue"
$todo = -not ($Servidor -or $Agente)

Write-Host "==> Deteniendo servicios de CyberManager..." -ForegroundColor Cyan

# ---------- Agente ----------
if ($Agente -or $todo) {
    $agent = Get-Service -Name "CyberManagerAgent" -ErrorAction SilentlyContinue
    if ($agent) {
        if ($agent.Status -ne "Stopped") {
            Stop-Service -Name "CyberManagerAgent" -Force
            $agent.WaitForStatus("Stopped", [TimeSpan]::FromSeconds(15))
        }
        & sc.exe delete "CyberManagerAgent" | Out-Null
        Write-Host "Servicio CyberManagerAgent eliminado." -ForegroundColor Green
    }

    # El cliente interactivo puede mantener bloqueados los archivos de la estación.
    $root = Split-Path -Parent $PSScriptRoot
    $clientExe = Join-Path $root "Client\CyberManager.Client.exe"
    Get-CimInstance Win32_Process -Filter "Name = 'CyberManager.Client.exe'" |
        Where-Object { $_.ExecutablePath -eq $clientExe } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
}

# ---------- Servidor ----------
if ($Servidor -or $todo) {
    $task = Get-ScheduledTask -TaskName "CyberManagerServer" -ErrorAction SilentlyContinue
    if ($task) {
        Stop-ScheduledTask -TaskName "CyberManagerServer"
        Unregister-ScheduledTask -TaskName "CyberManagerServer" -Confirm:$false
        Write-Host "Tarea programada CyberManagerServer eliminada." -ForegroundColor Green
    }

    # Detiene el servidor, el lanzador y la consola iniciados desde esta carpeta.
    $root = Split-Path -Parent $PSScriptRoot
    $serverExe = Join-Path $root "Server\CyberManager.Server.exe"
    $launcherExe = Join-Path $root "Server\CyberManager.exe"
    $adminExe = Join-Path $root "Admin\CyberManager.Admin.exe"

    Get-CimInstance Win32_Process |
        Where-Object {
            $_.ExecutablePath -eq $serverExe -or
            $_.ExecutablePath -eq $launcherExe -or
            $_.ExecutablePath -eq $adminExe
        } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force }

    # ---------- Firewall ----------
    Get-NetFirewallRule -DisplayName "CyberManager Server*" -ErrorAction SilentlyContinue |
        Remove-NetFirewallRule -ErrorAction SilentlyContinue
}

Write-Host ""
Write-Host "Servicios detenidos. La base de datos se conserva en la carpeta Server." -ForegroundColor Yellow
