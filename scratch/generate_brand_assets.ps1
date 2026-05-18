Add-Type -AssemblyName System.Drawing

$srcPath = "C:\Users\techbox\.gemini\antigravity\brain\e0724ef8-a2c4-4aa5-be44-f63b8fdf9e37\media__1779084444720.png"
$root = "c:\Users\techbox\.gemini\antigravity\ManagementBackup1234"
$presentationAssetsDir = Join-Path $root "Management.Presentation\Assets"
$outIcoPath = Join-Path $presentationAssetsDir "atrium_rounded.ico"
$outPngPath = Join-Path $presentationAssetsDir "atrium_icon_rounded.png"
$rootIcoPath = Join-Path $root "assets\app.ico"

Write-Host "Source: $srcPath"
Write-Host "Presentation Assets: $presentationAssetsDir"
Write-Host "Output ICO: $outIcoPath"
Write-Host "Output PNG: $outPngPath"
Write-Host "Root ICO (installer): $rootIcoPath"

# 1. Load source image
$img = [System.Drawing.Image]::FromFile($srcPath)
$bmp = New-Object System.Drawing.Bitmap($img)

# 2. Crop the logo mark tightly
# Bounds: X=418 to 604 (width=186), Y=162 to 343 (height=181)
$cropRect = New-Object System.Drawing.Rectangle(418, 162, 186, 181)
$croppedBmp = $bmp.Clone($cropRect, $bmp.PixelFormat)
Write-Host "Tightly cropped logo mark: size = $($croppedBmp.Width)x$($croppedBmp.Height)"

# 3. Helper: Generate rounded squircle icon of size $sz with background #161716 and logo scaled to 65% width
function Make-RoundedPremiumIcon {
    param([System.Drawing.Bitmap]$croppedEmblem, [int]$sz, [int]$rad)
    
    # Target color: #161716
    $bgColor = [System.Drawing.Color]::FromArgb(22, 23, 22)
    
    # Create the base flat canvas with the solid background and emblem centered
    $flatBmp = New-Object System.Drawing.Bitmap($sz, $sz, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $gFlat = [System.Drawing.Graphics]::FromImage($flatBmp)
    $gFlat.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $gFlat.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    
    # Fill solid background
    $brush = New-Object System.Drawing.SolidBrush($bgColor)
    $gFlat.FillRectangle($brush, 0, 0, $sz, $sz)
    $brush.Dispose()
    
    # Scale emblem to 65% of the canvas width
    $targetWidth = [int]($sz * 0.65)
    $targetHeight = [int]($targetWidth * ($croppedEmblem.Height / $croppedEmblem.Width))
    
    $destX = [int](($sz - $targetWidth) / 2)
    $destY = [int](($sz - $targetHeight) / 2)
    
    $gFlat.DrawImage($croppedEmblem, $destX, $destY, $targetWidth, $targetHeight)
    $gFlat.Dispose()
    
    # Create a new transparent canvas to draw the rounded clipped version
    $roundedBmp = New-Object System.Drawing.Bitmap($sz, $sz, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $gRound = [System.Drawing.Graphics]::FromImage($roundedBmp)
    $gRound.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $gRound.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $gRound.Clear([System.Drawing.Color]::Transparent)
    
    # Create rounded corner path
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.AddArc($sz - $rad*2, 0,           $rad*2, $rad*2, 270, 90)
    $p.AddArc($sz - $rad*2, $sz-$rad*2,  $rad*2, $rad*2, 0,   90)
    $p.AddArc(0,            $sz-$rad*2,  $rad*2, $rad*2, 90,  90)
    $p.AddArc(0,            0,           $rad*2, $rad*2, 180, 90)
    $p.CloseFigure()
    
    # Clip and draw the flat colored canvas onto the rounded canvas
    $gRound.SetClip($p)
    $gRound.DrawImage($flatBmp, 0, 0, $sz, $sz)
    
    $p.Dispose()
    $gRound.Dispose()
    $flatBmp.Dispose()
    
    return $roundedBmp
}

# 4. Generate 256px rounded PNG companion preview
Write-Host "Generating 256px preview PNG..."
$previewBmp = Make-RoundedPremiumIcon -croppedEmblem $croppedBmp -sz 256 -rad 52
$previewBmp.Save($outPngPath, [System.Drawing.Imaging.ImageFormat]::Png)
Write-Host "Saved companion PNG preview to $outPngPath"

# 5. Compile multi-size rounded ICO
$sizes = @(256, 48, 32, 16)
$radii = @(52,  10,  6,   3)
$blobs = @()

Write-Host "Generating multi-size ICO binary streams..."
foreach ($i in 0..($sizes.Count - 1)) {
    $size = $sizes[$i]
    $rad = $radii[$i]
    Write-Host "  - Processing size ${size}x${size} (radius ${rad}px)..."
    $resBmp = Make-RoundedPremiumIcon -croppedEmblem $croppedBmp -sz $size -rad $rad
    
    $pngMs = New-Object System.IO.MemoryStream
    $resBmp.Save($pngMs, [System.Drawing.Imaging.ImageFormat]::Png)
    $blobs += ,$pngMs.ToArray()
    
    $pngMs.Dispose()
    $resBmp.Dispose()
}

$ms = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($ms)

# Write ICO File Header
$writer.Write([uint16]0)              # Reserved
$writer.Write([uint16]1)              # Type (1 = ICO)
$writer.Write([uint16]$sizes.Count)  # Image Count

# Write ICO Directory Entries
$dataOffset = 6 + 16 * $sizes.Count
foreach ($i in 0..($sizes.Count - 1)) {
    $dim = if ($sizes[$i] -eq 256) { [byte]0 } else { [byte]$sizes[$i] }
    $writer.Write($dim)                          # Width
    $writer.Write($dim)                          # Height
    $writer.Write([byte]0)                       # Color count
    $writer.Write([byte]0)                       # Reserved
    $writer.Write([uint16]1)                     # Color planes
    $writer.Write([uint16]32)                    # Bits per pixel
    $writer.Write([uint32]$blobs[$i].Length)     # Size of image data
    $writer.Write([uint32]$dataOffset)           # Offset to image data
    $dataOffset += $blobs[$i].Length
}

# Write Image Data Blobs
foreach ($blob in $blobs) {
    $writer.Write($blob)
}
$writer.Flush()

# Save ICO to Presentation Assets
$icoBytes = $ms.ToArray()
[System.IO.File]::WriteAllBytes($outIcoPath, $icoBytes)
Write-Host "Saved compiled multi-size ICO to $outIcoPath"

# Save ICO to root assets\app.ico (installer)
$rootAssetsDir = Split-Path $rootIcoPath
if (-not (Test-Path $rootAssetsDir)) { New-Item -ItemType Directory -Path $rootAssetsDir | Out-Null }
[System.IO.File]::WriteAllBytes($rootIcoPath, $icoBytes)
Write-Host "Copied compiled ICO to Installer Asset: $rootIcoPath"

# Cleanup
$croppedBmp.Dispose()
$bmp.Dispose()
$img.Dispose()
$previewBmp.Dispose()
$ms.Dispose()

Write-Host "Brand asset replacement pipeline completed successfully!"
