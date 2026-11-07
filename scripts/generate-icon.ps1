<#
.SYNOPSIS
    Genera el icono compartido de CyberManager en assets\CyberManager.ico.

.DESCRIPTION
    Dibuja un icono vectorial (fondo redondeado azul oscuro con una «C» cian) en varios
    tamaños (16 a 256 px) y lo empaqueta como un .ico con entradas BMP de 32 bits.
    Ese formato es compatible con los ejecutables de .NET y con Inno Setup.

.EXAMPLE
    pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\generate-icon.ps1
#>
[CmdletBinding()]
param(
    [string]$OutputPath = (Join-Path (Split-Path -Parent $PSScriptRoot) "assets\CyberManager.ico"),
    [string]$PreviewDir = (Join-Path $env:TEMP "cybermanager-icon")
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing.Common

function New-RoundedPath([single]$X, [single]$Y, [single]$Width, [single]$Height, [single]$Radius) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $diameter = $Radius * 2
    $path.AddArc($X, $Y, $diameter, $diameter, 180, 90)
    $path.AddArc($X + $Width - $diameter, $Y, $diameter, $diameter, 270, 90)
    $path.AddArc($X + $Width - $diameter, $Y + $Height - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($X, $Y + $Height - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-IconBitmap([int]$Size) {
    $scale = 4
    $s = [single]($Size * $scale)

    $big = New-Object System.Drawing.Bitmap ([int]$s), ([int]$s), ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($big)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    $bg1 = [System.Drawing.ColorTranslator]::FromHtml("#0B1220")
    $bg2 = [System.Drawing.ColorTranslator]::FromHtml("#17395A")
    $cyan = [System.Drawing.ColorTranslator]::FromHtml("#22D3EE")
    $white = [System.Drawing.ColorTranslator]::FromHtml("#E8EFFA")

    $margin = [single]($s * 0.055)
    $side = [single]($s - $margin * 2)
    $radius = [single]($s * 0.22)
    $rect = New-Object System.Drawing.RectangleF $margin, $margin, $side, $side
    $shape = New-RoundedPath $margin $margin $side $side $radius

    $gradient = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, $bg1, $bg2, 45.0
    $g.FillPath($gradient, $shape)

    $border = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(150, $cyan.R, $cyan.G, $cyan.B)), ([single]($s * 0.018))
    $g.DrawPath($border, $shape)

    # Aro de la «C» con un resplandor suave y extremos redondeados.
    $inset = [single]($s * 0.265)
    $arcSide = [single]($s - $inset * 2)
    $arcRect = New-Object System.Drawing.RectangleF $inset, $inset, $arcSide, $arcSide

    $glow = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(70, $cyan.R, $cyan.G, $cyan.B)), ([single]($s * 0.165))
    $glow.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $glow.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawArc($glow, $arcRect, 42.0, 276.0)

    $arc = New-Object System.Drawing.Pen $cyan, ([single]($s * 0.11))
    $arc.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $arc.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawArc($arc, $arcRect, 42.0, 276.0)

    # Nodo luminoso en la abertura de la «C».
    $dotRadius = [single]($s * 0.06)
    $dotX = [single]($s * 0.752)
    $dotY = [single]($s * 0.5)
    $dotGlow = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(80, $cyan.R, $cyan.G, $cyan.B))
    $g.FillEllipse($dotGlow, $dotX - $dotRadius * 1.8, $dotY - $dotRadius * 1.8, $dotRadius * 3.6, $dotRadius * 3.6)
    $dot = New-Object System.Drawing.SolidBrush $white
    $g.FillEllipse($dot, $dotX - $dotRadius, $dotY - $dotRadius, $dotRadius * 2, $dotRadius * 2)

    $dot.Dispose(); $dotGlow.Dispose()
    $arc.Dispose(); $glow.Dispose(); $border.Dispose(); $gradient.Dispose(); $shape.Dispose()
    $g.Dispose()

    $final = New-Object System.Drawing.Bitmap $Size, $Size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $fg = [System.Drawing.Graphics]::FromImage($final)
    $fg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $fg.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $fg.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $fg.DrawImage($big, 0, 0, $Size, $Size)
    $fg.Dispose()
    $big.Dispose()

    return $final
}

