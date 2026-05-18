Add-Type -AssemblyName System.Drawing
$path = "c:\Users\techbox\.gemini\antigravity\ManagementBackup1234\Management.Presentation\Assets\atrium_icon_rounded.png"
if (Test-Path $path) {
    $bmp = New-Object System.Drawing.Bitmap($path)
    $width = $bmp.Width
    $height = $bmp.Height
    
    # Find bounding box of non-transparent pixels in the center area (excluding the rounded background if it is transparent, or finding the logo mark inside it)
    # The logo mark is a specific color (rust/orange or whatever color was used).
    # Let's inspect the pixels to see what colors are present and locate the logo mark.
    # We can group pixels by color or just find non-transparent, non-background pixels.
    # Since the icon is rounded, the corners are transparent. The background of the round rect is some color.
    # Let's check the colors of some pixels.
    $colors = @{}
    for ($y = 0; $y -lt $height; $y += 4) {
        for ($x = 0; $x -lt $width; $x += 4) {
            $c = $bmp.GetPixel($x, $y)
            if ($c.A -gt 10) {
                $hex = "{0:X2}{1:X2}{2:X2}" -f $c.R, $c.G, $c.B
                $colors[$hex] = ($colors[$hex] + 1)
            }
        }
    }
    
    Write-Host "Top colors in current icon (sample):"
    $colors.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First 10 | ForEach-Object {
        Write-Host "$($_.Name): $($_.Value) pixels"
    }
    
    # Let's find the bounding box of the orange logo mark.
    # The orange color in the brand is roughly R=196, G=120, B=90 (#C4785A) or similar.
    # Let's search for pixels where R is significantly higher than B (e.g. R - B > 50) to find the orange mark.
    $minY = $height; $maxY = 0; $minX = $width; $maxX = 0;
    $orangeCount = 0
    for ($y = 0; $y -lt $height; $y++) {
        for ($x = 0; $x -lt $width; $x++) {
            $c = $bmp.GetPixel($x, $y)
            if ($c.A -gt 10 -and ($c.R - $c.B) -gt 40 -and $c.G -gt $c.B) {
                $orangeCount++
                if ($y -lt $minY) { $minY = $y }
                if ($y -gt $maxY) { $maxY = $y }
                if ($x -lt $minX) { $minX = $x }
                if ($x -gt $maxX) { $maxX = $x }
            }
        }
    }
    
    if ($orangeCount -gt 0) {
        $markWidth = $maxX - $minX
        $markHeight = $maxY - $minY
        Write-Host "Orange Logo Mark in current icon:"
        Write-Host "Bounds: X=$minX to $maxX (width=$markWidth), Y=$minY to $maxY (height=$markHeight)"
        Write-Host "Percentage of canvas width: $([Math]::Round(($markWidth / $width) * 100, 1))%"
    } else {
        Write-Host "Could not locate orange logo mark by color heuristic."
    }
    
    $bmp.Dispose()
} else {
    Write-Host "File not found: $path"
}
