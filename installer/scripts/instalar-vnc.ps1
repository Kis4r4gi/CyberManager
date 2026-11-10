#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Instala, configura o desinstala TightVNC como componente opcional de CyberManager.

.DESCRIPTION
    Se ejecuta desde el instalador de estación (servidor VNC) y desde el instalador del
    servidor (visor VNC). Instala TightVNC en modo silencioso con MSI, registra el servicio,
    aplica la contraseña y el puerto y crea una regla de Firewall limitada a la red local.

    La desinstalación solo quita TightVNC si lo instaló CyberManager. Si el equipo ya tenía
    TightVNC antes, o si el otro componente de CyberManager todavía lo usa, se conserva.

.PARAMETER Action
    Install o Uninstall.

.PARAMETER MsiPath
    Ruta del MSI de TightVNC copiado por el instalador.

.PARAMETER PasswordFile
    Archivo temporal con la contraseña VNC (máximo 8 caracteres). Solo para el servidor VNC.

.PARAMETER Port
    Puerto RFB de TightVNC. Por defecto 5900.

.PARAMETER Component
    Componente de CyberManager que instala o libera TightVNC: estacion o servidor.

.PARAMETER Mode
    Server instala el servidor VNC; Viewer instala solo el visor; Both instala ambos.

.NOTES
    TightVNC 2.8.90 se distribuye como programa separado bajo GPLv2. Consulte
    THIRD-PARTY-NOTICES.md y LICENSE-TightVNC-GPLv2.txt.
