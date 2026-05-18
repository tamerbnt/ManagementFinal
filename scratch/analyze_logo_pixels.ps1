Add-Type -AssemblyName System.Drawing
$path = "C:\Users\techbox\.gemini\antigravity\brain\e0724ef8-a2c4-4aa5-be44-f63b8fdf9e37\media__1779084444720.png"
$bmp = New-Object System.Drawing.Bitmap($path)
$width = $bmp.Width
$height = $bmp.Height

# Find the bounding box of non-background pixels.
# The background is dark. Let's find pixels that are not close to the background color.
# Let's inspect the corner pixel to know what the background color is.
$bgPixel = $bmp.GetPixel(0, 0)
Write-Host "Background color at (0,0): R=$($bgPixel.R), G=$($bgPixel.G), B=$($bgPixel.B)"

$minY = $height; $maxY = 0; $minX = $width; $maxX = 0;
for ($y = 0; $y -lt $height; $y++) {
    for ($x = 0; $x -lt $width; $x++) {
        $c = $bmp.GetPixel($x, $y)
        # Check if it differs significantly from the background color
        $diff = [Math]::Abs($c.R - $bgPixel.R) + [Math]::Abs($c.G - $bgPixel.G) + [Math]::Abs($c.B - $bgPixel.B)
        if ($diff -gt 30) {
            if ($y -lt $minY) { $minY = $y }
            if ($y -gt $maxY) { $maxY = $y }
            if ($x -lt $minX) { $minX = $x }
            if ($x -gt $maxX) { $maxX = $x }
        }
    }
}

Write-Host "Non-background content bounds: X=$minX to $maxX (width=$($maxX - $minX)), Y=$minY to $maxY (height=$($maxY - $minY))"

# Let's find any vertical gap between the logo mark and the text.
$gaps = @()
$inGap = $false
$gapStart = 0
for ($y = $minY; $y -le $maxY; $y++) {
    $rowHasPixels = $false
    for ($x = $minX; $x -le $maxX; $x++) {
        $c = $bmp.GetPixel($x, $y)
        $diff = [Math]::Abs($c.R - $bgPixel.R) + [Math]::Abs($c.G - $bgPixel.G) + [Math]::Abs($c.B - $bgPixel.B)
        if ($diff -gt 30) {
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
            if (($y - $gapStart) -gt 3) {
                $gaps += [PSCustomObject]@{ Start=$gapStart; End=$y; Size=($y - $gapStart) }
            }
        }
    }
}

$biggestGap = $gaps | Sort-Object Size -Descending | Select-Object -First 1
if ($biggestGap) {
    $markMaxY = $biggestGap.Start
    Write-Host "Found biggest gap from Y=$($biggestGap.Start) to Y=$($biggestGap.End). Logo mark is from Y=$minY to Y=$markMaxY"
    
    # Calculate bounding box of the logo mark only
    $markMinX = $width; $markMaxX = 0;
    for ($y = $minY; $y -lt $markMaxY; $y++) {
        for ($x = $minX; $x -le $maxX; $x++) {
            $c = $bmp.GetPixel($x, $y)
            $diff = [Math]::Abs($c.R - $bgPixel.R) + [Math]::Abs($c.G - $bgPixel.G) + [Math]::Abs($c.B - $bgPixel.B)
            if ($diff -gt 30) {
                if ($x -lt $markMinX) { $markMinX = $x }
                if ($x -gt $markMaxX) { $markMaxX = $x }
            }
        }
    }
    
    $markWidth = $markMaxX - $markMinX
    $markHeight = $markMaxY - $minY
    Write-Host "Logo mark: X=$markMinX to $markMaxX (width=$markWidth), Y=$minY to $markMaxY (height=$markHeight)"
} else {
    Write-Host "No gap found!"
}

$bmp.Dispose()
