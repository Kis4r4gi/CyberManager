#Requires -Version 5.1
<#
.SYNOPSIS
    Abre la configuración de TightVNC y actualiza la regla de Firewall de CyberManager.

.DESCRIPTION
    Acceso directo «Configurar servidor VNC» del instalador de estación. Comprueba que
    TightVNC esté instalado, lee el puerto RFB del registro, actualiza la regla de Firewall
    para permitir solo la red local y abre la ventana de configuración del servicio.

    Si se ejecuta sin permisos de administrador, solicita elevación con UAC.

.PARAMETER Port
    Puerto RFB que se usará en la regla de Firewall. Si se omite, se lee del registro de
    TightVNC (RfbPort) y, si no existe, se usa 5900.
#>
[CmdletBinding()]
param(
    [int]$Port = 0
)

$ErrorActionPreference = "Stop"

function Write-Step([string]$Message) {
    Write-Host "==> $Message" -ForegroundColor Cyan
}

$principal = New-Object Security.Principal.WindowsPrincipal(
    [Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Step "Se necesitan permisos de administrador. Abriendo una ventana elevada..."
    Start-Process -FilePath "powershell.exe" -Verb RunAs -ArgumentList @(
        "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "`"$PSCommandPath`""
    ) | Out-Null
    exit
}

function Find-TightVncServer {
    $candidates = @(
        (Join-Path $env:ProgramFiles "TightVNC\tvnserver.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "TightVNC\tvnserver.exe")
    )
    foreach ($path in $candidates) {
        if ($path -and (Test-Path $path)) { return $path }
    }
    return $null
}

function Get-TightVncPort {
    $keys = @(
        "HKLM:\SOFTWARE\TightVNC\Server",
        "HKLM:\SOFTWARE\WOW6432Node\TightVNC\Server"
    )
    foreach ($key in $keys) {
        try {
            $value = (Get-ItemProperty -Path $key -Name "RfbPort" -ErrorAction Stop).RfbPort
            if ($value -is [int] -and $value -ge 1 -and $value -le 65535) { return [int]$value }
        }
        catch { }
    }
    return 5900
}

function Set-CyberManagerVncFirewall([int]$RfbPort) {
    $ruleName = "CyberManager VNC (TightVNC)"
    Get-NetFirewallRule -DisplayName "CyberManager VNC*" -ErrorAction SilentlyContinue |
        Remove-NetFirewallRule -ErrorAction SilentlyContinue

    New-NetFirewallRule `
        -DisplayName $ruleName `
        -Direction Inbound `
        -Protocol TCP `
        -LocalPort $RfbPort `
        -Action Allow `
        -Profile Domain,Private `
        -RemoteAddress LocalSubnet `
        -Description "Permite VNC (TightVNC) solo desde la red local. Instalado por CyberManager." | Out-Null
}

$exe = Find-TightVncServer
if (-not $exe) {
    Write-Host "No se encontró TightVNC en este equipo." -ForegroundColor Yellow
    Write-Host "Instálelo desde el instalador de CyberManager Estación, en la tarea de control remoto." -ForegroundColor Yellow
    Read-Host "Presione Enter para cerrar"
    exit 1
}

if ($Port -le 0) { $Port = Get-TightVncPort }

Set-CyberManagerVncFirewall $Port
Write-Step "Regla de Firewall actualizada para el puerto TCP $Port (solo red local)."

# El servicio debe estar activo para abrir la configuración en modo servicio.
$service = Get-Service -Name "tvnserver" -ErrorAction SilentlyContinue
if ($service -and $service.Status -ne "Running") {
    Write-Step "Iniciando el servicio TightVNC..."
    Start-Service -Name "tvnserver" -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    $service = Get-Service -Name "tvnserver" -ErrorAction SilentlyContinue
}

Write-Step "Abriendo la configuración de TightVNC..."
$process = Start-Process -FilePath $exe -ArgumentList "-controlservice" -PassThru -ErrorAction SilentlyContinue
if ($process) {
    Start-Sleep -Seconds 3
    if ($process.HasExited -and $process.ExitCode -ne 0) {
        # Sin servicio activo, la configuración offline escribe directamente en el registro.
        Start-Process -FilePath $exe -ArgumentList "-configservice" | Out-Null
    }
}
else {
    Start-Process -FilePath $exe -ArgumentList "-configservice" | Out-Null
}

Write-Host ""
Write-Host "Recuerde usar una contraseña de 4 a 8 caracteres." -ForegroundColor Yellow
Write-Host "Si cambió el puerto en TightVNC, vuelva a abrir este acceso directo para actualizar el Firewall." -ForegroundColor Yellow
Write-Host "No exponga VNC a Internet: la regla solo permite la red local." -ForegroundColor Yellow
