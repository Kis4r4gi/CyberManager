<#
.SYNOPSIS
    Descarga los instaladores MSI de TightVNC usados por los instaladores de CyberManager.

.DESCRIPTION
    TightVNC es un componente opcional que se distribuye como programa separado bajo GPLv2.
    Los MSI no se guardan en el repositorio: este script los descarga en installer\vnc y
    verifica su tamaño y su hash SHA-256 antes de usarlos. build-installer.ps1 lo llama
    automáticamente al generar los instaladores.

.PARAMETER Destination
    Carpeta donde se guardan los MSI. Por defecto installer\vnc.

.PARAMETER IncludeSource
    También descarga el código fuente GPL de TightVNC para adjuntarlo a la release.

.PARAMETER Force
    Vuelve a descargar aunque los archivos ya existan y coincidan.
#>
[CmdletBinding()]
param(
    [string]$Destination,
    [switch]$IncludeSource,
    [switch]$Force
)

$ErrorActionPreference = "Stop"

function Write-Step([string]$Message) {
    Write-Host "==> $Message" -ForegroundColor Cyan
}

if ([string]::IsNullOrWhiteSpace($Destination)) {
    $Destination = Join-Path (Split-Path -Parent $PSScriptRoot) "installer\vnc"
}

$files = @(
    @{
        Name   = "tightvnc-2.8.90-gpl-setup-64bit.msi"
        Url    = "https://www.tightvnc.com/download/2.8.90/tightvnc-2.8.90-gpl-setup-64bit.msi"
        Sha256 = "9693CB4CDC2E7E482E8E3F5890415B57EF87DE3369FA5C337DB516218A1170CE"
        Size   = 2543616
    },
    @{
        Name   = "tightvnc-2.8.90-gpl-setup-32bit.msi"
        Url    = "https://www.tightvnc.com/download/2.8.90/tightvnc-2.8.90-gpl-setup-32bit.msi"
        Sha256 = "669BCF5796C2655FB1A8B556CAD8DECF25D3008A650EF495B1ED3882AA918211"
        Size   = 2113536
    }
)

if ($IncludeSource) {
    $files += @{
        Name   = "tightvnc-2.8.90-src-gpl.zip"
        Url    = "https://www.tightvnc.com/download/2.8.90/tightvnc-2.8.90-src-gpl.zip"
        Sha256 = "F3F76393A5865E1F1919C78C3EB43B9C937CF738927C2BFC920ECBD3913B061C"
        Size   = 3018194
    }
}

New-Item -ItemType Directory -Force -Path $Destination | Out-Null

foreach ($file in $files) {
    $path = Join-Path $Destination $file.Name
    $valid = $false

    if ((Test-Path $path) -and -not $Force) {
        $item = Get-Item $path
        if ($item.Length -eq $file.Size) {
            $hash = (Get-FileHash -Path $path -Algorithm SHA256).Hash
            $valid = $hash -eq $file.Sha256
        }
    }

    if (-not $valid) {
        Write-Step "Descargando $($file.Name)..."
        $temp = "$path.download"
        try {
            Invoke-WebRequest -Uri $file.Url -OutFile $temp -UseBasicParsing
            $item = Get-Item $temp
            if ($item.Length -ne $file.Size) {
                throw "Tamaño inesperado: $($item.Length) bytes (se esperaban $($file.Size))."
            }

            $hash = (Get-FileHash -Path $temp -Algorithm SHA256).Hash
            if ($hash -ne $file.Sha256) {
                throw "Hash SHA-256 inesperado: $hash (se esperaba $($file.Sha256))."
            }

            Move-Item -Path $temp -Destination $path -Force
        }
        finally {
            Remove-Item -Path $temp -Force -ErrorAction SilentlyContinue
        }
    }

    $item = Get-Item $path
    Write-Host ("  {0} ({1:N0} bytes, SHA-256 verificado)" -f $item.Name, $item.Length) -ForegroundColor Green
}

Write-Host ""
Write-Host "TightVNC listo en: $Destination" -ForegroundColor Green
