<#
.SYNOPSIS
    Compila y arranca el servidor de CyberManager y la consola de administración para desarrollo.

.DESCRIPTION
    Compila la solución, inicia el servidor en segundo plano, espera a que responda
    /api/health y abre la consola de administración. El servidor se cierra al pulsar Ctrl+C
    en esta ventana.

.PARAMETER Port
    Puerto HTTP del servidor. Debe coincidir con appsettings.json (5080 por defecto).

.PARAMETER ResetDb
    Elimina la base de datos SQLite antes de arrancar (se vuelve a crear con datos de demostración).

.PARAMETER NoAdmin
    Arranca solo el servidor, sin abrir la consola de administración.

.EXAMPLE
    .\run-dev.ps1
    .\run-dev.ps1 -ResetDb
    .\run-dev.ps1 -NoAdmin -Port 5080
#>
[CmdletBinding()]
param(
    [int]$Port = 5080,
    [switch]$ResetDb,
    [switch]$NoAdmin,
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

function Write-Step([string]$Message) {
    Write-Host "==> $Message" -ForegroundColor Cyan
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$dotnet = Join-Path $env:LOCALAPPDATA "Microsoft\dotnet\dotnet.exe"
if (-not (Test-Path $dotnet)) {
    $command = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($command) { $dotnet = $command.Source }
}
if (-not $dotnet -or -not (Test-Path $dotnet)) {
    throw "No se encontró dotnet.exe. Instale el SDK de .NET 8."
}

$sln = Join-Path $repoRoot "CyberManager.sln"
$serverProject = Join-Path $repoRoot "src\CyberManager.Server\CyberManager.Server.csproj"
$adminProject = Join-Path $repoRoot "src\CyberManager.Admin\CyberManager.Admin.csproj"

# ---------- Base de datos ----------
$dbPath = Join-Path $repoRoot "src\CyberManager.Server\bin\Debug\net8.0\cybermanager.db"
if ($ResetDb -and (Test-Path $dbPath)) {
    Write-Step "Eliminando la base de datos existente..."
    Remove-Item -Path $dbPath -Force
}

# ---------- Compilación ----------
if (-not $SkipBuild) {
    Write-Step "Compilando la solución..."
    & $dotnet build $sln -v q --nologo
    if ($LASTEXITCODE -ne 0) { throw "La compilación falló." }
}

# ---------- Servidor ----------
$logDir = Join-Path $repoRoot "artifacts"
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$serverLog = Join-Path $logDir "server.log"
$serverErr = Join-Path $logDir "server.err.log"

Write-Step "Iniciando el servidor en http://127.0.0.1:$Port ..."
$server = Start-Process -FilePath $dotnet `
    -ArgumentList @("run", "--project", $serverProject, "--no-build", "--", "--Server:Port", $Port, "--no-elevate") `
    -WorkingDirectory $repoRoot `
    -RedirectStandardOutput $serverLog `
    -RedirectStandardError $serverErr `
    -PassThru -WindowStyle Hidden

$healthy = $false
for ($i = 0; $i -lt 60; $i++) {
    if ($server.HasExited) { break }
    try {
        $response = Invoke-RestMethod -Uri "http://127.0.0.1:$Port/api/health" -TimeoutSec 2
        if ($response.status -eq "ok") { $healthy = $true; break }
    } catch {
        Start-Sleep -Milliseconds 500
    }
}

if (-not $healthy) {
    if (-not $server.HasExited) { Stop-Process -Id $server.Id -Force -ErrorAction SilentlyContinue }
    Write-Host ""
    Write-Host "El servidor no respondió. Revise los registros:" -ForegroundColor Red
    Write-Host "  $serverLog"
    Write-Host "  $serverErr"
    if (Test-Path $serverErr) { Get-Content $serverErr -Tail 30 }
    exit 1
}

Write-Host ""
Write-Host "Servidor listo:  http://127.0.0.1:$Port" -ForegroundColor Green
Write-Host "Usuario:         admin / admin123" -ForegroundColor Green
Write-Host "Registro:        $serverLog" -ForegroundColor DarkGray
Write-Host ""

# ---------- Consola de administración ----------
$admin = $null
if (-not $NoAdmin) {
    Write-Step "Abriendo la consola de administración..."
    $admin = Start-Process -FilePath $dotnet `
        -ArgumentList @("run", "--project", $adminProject, "--no-build") `
        -WorkingDirectory $repoRoot `
        -PassThru
}

Write-Host "Pulse Ctrl+C para detener el servidor." -ForegroundColor Yellow

# ---------- Esperar y limpiar ----------
try {
    while (-not $server.HasExited) {
        Start-Sleep -Seconds 1
    }
} finally {
    if ($admin -and -not $admin.HasExited) {
        Stop-Process -Id $admin.Id -Force -ErrorAction SilentlyContinue
    }
    if (-not $server.HasExited) {
        Stop-Process -Id $server.Id -Force -ErrorAction SilentlyContinue
    }
}