function Get-IconEntryBytes([System.Drawing.Bitmap]$Bitmap) {
    $w = $Bitmap.Width
    $h = $Bitmap.Height
    $rect = New-Object System.Drawing.Rectangle 0, 0, $w, $h
    $locked = $Bitmap.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $stride = $locked.Stride
        $pixels = New-Object byte[] ($stride * $h)
        [System.Runtime.InteropServices.Marshal]::Copy($locked.Scan0, $pixels, 0, $pixels.Length)
    }
    finally {
        $Bitmap.UnlockBits($locked)
    }

    $stream = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter $stream

    # BITMAPINFOHEADER: altura doble porque el formato ICO incluye la máscara AND.
    $writer.Write([int]40)
    $writer.Write([int]$w)
    $writer.Write([int]($h * 2))
    $writer.Write([int16]1)
    $writer.Write([int16]32)
    $writer.Write([int]0)
    $writer.Write([int]($w * $h * 4))
    $writer.Write([int]0)
    $writer.Write([int]0)
    $writer.Write([int]0)
    $writer.Write([int]0)

    for ($y = $h - 1; $y -ge 0; $y--) {
        $writer.Write($pixels, $y * $stride, $w * 4)
    }

    $maskStride = [int]([Math]::Floor(($w + 31) / 32) * 4)
    $mask = New-Object byte[] ($maskStride * $h)
    $writer.Write($mask, 0, $mask.Length)
    $writer.Flush()

    $bytes = $stream.ToArray()
    $writer.Dispose()
    $stream.Dispose()
    return ,$bytes
}

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$entries = @{}

foreach ($size in $sizes) {
    $bitmap = New-IconBitmap $size
    try {
        $entries[$size] = Get-IconEntryBytes $bitmap
    }
    finally {
        $bitmap.Dispose()
    }
}

$outputDir = Split-Path -Parent $OutputPath
if (-not (Test-Path $outputDir)) { New-Item -ItemType Directory -Force -Path $outputDir | Out-Null }

$stream = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter $stream
$writer.Write([int16]0)
$writer.Write([int16]1)
$writer.Write([int16]$sizes.Count)

$offset = 6 + 16 * $sizes.Count
foreach ($size in $sizes) {
    $dimension = if ($size -ge 256) { 0 } else { $size }
    $writer.Write([byte]$dimension)
    $writer.Write([byte]$dimension)
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([int16]1)
    $writer.Write([int16]32)
    $writer.Write([int]$entries[$size].Length)
    $writer.Write([int]$offset)
    $offset += $entries[$size].Length
}

foreach ($size in $sizes) {
    $writer.Write($entries[$size], 0, $entries[$size].Length)
}

$writer.Flush()
[System.IO.File]::WriteAllBytes($OutputPath, $stream.ToArray())
$writer.Dispose()
$stream.Dispose()

if (-not (Test-Path $PreviewDir)) { New-Item -ItemType Directory -Force -Path $PreviewDir | Out-Null }
foreach ($size in @(32, 256)) {
    $preview = New-IconBitmap $size
    try { $preview.Save((Join-Path $PreviewDir "icon-$size.png"), [System.Drawing.Imaging.ImageFormat]::Png) }
    finally { $preview.Dispose() }
}

$info = Get-Item $OutputPath
Write-Host "Icono generado: $($info.FullName) ($([Math]::Round($info.Length / 1KB, 1)) KB)" -ForegroundColor Green
Write-Host "Vistas previas: $PreviewDir" -ForegroundColor DarkGray
