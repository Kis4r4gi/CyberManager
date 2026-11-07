<#
.SYNOPSIS
    Sincroniza automáticamente el proyecto con GitHub.

.DESCRIPTION
    Hace git add de todos los cambios, crea un commit si hay algo nuevo, integra los
    cambios del remoto con rebase y publica la rama. El registro se guarda en
    %LOCALAPPDATA%\CyberManager\logs\github-sync.log para no ensuciar el repositorio.

    Con -InstallTask registra una tarea programada de Windows que ejecuta este script
    cada cierto número de minutos mientras el usuario tenga la sesión iniciada.
    Con -RemoveTask elimina esa tarea.

.PARAMETER Message
    Mensaje de commit. Si se omite se usa «Sincronización automática: fecha y hora».

.PARAMETER Branch
    Rama que se sincroniza. Por defecto main.

.PARAMETER Remote
    Remoto de Git. Por defecto origin.

.PARAMETER TaskMinutes
    Intervalo de la tarea programada, en minutos. Por defecto 15.

.PARAMETER InstallTask
    Registra la tarea programada CyberManagerGitSync.

.PARAMETER RemoveTask
    Elimina la tarea programada CyberManagerGitSync.

.PARAMETER NoPush
    Crea el commit local pero no hace fetch, pull ni push.

.EXAMPLE
    .\scripts\sync-github.ps1

.EXAMPLE
    .\scripts\sync-github.ps1 -Message "Corregir cierre de caja"

.EXAMPLE
    .\scripts\sync-github.ps1 -InstallTask -TaskMinutes 15
#>
[CmdletBinding()]
param(
    [string]$Message,
    [string]$Branch = "main",
    [string]$Remote = "origin",
    [ValidateRange(5, 1440)]
    [int]$TaskMinutes = 15,
    [switch]$InstallTask,
    [switch]$RemoveTask,
    [switch]$NoPush,
    [switch]$Quiet
)

$ErrorActionPreference = "Stop"

$taskName = "CyberManagerGitSync"
$repoRoot = Split-Path -Parent $PSScriptRoot
$logDir = Join-Path $env:LOCALAPPDATA "CyberManager\logs"
$logFile = Join-Path $logDir "github-sync.log"

# Las herramientas portátiles no siempre heredan el PATH del usuario en tareas programadas.
$portableTools = Join-Path $env:LOCALAPPDATA "Programs\cybermanager-tools"
$portableGit = Join-Path $portableTools "PortableGit\cmd\git.exe"
$portableGh = Join-Path $portableTools "GitHubCLI\bin"
if (Test-Path $portableGit) {
    $env:Path = "$(Split-Path $portableGit -Parent);$portableGh;$env:Path"
}

function Write-Log([string]$Text, [string]$Color = "Gray") {
    $line = "[{0}] {1}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"), $Text
    try {
        if (-not (Test-Path $logDir)) { New-Item -ItemType Directory -Path $logDir -Force | Out-Null }
        Add-Content -Path $logFile -Value $line -Encoding UTF8
    } catch { }
    if (-not $Quiet) { Write-Host $line -ForegroundColor $Color }
}

function Find-Git {
    if (Test-Path $portableGit) { return $portableGit }
    $command = Get-Command git -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }
    throw "No se encontró git.exe. Instale Git o use las herramientas portátiles de CyberManager."
}

function Remove-SyncTask {
    & schtasks.exe /Delete /TN $taskName /F 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) {
        Write-Log "Tarea programada $taskName eliminada." "Yellow"
    } else {
        Write-Log "La tarea programada $taskName no existe o no se pudo eliminar." "Yellow"
    }
}

function Install-SyncTask {
    $hostExe = (Get-Command pwsh -ErrorAction SilentlyContinue).Source
    if (-not $hostExe) {
        $hostExe = Join-Path $env:SystemRoot "System32\WindowsPowerShell\v1.0\powershell.exe"
    }

    $action = "`"$hostExe`" -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$PSCommandPath`" -Quiet"
    & schtasks.exe /Create /TN $taskName /TR $action /SC MINUTE /MO $TaskMinutes /F 2>&1 | Out-Null

    if ($LASTEXITCODE -ne 0) {
        throw "No se pudo registrar la tarea programada $taskName."
    }

    Write-Log "Tarea programada $taskName registrada cada $TaskMinutes minutos." "Green"
    Write-Log "Se ejecuta con la sesión del usuario y requiere que GitHub CLI siga autenticado." "Gray"
}

if ($RemoveTask) {
    Remove-SyncTask
    return
}

if ($InstallTask) {
    Install-SyncTask
    return
}

$git = Find-Git

Push-Location $repoRoot
try {
    & $git rev-parse --is-inside-work-tree 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "$repoRoot no es un repositorio Git." }

    & $git add -A
    & $git diff --cached --quiet
    if ($LASTEXITCODE -eq 0) {
        Write-Log "Sin cambios pendientes; no se creó ningún commit."
        return
    }

    $commitMessage = if ([string]::IsNullOrWhiteSpace($Message)) {
        "Sincronización automática: $(Get-Date -Format 'yyyy-MM-dd HH:mm')"
    } else {
        $Message.Trim()
    }

    & $git commit -m $commitMessage 2>&1 | ForEach-Object { Write-Log $_ }
    if ($LASTEXITCODE -ne 0) { throw "git commit falló." }

    if ($NoPush) {
        Write-Log "Commit local creado ($commitMessage); no se publicó por -NoPush." "Yellow"
        return
    }

    & $git fetch $Remote $Branch 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) {
        & $git pull --rebase --autostash $Remote $Branch 2>&1 | ForEach-Object { Write-Log $_ }
        if ($LASTEXITCODE -ne 0) {
            & $git rebase --abort 2>&1 | Out-Null
            throw "No se pudieron integrar los cambios del remoto. Resuelva el conflicto manualmente y vuelva a ejecutar el script."
        }
    } else {
        Write-Log "No se pudo consultar $Remote/$Branch; se intentará publicar igualmente." "Yellow"
    }

    & $git push $Remote $Branch 2>&1 | ForEach-Object { Write-Log $_ }
    if ($LASTEXITCODE -ne 0) { throw "git push falló." }

    Write-Log "Sincronizado con $Remote/$Branch ($commitMessage)." "Green"
}
catch {
    Write-Log $_.Exception.Message "Red"
    exit 1
}
finally {
    Pop-Location
}
