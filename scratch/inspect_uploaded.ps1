Add-Type -AssemblyName System.Drawing
$path = "C:\Users\techbox\.gemini\antigravity\brain\e0724ef8-a2c4-4aa5-be44-f63b8fdf9e37\media__1779084444720.png"
if (Test-Path $path) {
    $img = [System.Drawing.Image]::FromFile($path)
    Write-Host "Uploaded Image:"
    Write-Host "Width = $($img.Width)"
    Write-Host "Height = $($img.Height)"
    $img.Dispose()
} else {
    Write-Host "Not found: $path"
}
