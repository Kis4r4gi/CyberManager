<#
.SYNOPSIS
    Registra el cliente de CyberManager para que se inicie con Windows con permisos elevados.

.DESCRIPTION
    Crea la tarea programada CyberManagerClient con un desencadenador de inicio de sesión
    para los usuarios del equipo. La tarea se ejecuta con el token de mayor nivel
    disponible, de modo que el cliente arranca como administrador sin mostrar el UAC en
    cada inicio. Sustituye al acceso directo de la carpeta Inicio.

.PARAMETER ClientExe
    Ruta de CyberManager.Client.exe. Por defecto se usa ..\Client\CyberManager.Client.exe.

.PARAMETER TaskName
    Nombre de la tarea programada.

.PARAMETER Start
    Inicia la tarea inmediatamente después de registrarla.

.PARAMETER Remove
    Elimina la tarea programada y termina (se usa al desinstalar).
#>
[CmdletBinding()]
param(
    [string]$ClientExe = "",
    [string]$TaskName = "CyberManagerClient",
    [switch]$Start,
    [switch]$Remove
)

$ErrorActionPreference = "Stop"

function Write-Step([string]$Message) {
    Write-Host "==> $Message" -ForegroundColor Cyan
}

# ---------- Elevación automática ----------
# Sin #Requires: la directiva impide que el script se analice sin privilegios y el
# instalador solo muestra un error de sintaxis en lugar de pedir la elevación.
$principal = New-Object Security.Principal.WindowsPrincipal(
    [Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
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

if ($Remove) {
    $existing = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
    if ($existing) {
        Write-Step "Quitando la tarea programada $TaskName..."
        Stop-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
        Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
        Write-Host "Tarea $TaskName eliminada." -ForegroundColor Green
    }
    else {
        Write-Host "La tarea $TaskName no estaba registrada." -ForegroundColor DarkGray
    }

    $watchdogName = "$TaskName" + "Watchdog"
    if (Get-ScheduledTask -TaskName $watchdogName -ErrorAction SilentlyContinue) {
        Write-Step "Quitando la tarea programada $watchdogName..."
        Unregister-ScheduledTask -TaskName $watchdogName -Confirm:$false
        Write-Host "Tarea $watchdogName eliminada." -ForegroundColor Green
    }
    exit 0
}

if ([string]::IsNullOrWhiteSpace($ClientExe)) {
    $ClientExe = Join-Path (Split-Path -Parent $PSScriptRoot) "Client\CyberManager.Client.exe"
}
if (-not (Test-Path -LiteralPath $ClientExe)) {
    throw "No se encontró el cliente en $ClientExe."
}

$ClientExe = (Resolve-Path -LiteralPath $ClientExe).Path
$clientDir = Split-Path -Parent $ClientExe
$command = [System.Security.SecurityElement]::Escape($ClientExe)
$workingDir = [System.Security.SecurityElement]::Escape($clientDir)

# La tarea se registra para el grupo Usuarios: se ejecuta en la sesión del usuario que
# inicia sesión y, si es administrador, con el token elevado (sin UAC en cada arranque).
$xml = @"
<?xml version="1.0" encoding="UTF-16"?>
<Task version="1.4" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
  <RegistrationInfo>
    <Description>Cliente de CyberManager: inicia con la sesión de Windows con permisos elevados.</Description>
    <URI>\$TaskName</URI>
  </RegistrationInfo>
  <Triggers>
    <LogonTrigger>
      <Enabled>true</Enabled>
      <Delay>PT15S</Delay>
    </LogonTrigger>
  </Triggers>
  <Principals>
    <Principal id="Author">
      <GroupId>S-1-5-32-545</GroupId>
      <RunLevel>HighestAvailable</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>false</AllowHardTerminate>
    <StartWhenAvailable>false</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <IdleSettings>
      <StopOnIdleEnd>false</StopOnIdleEnd>
      <RestartOnIdle>false</RestartOnIdle>
    </IdleSettings>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>false</Hidden>
    <RunOnlyIfIdle>false</RunOnlyIfIdle>
    <DisallowStartOnRemoteAppSession>false</DisallowStartOnRemoteAppSession>
    <UseUnifiedSchedulingEngine>true</UseUnifiedSchedulingEngine>
    <WakeToRun>false</WakeToRun>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <Priority>7</Priority>
    <RestartOnFailure>
      <Interval>PT1M</Interval>
      <Count>3</Count>
    </RestartOnFailure>
  </Settings>
  <Actions Context="Author">
    <Exec>
      <Command>$command</Command>
      <WorkingDirectory>$workingDir</WorkingDirectory>
    </Exec>
  </Actions>
</Task>
"@

Write-Step "Registrando la tarea programada $TaskName..."
Register-ScheduledTask -TaskName $TaskName -Xml $xml -Force | Out-Null

# Vigilante: si el cliente se cierra (cierre manual, fallo del proceso), lo vuelve a
# abrir cada dos minutos. Se ejecuta como SYSTEM y arranca la tarea del cliente.
$watchdogName = "$TaskName" + "Watchdog"
$watchdogXml = @"
<?xml version="1.0" encoding="UTF-16"?>
<Task version="1.4" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
  <RegistrationInfo>
    <Description>Vuelve a abrir el cliente de CyberManager si deja de estar en ejecución.</Description>
    <URI>\$watchdogName</URI>
  </RegistrationInfo>
  <Triggers>
    <BootTrigger>
      <Enabled>true</Enabled>
      <Delay>PT1M</Delay>
    </BootTrigger>
    <TimeTrigger>
      <StartBoundary>2026-01-01T00:00:00</StartBoundary>
      <Enabled>true</Enabled>
      <Repetition>
        <Interval>PT2M</Interval>
        <StopAtDurationEnd>false</StopAtDurationEnd>
      </Repetition>
    </TimeTrigger>
  </Triggers>
  <Principals>
    <Principal id="Author">
      <UserId>S-1-5-18</UserId>
      <RunLevel>HighestAvailable</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>true</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>true</Hidden>
    <RunOnlyIfIdle>false</RunOnlyIfIdle>
    <UseUnifiedSchedulingEngine>true</UseUnifiedSchedulingEngine>
    <ExecutionTimeLimit>PT2M</ExecutionTimeLimit>
    <Priority>7</Priority>
  </Settings>
  <Actions Context="Author">
    <Exec>
      <Command>powershell.exe</Command>
      <Arguments>-NoProfile -NonInteractive -WindowStyle Hidden -Command "if (-not (Get-Process -Name 'CyberManager.Client' -ErrorAction SilentlyContinue)) { Start-ScheduledTask -TaskName '$TaskName' }"</Arguments>
    </Exec>
  </Actions>
</Task>
"@

Write-Step "Registrando el vigilante $watchdogName..."
Register-ScheduledTask -TaskName $watchdogName -Xml $watchdogXml -Force | Out-Null

if ($Start) {
    Write-Step "Iniciando el cliente..."
    Start-ScheduledTask -TaskName $TaskName
    Start-Sleep -Seconds 2
}

$task = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
Write-Host ""
if ($task) {
    Write-Host "Cliente configurado para iniciar con Windows." -ForegroundColor Green
    Write-Host "  Tarea:      $TaskName"
    Write-Host "  Vigilante:  $watchdogName (cada 2 minutos)"
    Write-Host "  Ejecutable: $ClientExe"
    Write-Host "  Permisos:   nivel más alto disponible (administrador)"
}
else {
    Write-Host "No se pudo registrar la tarea $TaskName." -ForegroundColor Yellow
}
Write-Host ""
