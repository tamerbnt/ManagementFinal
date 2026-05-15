Add-Type -AssemblyName System.Drawing
$img = [System.Drawing.Image]::FromFile('c:\Users\techbox\.gemini\antigravity\ManagementBackup1234\Management.Presentation\Assets\atrium_header_logo_transparent.png')
$bmp = New-Object System.Drawing.Bitmap($img)
$width = $bmp.Width
$height = $bmp.Height
Write-Host "Width=$width, Height=$height"

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
Write-Host "Content bounds: X=$minX to $maxX, Y=$minY to $maxY"

$gaps = @()
$inGap = $false
$gapStart = 0
for ($y = $minY; $y -le $maxY; $y++) {
    $rowHasPixels = $false
    for ($x = $minX; $x -le $maxX; $x++) {
        if ($bmp.GetPixel($x, $y).A -gt 10) {
            $rowHasPixels = $true
            break
        }
    }
    
    if (-not $rowHasPixels) {
        if (-not $inGap) {
            $inGap = $true
            $gapStart = $y
        }
    } else {
        if ($inGap) {
            $inGap = $false
            if (($y - $gapStart) -gt 5) {
                $gaps += [PSCustomObject]@{ Start=$gapStart; End=$y; Size=($y - $gapStart) }
            }
        }
    }
}

$biggestGap = $gaps | Sort-Object Size -Descending | Select-Object -First 1
if ($biggestGap) {
    $markMaxY = $biggestGap.Start
    Write-Host "Mark goes from Y=$minY to Y=$markMaxY"
    
    $markMinX = $width; $markMaxX = 0;
    for ($y = $minY; $y -lt $markMaxY; $y++) {
        for ($x = $minX; $x -le $maxX; $x++) {
            if ($bmp.GetPixel($x, $y).A -gt 10) {
                if ($x -lt $markMinX) { $markMinX = $x }
                if ($x -gt $markMaxX) { $markMaxX = $x }
            }
        }
    }
    
    $markWidth = $markMaxX - $markMinX
    $markHeight = $markMaxY - $minY
    Write-Host "Mark Width=$markWidth, Height=$markHeight"
    
    $sz = [Math]::Max($markWidth, $markHeight)
    $pad = [int]($sz * 0.1)
    $sz = $sz + $pad * 2
    
    $rectX = [int]($markMinX - ($sz - $markWidth)/2)
    $rectY = [int]($minY - ($sz - $markHeight)/2)
    
    if ($rectX -lt 0) { $rectX = 0 }
    if ($rectY -lt 0) { $rectY = 0 }
    if ($rectX + $sz -gt $width) { $sz = $width - $rectX }
    if ($rectY + $sz -gt $height) { $sz = $height - $rectY }

    $rect = New-Object System.Drawing.Rectangle($rectX, $rectY, $sz, $sz)
    $bmpCrop = $bmp.Clone($rect, $bmp.PixelFormat)

    $outPath = 'c:\Users\techbox\.gemini\antigravity\ManagementBackup1234\Management.Presentation\Assets\atrium_icon_transparent.png'
    $bmpCrop.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
    Write-Host "Saved successfully!"
    $bmpCrop.Dispose()
} else {
    Write-Host "No gap found!"
}

$bmp.Dispose()
$img.Dispose()
