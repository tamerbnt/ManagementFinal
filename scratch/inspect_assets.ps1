Add-Type -AssemblyName System.Drawing
$assets = @("atrium.ico", "atrium_rounded.ico", "atrium_icon_rounded.png", "atrium_icon_transparent.png", "atrium_header_logo.png", "atrium_header_logo_transparent.png")
$dir = "c:\Users\techbox\.gemini\antigravity\ManagementBackup1234\Management.Presentation\Assets"

foreach ($file in $assets) {
    $path = Join-Path $dir $file
    if (Test-Path $path) {
        if ($file.EndsWith(".ico")) {
            $ico = New-Object System.Drawing.Icon($path)
            Write-Host "$file : ICO file, size = $($ico.Size.Width)x$($ico.Size.Height)"
            $ico.Dispose()
        } else {
            $img = [System.Drawing.Image]::FromFile($path)
            Write-Host "$file : Image file, size = $($img.Width)x$($img.Height), format = $($img.RawFormat)"
            $img.Dispose()
        }
    } else {
        Write-Host "$file : Not found"
    }
}
