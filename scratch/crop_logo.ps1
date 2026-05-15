Add-Type -AssemblyName System.Drawing
$img = [System.Drawing.Image]::FromFile("c:\Users\techbox\.gemini\antigravity\ManagementBackup1234\Management.Presentation\Assets\atrium_header_logo_transparent.png")
$bmp = New-Object System.Drawing.Bitmap($img)
$width = $bmp.Width
$height = $bmp.Height

$minY = $height; $maxY = 0; $minX = $width; $maxX = 0;
for ($y = 0; $y -lt $height; $y++) {
    for ($x = 0; $x -lt $width; $x++) {
        if ($bmp.GetPixel($x, $y).A -gt 10) {
            if ($y -lt $minY) { $minY = $y }
            if ($y -gt $maxY) { $maxY = $y }
            if ($x -lt $minX) { $minX = $x }
            if ($x -gt $maxX) { $maxX = $x }
        }
    }
}

$gapStart = 0
$gapEnd = 0
$inContent = $false
for ($y = $minY; $y -le $maxY; $y++) {
    $rowHasPixels = $false
    for ($x = $minX; $x -le $maxX; $x++) {
        if ($bmp.GetPixel($x, $y).A -gt 10) {
            $rowHasPixels = $true
            break
        }
    }
    
    if ($rowHasPixels) {
        if ($inContent -and $gapStart -gt 0 -and $gapEnd -eq 0) {
            $gapEnd = $y
            Write-Host "Gap ends at $gapEnd"
        }
        $inContent = $true
    } else {
        if ($inContent -and $gapStart -eq 0) {
            $gapStart = $y
            Write-Host "Gap starts at $gapStart"
        }
    }
}

if ($gapStart -eq 0) {
    # If no gap found, assume top 50% is the mark.
    $gapStart = [int]($minY + ($maxY - $minY) / 2)
}

$markMinX = $width; $markMaxX = 0;
for ($y = $minY; $y -lt $gapStart; $y++) {
    for ($x = $minX; $x -le $maxX; $x++) {
        if ($bmp.GetPixel($x, $y).A -gt 10) {
            if ($x -lt $markMinX) { $markMinX = $x }
            if ($x -gt $markMaxX) { $markMaxX = $x }
        }
    }
}

$markWidth = $markMaxX - $markMinX
$markHeight = $gapStart - $minY
Write-Host "Mark is from Y=$minY to $gapStart, X=$markMinX to $markMaxX. Width=$markWidth, Height=$markHeight"

# Crop it to a square
$sz = [Math]::Max($markWidth, $markHeight)
$rect = New-Object System.Drawing.Rectangle([int]($markMinX - ($sz - $markWidth)/2), [int]($minY - ($sz - $markHeight)/2), $sz, $sz)
$bmpCrop = $bmp.Clone($rect, $bmp.PixelFormat)

$outPath = "c:\Users\techbox\.gemini\antigravity\ManagementBackup1234\Management.Presentation\Assets\atrium_icon_transparent.png"
$bmpCrop.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
Write-Host "Saved $outPath"
$bmpCrop.Dispose()
$bmp.Dispose()
$img.Dispose()
