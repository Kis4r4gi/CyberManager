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

# Permite que el servidor descubra la estación y el agente por UDP (escaneo de red).
function Enable-DiscoveryFirewall {
    $name = "CyberManager Estación (descubrimiento)"
    try {
        Remove-NetFirewallRule -DisplayName $name -ErrorAction SilentlyContinue
        New-NetFirewallRule -DisplayName $name -Direction Inbound -Action Allow `
            -Protocol UDP -LocalPort 5092, 5093 -Profile Any | Out-Null
        Write-Step "Firewall: puertos UDP 5092 y 5093 habilitados para el descubrimiento."
    }
    catch {
        Write-Warning "No se pudieron crear las reglas de firewall: $_"
    }
}

$principal = New-Object Security.Principal.WindowsPrincipal(
    [Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    # Se relanza con UAC conservando los parámetros. Sin #Requires, el script se puede
    # analizar y ejecutar siempre; la directiva anterior impedía incluso parsearlo y
    # PowerShell solo mostraba un error de sintaxis en lugar de pedir la elevación.
    Write-Host "Se necesitan permisos de administrador. Solicitando elevación..." -ForegroundColor Yellow
    $arguments = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "`"$PSCommandPath`"")
    foreach ($entry in $PSBoundParameters.GetEnumerator()) {
        if ($entry.Value -is [switch]) {
            if ($entry.Value.IsPresent) { $arguments += "-$($entry.Key)" }
        }
        elseif ($null -ne $entry.Value -and "$($entry.Value)" -ne "") {
            $arguments += "-$($entry.Key)"
            $arguments += "`"$($entry.Value)`""
        }
    }
    $arguments += "-NoExit"
    Start-Process -FilePath "powershell.exe" -ArgumentList $arguments -Verb RunAs
    exit
}

$root = Split-Path -Parent $PSScriptRoot
$agentDir = Join-Path $root "Agent"
$exe = Join-Path $agentDir "CyberManager.Agent.exe"
if (-not (Test-Path $exe)) {
    throw "No se encontró el agente en $exe. Instale el componente 'Agente de Windows'."
}

Write-Host ""
Write-Host "Configuración del agente de CyberManager" -ForegroundColor Cyan
Write-Host "El servidor puede encontrar este equipo con «Escanear la red» y asignarle" -ForegroundColor DarkGray
Write-Host "el ID y el token automáticamente. También puede escribirlos ahora." -ForegroundColor DarkGray
Write-Host ""

$manual = ($StationId -gt 0) -or (-not [string]::IsNullOrWhiteSpace($Token))
if (-not $manual) {
    $answer = Read-Host "¿Configurar manualmente el ID y el token? (s/N)"
    $manual = $answer -match '^(s|si|sí|y|yes)$'
}

if ($manual) {
    if ($StationId -le 0) {
        $StationId = [int](Read-Host "ID del equipo")
    }
    if ([string]::IsNullOrWhiteSpace($Token)) {
        $Token = Read-Host "Token del equipo"
    }
    if ($StationId -le 0) { throw "El ID del equipo debe ser mayor que cero." }
    if ([string]::IsNullOrWhiteSpace($Token)) { throw "Debe indicar el token del equipo." }
}
else {
    $StationId = 0
    $Token = ""
    Write-Step "El equipo quedará a la espera del escaneo de red del servidor."
}

if ([string]::IsNullOrWhiteSpace($ServerUrl)) {
    $ServerUrl = Read-Host "URL del servidor (ej. http://192.168.1.10:5080)"
}

if ([string]::IsNullOrWhiteSpace($ServerUrl)) { throw "Debe indicar la URL del servidor." }

$ServerUrl = $ServerUrl.Trim().TrimEnd("/")
if (-not $ServerUrl.StartsWith("http://") -and -not $ServerUrl.StartsWith("https://")) {
    $ServerUrl = "http://$ServerUrl"
}

Enable-DiscoveryFirewall

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
