Add-Type -AssemblyName System.Drawing

$presentationAssetsDir = "c:\Users\techbox\.gemini\antigravity\ManagementBackup1234\Management.Presentation\Assets"
$icoPath = Join-Path $presentationAssetsDir "atrium_rounded.ico"
$pngPath = Join-Path $presentationAssetsDir "atrium_icon_rounded.png"
$rootIcoPath = "c:\Users\techbox\.gemini\antigravity\ManagementBackup1234\assets\app.ico"

# 1. Verify PNG preview
if (Test-Path $pngPath) {
    $img = [System.Drawing.Image]::FromFile($pngPath)
    Write-Host "atrium_icon_rounded.png: EXISTS, size = $($img.Width)x$($img.Height)"
    
    # Inspect center pixel color to ensure it matches emblem, and some background pixels to ensure it is dark
    $bmp = New-Object System.Drawing.Bitmap($img)
    $bgPixel = $bmp.GetPixel(10, 10)
    $centerPixel = $bmp.GetPixel(128, 128)
    
    Write-Host "  - Corner/Background pixel color: R=$($bgPixel.R), G=$($bgPixel.G), B=$($bgPixel.B) (A=$($bgPixel.A))"
    Write-Host "  - Center pixel color: R=$($centerPixel.R), G=$($centerPixel.G), B=$($centerPixel.B) (A=$($centerPixel.A))"
    
    # Bounding box of emblem in the 256x256 image
    $minY = 256; $maxY = 0; $minX = 256; $maxX = 0;
    $orangeCount = 0
    for ($y = 0; $y -lt 256; $y++) {
        for ($x = 0; $x -lt 256; $x++) {
            $c = $bmp.GetPixel($x, $y)
            # Find the orange emblem pixels (using simple R > B + 30 and G > B + 10 heuristic)
            if ($c.A -gt 10 -and ($c.R - $c.B) -gt 30 -and $c.G -gt $c.B) {
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
        $percent = [Math]::Round(($markWidth / 256) * 100, 1)
        Write-Host "  - Detected Orange Emblem: Width = $markWidth, Height = $markHeight"
        Write-Host "  - Emblem occupies $percent% of canvas width (target: ~65%)"
    } else {
        Write-Host "  - Warning: Orange emblem not detected by simple heuristic."
    }
    
    $bmp.Dispose()
    $img.Dispose()
} else {
    Write-Host "atrium_icon_rounded.png: NOT FOUND"
}

# 2. Verify presentation ICO
if (Test-Path $icoPath) {
    $fileSize = (Get-Item $icoPath).Length
    Write-Host "atrium_rounded.ico: EXISTS, size = $fileSize bytes"
} else {
    Write-Host "atrium_rounded.ico: NOT FOUND"
}

# 3. Verify root installer ICO
if (Test-Path $rootIcoPath) {
    $fileSize = (Get-Item $rootIcoPath).Length
    Write-Host "assets/app.ico: EXISTS, size = $fileSize bytes"
} else {
    Write-Host "assets/app.ico: NOT FOUND"
}
