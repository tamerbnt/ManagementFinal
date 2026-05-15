Add-Type -AssemblyName System.Drawing

$root      = Split-Path $PSScriptRoot
$assetsDir = Join-Path $root "Management.Presentation\Assets"
$icoPath   = Join-Path $assetsDir "atrium.ico"
$pngPath   = Join-Path $assetsDir "atrium_icon_rounded.png"
$outIco    = Join-Path $assetsDir "atrium_rounded.ico"
$rootIco   = Join-Path $root "assets\app.ico"

# ── Load source ICO ──────────────────────────────────────────────────────────
$ico = New-Object System.Drawing.Icon($icoPath)
$src = $ico.ToBitmap()

# ── Helper: render one size with rounded corners ──────────────────────────────
function Make-RoundedBitmap {
    param([System.Drawing.Image]$source, [int]$sz, [int]$rad)

    $b  = New-Object System.Drawing.Bitmap($sz, $sz, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g2 = [System.Drawing.Graphics]::FromImage($b)
    $g2.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g2.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g2.Clear([System.Drawing.Color]::Transparent)

    $p2 = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p2.AddArc($sz - $rad*2, 0,           $rad*2, $rad*2, 270, 90)
    $p2.AddArc($sz - $rad*2, $sz-$rad*2,  $rad*2, $rad*2, 0,   90)
    $p2.AddArc(0,            $sz-$rad*2,  $rad*2, $rad*2, 90,  90)
    $p2.AddArc(0,            0,           $rad*2, $rad*2, 180, 90)
    $p2.CloseFigure()

    $g2.SetClip($p2)
    $g2.DrawImage($source, 0, 0, $sz, $sz)
    $g2.Dispose()
    return $b
}

# ── Build rounded PNG preview (256px) ────────────────────────────────────────
$preview = Make-RoundedBitmap $src 256 52
$preview.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)
Write-Host "Saved preview PNG: $pngPath"

# ── Build multi-size ICO ─────────────────────────────────────────────────────
$sizes  = @(256, 48, 32, 16)
$radii  = @(52,  10,  6,   3)
$blobs  = @()

foreach ($i in 0..($sizes.Count - 1)) {
    $bmp   = Make-RoundedBitmap $src $sizes[$i] $radii[$i]
    $pngMs = New-Object System.IO.MemoryStream
    $bmp.Save($pngMs, [System.Drawing.Imaging.ImageFormat]::Png)
    $blobs += ,$pngMs.ToArray()
    $bmp.Dispose()
}

$ms     = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($ms)

# ICO header
$writer.Write([uint16]0)              # reserved
$writer.Write([uint16]1)              # type = ICO
$writer.Write([uint16]$sizes.Count)  # image count

# Directory: offset starts after header(6) + 16 bytes per entry
$dataOffset = 6 + 16 * $sizes.Count
foreach ($i in 0..($sizes.Count - 1)) {
    $dim = if ($sizes[$i] -eq 256) { [byte]0 } else { [byte]$sizes[$i] }
    $writer.Write($dim)                          # width  (0 = 256)
    $writer.Write($dim)                          # height (0 = 256)
    $writer.Write([byte]0)                       # colour count
    $writer.Write([byte]0)                       # reserved
    $writer.Write([uint16]1)                     # colour planes
    $writer.Write([uint16]32)                    # bits per pixel
    $writer.Write([uint32]$blobs[$i].Length)     # size of image data
    $writer.Write([uint32]$dataOffset)           # offset to image data
    $dataOffset += $blobs[$i].Length
}

foreach ($blob in $blobs) {
    $writer.Write($blob)
}
$writer.Flush()

$icoBytes = $ms.ToArray()
[System.IO.File]::WriteAllBytes($outIco, $icoBytes)
Write-Host "Saved rounded ICO: $outIco"

# ── Copy to root assets\app.ico ───────────────────────────────────────────────
$rootAssetsDir = Split-Path $rootIco
if (-not (Test-Path $rootAssetsDir)) { New-Item -ItemType Directory -Path $rootAssetsDir | Out-Null }
[System.IO.File]::WriteAllBytes($rootIco, $icoBytes)
Write-Host "Copied to installer ICO: $rootIco"

$src.Dispose()
$preview.Dispose()
Write-Host "Done."