#>
[CmdletBinding()]
param(
    [ValidateSet("Install", "Uninstall")]
    [string]$Action = "Install",

    [string]$MsiPath = "",

    [string]$PasswordFile = "",

    [int]$Port = 5900,

    [ValidateSet("estacion", "servidor")]
    [string]$Component = "estacion",

    [ValidateSet("Server", "Viewer", "Both")]
    [string]$Mode = "Server"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$vncDir = Join-Path $root "vnc"
$logPath = Join-Path $vncDir "instalacion.log"
$msiLog = Join-Path $vncDir "msiexec.log"
$markerDir = Join-Path $env:ProgramData "CyberManager"
$markerPath = Join-Path $markerDir "vnc-componentes.txt"
$firewallName = "CyberManager VNC (TightVNC)"

New-Item -ItemType Directory -Force -Path $vncDir, $markerDir | Out-Null

function Write-Log([string]$Message) {
    $line = "[{0:yyyy-MM-dd HH:mm:ss}] {1}" -f (Get-Date), $Message
    Write-Host $line
    Add-Content -Path $logPath -Value $line -Encoding UTF8 -ErrorAction SilentlyContinue
}

function Get-TightVncServerExe {
    $candidates = @(
        (Join-Path $env:ProgramFiles "TightVNC\tvnserver.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "TightVNC\tvnserver.exe")
    )
    foreach ($path in $candidates) {
        if ($path -and (Test-Path $path)) { return $path }
    }
    return $null
}

function Get-TightVncViewerExe {
    $candidates = @(
        (Join-Path $env:ProgramFiles "TightVNC\tvnviewer.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "TightVNC\tvnviewer.exe")
    )
    foreach ($path in $candidates) {
        if ($path -and (Test-Path $path)) { return $path }
    }
    return $null
}

function Test-TightVncServerInstalled {
    if (Get-Service -Name "tvnserver" -ErrorAction SilentlyContinue) { return $true }
    if (Test-Path "HKLM:\SOFTWARE\TightVNC\Server") { return $true }
    if (Test-Path "HKLM:\SOFTWARE\WOW6432Node\TightVNC\Server") { return $true }
    return $false
}

function Get-MarkerComponents {
    if (-not (Test-Path $markerPath)) { return @() }
    return @(Get-Content -Path $markerPath -ErrorAction SilentlyContinue |
        ForEach-Object { $_.Trim() } |
        Where-Object { $_ })
}

function Add-MarkerComponent([string]$Name) {
    $items = @(Get-MarkerComponents)
    if ($items -notcontains $Name) {
        $items = @($items + $Name | Sort-Object -Unique)
        Set-Content -Path $markerPath -Value $items -Encoding UTF8
    }
}

function Remove-MarkerComponent([string]$Name) {
    if (-not (Test-Path $markerPath)) { return 0 }
    $items = @(Get-MarkerComponents | Where-Object { $_ -ne $Name })
    if ($items.Count -eq 0) {
        Remove-Item -Path $markerPath -Force -ErrorAction SilentlyContinue
    }
    else {
        Set-Content -Path $markerPath -Value $items -Encoding UTF8
    }
    return $items.Count
}

function Test-AlreadyInstalled {
    $server = Test-TightVncServerInstalled
    $viewer = [bool](Get-TightVncViewerExe)

    switch ($Mode) {
        "Server" { return $server }
        "Viewer" { return $viewer }
        "Both"   { return ($server -and $viewer) }
    }
    return $false
}

function Install-TightVnc {
    if (Test-AlreadyInstalled) {
        Write-Log "TightVNC ya está instalado en este equipo. No se modifica su configuración."
        if (Test-Path $markerPath) { Add-MarkerComponent $Component }
        return 2
    }

    if (-not (Test-Path $MsiPath)) {
        Write-Log "No se encontró el MSI de TightVNC en: $MsiPath"
        return 1
    }

    $password = ""
    if ($Mode -eq "Server" -or $Mode -eq "Both") {
        if ([string]::IsNullOrWhiteSpace($PasswordFile) -or -not (Test-Path $PasswordFile)) {
            Write-Log "No se recibió la contraseña VNC."
            return 1
        }

        $password = (Get-Content -Path $PasswordFile -Raw -ErrorAction Stop).Trim()
        if ($password.Length -lt 4 -or $password.Length -gt 8) {
            Write-Log "La contraseña VNC debe tener entre 4 y 8 caracteres."
            return 1
        }

        if ($password -match '[\s"]') {
            Write-Log "La contraseña VNC no puede contener espacios ni comillas."
            return 1
        }

        if ($Port -lt 1024 -or $Port -gt 65535) {
            Write-Log "El puerto VNC no es válido: $Port"
            return 1
        }
    }

    $arguments = @(
        "/i", "`"$MsiPath`"",
        "/qn", "/norestart",
        "/l*v", "`"$msiLog`""
    )

    switch ($Mode) {
        "Server" {
            $arguments += @(
                "ADDLOCAL=Server",
                "SERVER_REGISTER_AS_SERVICE=1",
                "SERVER_SERVICE_ONLY=1",
                "SERVER_ALLOW_SAS=1",
                "SERVER_ADD_FIREWALL_EXCEPTION=0",
                "VIEWER_ADD_FIREWALL_EXCEPTION=0",
                "SET_USEVNCAUTHENTICATION=1",
                "VALUE_OF_USEVNCAUTHENTICATION=1",
                "SET_PASSWORD=1",
                "VALUE_OF_PASSWORD=$password",
                "SET_RFBPORT=1",
                "VALUE_OF_RFBPORT=$Port",
                "SET_ACCEPTRFBCONNECTIONS=1",
                "VALUE_OF_ACCEPTRFBCONNECTIONS=1",
                "SET_ACCEPTHTTPCONNECTIONS=0"
            )
        }
        "Viewer" {
            $arguments += @(
                "ADDLOCAL=Viewer",
                "VIEWER_ASSOCIATE_VNC_EXTENSION=0",
                "VIEWER_ADD_FIREWALL_EXCEPTION=0",
                "SERVER_ADD_FIREWALL_EXCEPTION=0"
            )
        }
        "Both" {
            $arguments += @(
                "ADDLOCAL=Server,Viewer",
                "SERVER_REGISTER_AS_SERVICE=1",
                "SERVER_SERVICE_ONLY=1",
                "SERVER_ALLOW_SAS=1",
                "SERVER_ADD_FIREWALL_EXCEPTION=0",
                "VIEWER_ADD_FIREWALL_EXCEPTION=0",
                "VIEWER_ASSOCIATE_VNC_EXTENSION=0",
                "SET_USEVNCAUTHENTICATION=1",
                "VALUE_OF_USEVNCAUTHENTICATION=1",
                "SET_PASSWORD=1",
                "VALUE_OF_PASSWORD=$password",
                "SET_RFBPORT=1",
                "VALUE_OF_RFBPORT=$Port",
                "SET_ACCEPTRFBCONNECTIONS=1",
                "VALUE_OF_ACCEPTRFBCONNECTIONS=1",
                "SET_ACCEPTHTTPCONNECTIONS=0"
            )
        }
    }

    Write-Log "Instalando TightVNC ($Mode)..."
    $process = Start-Process -FilePath "msiexec.exe" -ArgumentList $arguments -Wait -PassThru
    if ($process.ExitCode -ne 0 -and $process.ExitCode -ne 3010) {
        Write-Log "msiexec terminó con el código $($process.ExitCode). Revise $msiLog"
        return 1
    }

    if ($Mode -eq "Server" -or $Mode -eq "Both") {
        $serverExe = Get-TightVncServerExe
        if (-not $serverExe) {
            Write-Log "No se encontró tvnserver.exe después de la instalación."
            return 1
        }

        Start-Sleep -Seconds 2
        $service = Get-Service -Name "tvnserver" -ErrorAction SilentlyContinue
        if (-not $service) {
            Write-Log "El servicio tvnserver no existe; registrándolo manualmente..."
            Start-Process -FilePath $serverExe -ArgumentList "-install", "-silent" -Wait -NoNewWindow
            Start-Sleep -Seconds 2
            $service = Get-Service -Name "tvnserver" -ErrorAction SilentlyContinue
        }

        if (-not $service) {
            Write-Log "No se pudo registrar el servicio tvnserver."
            return 1
        }

        Set-Service -Name "tvnserver" -StartupType Automatic -ErrorAction SilentlyContinue
        if ($service.Status -ne "Running") {
            Start-Service -Name "tvnserver" -ErrorAction SilentlyContinue
            Start-Sleep -Seconds 2
            $service = Get-Service -Name "tvnserver" -ErrorAction SilentlyContinue
        }

        if (-not $service -or $service.Status -ne "Running") {
            Write-Log "El servicio tvnserver no está en ejecución. Revise el Visor de eventos."
            return 1
        }

        # Firewall: solo red local, nunca Internet. Se reemplaza cualquier regla anterior.
        Get-NetFirewallRule -DisplayName "CyberManager VNC*" -ErrorAction SilentlyContinue |
            Remove-NetFirewallRule -ErrorAction SilentlyContinue
        New-NetFirewallRule `
            -DisplayName $firewallName `
            -Direction Inbound `
            -Protocol TCP `
            -LocalPort $Port `
            -Action Allow `
            -Profile Domain,Private `
            -RemoteAddress LocalSubnet `
            -Description "Permite VNC (TightVNC) solo desde la red local. Instalado por CyberManager." | Out-Null

        Write-Log "Servidor VNC activo en el puerto $Port, limitado a la red local."
    }

    if ($Mode -eq "Viewer" -or $Mode -eq "Both") {
        $viewerExe = Get-TightVncViewerExe
        if (-not $viewerExe) {
            Write-Log "No se encontró tvnviewer.exe después de la instalación."
            return 1
        }
        Write-Log "Visor VNC instalado en: $viewerExe"
    }

    Add-MarkerComponent $Component
    Write-Log "TightVNC instalado correctamente. Componente: $Component"
    return 0
}

function Uninstall-TightVnc {
    if (-not (Test-Path $markerPath)) {
        Write-Log "TightVNC no fue instalado por CyberManager; no se desinstala."
        return 0
    }

    $remaining = Remove-MarkerComponent $Component
    if ($remaining -gt 0) {
        Write-Log "Otro componente de CyberManager todavía usa TightVNC; se conserva."
        return 0
    }

    Write-Log "Desinstalando TightVNC..."

    $service = Get-Service -Name "tvnserver" -ErrorAction SilentlyContinue
    if ($service) {
        if ($service.Status -ne "Stopped") {
            Stop-Service -Name "tvnserver" -Force -ErrorAction SilentlyContinue
            try { $service.WaitForStatus("Stopped", [TimeSpan]::FromSeconds(15)) } catch { }
        }
    }

    $uninstalled = $false
    if (-not [string]::IsNullOrWhiteSpace($MsiPath) -and (Test-Path $MsiPath)) {
        $arguments = @("/x", "`"$MsiPath`"", "/qn", "/norestart", "/l*v", "`"$msiLog`"")
        $process = Start-Process -FilePath "msiexec.exe" -ArgumentList $arguments -Wait -PassThru
        $uninstalled = ($process.ExitCode -eq 0 -or $process.ExitCode -eq 3010)
    }

    if (-not $uninstalled) {
        $entries = @()
        $entries += Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*" -ErrorAction SilentlyContinue
        $entries += Get-ItemProperty "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*" -ErrorAction SilentlyContinue
        $entry = $entries | Where-Object { $_.DisplayName -like "TightVNC*" } | Select-Object -First 1

        if ($entry -and $entry.UninstallString -match "\{[0-9A-Fa-f-]+\}") {
            $productCode = $Matches[0]
            $process = Start-Process -FilePath "msiexec.exe" `
                -ArgumentList @("/x", $productCode, "/qn", "/norestart") -Wait -PassThru
            $uninstalled = ($process.ExitCode -eq 0 -or $process.ExitCode -eq 3010)
        }
    }

    Get-NetFirewallRule -DisplayName "CyberManager VNC*" -ErrorAction SilentlyContinue |
        Remove-NetFirewallRule -ErrorAction SilentlyContinue

    Remove-Item -Path $markerPath -Force -ErrorAction SilentlyContinue
    Write-Log ("TightVNC desinstalado: {0}" -f $uninstalled)
    return 0
}

$result = 1
try {
    if ($Action -eq "Uninstall") {
        $result = Uninstall-TightVnc
    }
    else {
        $result = Install-TightVnc
    }
}
catch {
    Write-Log "Error: $($_.Exception.Message)"
    $result = 1
}
finally {
    if (-not [string]::IsNullOrWhiteSpace($PasswordFile) -and (Test-Path $PasswordFile)) {
        Remove-Item -Path $PasswordFile -Force -ErrorAction SilentlyContinue
    }
}

exit $result
