param(
    [string]$SourceFile
)

if (-not $SourceFile) {
    Write-Host "Usage: .\Translate-Generic.ps1 -SourceFile Terminology.Salon.xaml" -ForegroundColor Yellow
    return
}

function Translate-Batch {
    param([string[]]$Texts, [string]$TargetLang)
    
    $combined = $Texts -join " ||| "
    $url = "https://translate.googleapis.com/translate_a/single?client=gtx&sl=en&tl=$TargetLang&dt=t&q=" + [uri]::EscapeDataString($combined)
    
    try {
        $response = Invoke-RestMethod -Uri $url
        $translatedCombined = ""
        foreach ($chunk in $response[0]) {
            $translatedCombined += $chunk[0]
        }
        
        $translatedArr = $translatedCombined -split '\|\|\|' | ForEach-Object { $_.Trim() }
        
        if ($translatedArr.Length -ne $Texts.Length) {
            $translatedArr = @()
            foreach ($t in $Texts) {
                if ([string]::IsNullOrWhiteSpace($t)) { $translatedArr += $t; continue }
                $u = "https://translate.googleapis.com/translate_a/single?client=gtx&sl=en&tl=$TargetLang&dt=t&q=" + [uri]::EscapeDataString($t)
                $r = Invoke-RestMethod -Uri $u
                $translatedText = ""
                foreach ($c in $r[0]) { $translatedText += $c[0] }
                $translatedArr += $translatedText.Trim()
                Start-Sleep -Milliseconds 50
            }
        }
        return $translatedArr
    } catch {
        return $Texts
    }
}

function Process-File {
    param([string]$TargetLang, [string]$OutputFile)
    Write-Host "Processing $SourceFile -> $OutputFile ($TargetLang)..."
    
    $lines = Get-Content $SourceFile -Raw
    $linesArray = $lines -split "`r`n|`n"
    
    $outLines = @()
    $extractRegex = '^(?<prefix>\s*<sys:String x:Key=".*?">)(?<text>.*?)(?<suffix></sys:String>\s*)$'
    
    $pendingTexts = @()
    $pendingIndices = @()
    
    for ($i = 0; $i -lt $linesArray.Length; $i++) {
        $line = $linesArray[$i]
        if ($line -match $extractRegex) {
            $text = $matches['text']
            if ([string]::IsNullOrWhiteSpace($text)) {
                $outLines += $line
            } else {
                $pendingTexts += $text
                $pendingIndices += $i
                $outLines += $line 
            }
        } else {
            $outLines += $line
        }
    }
    
    $batchSize = 25
    for ($b = 0; $b -lt $pendingTexts.Count; $b += $batchSize) {
        $chunkSize = [math]::Min($batchSize, $pendingTexts.Count - $b)
        $batchTexts = $pendingTexts[$b..($b + $chunkSize - 1)]
        $batchIndices = $pendingIndices[$b..($b + $chunkSize - 1)]
        
        $translated = Translate-Batch -Texts $batchTexts -TargetLang $TargetLang
        
        for ($j = 0; $j -lt $chunkSize; $j++) {
            $idx = $batchIndices[$j]
            if ($linesArray[$idx] -match $extractRegex) {
                $fixedText = $translated[$j]
                $fixedText = $fixedText -replace '\{ (\d+) \}', '{$1}'
                $fixedText = $fixedText -replace '\{(\d+) \}', '{$1}'
                $fixedText = $fixedText -replace '\{ (\d+)\}', '{$1}'
                $fixedText = $fixedText -replace '&(?!amp;|lt;|gt;|quot;|apos;)', '&amp;'
                $fixedText = $fixedText -replace '<', '&lt;'
                $fixedText = $fixedText -replace '>', '&gt;'
                $outLines[$idx] = $matches['prefix'] + $fixedText + $matches['suffix']
            }
        }
        Start-Sleep -Milliseconds 100
    }
    
    # CRITICAL: Force UTF-8 WITH BOM
    $utf8WithBom = New-Object System.Text.UTF8Encoding($true)
    [IO.File]::WriteAllText((Join-Path (Get-Location) $OutputFile), ($outLines -join "`r`n"), $utf8WithBom)
    Write-Host "Saved $OutputFile with BOM!" -ForegroundColor Green
}

$baseName = [System.IO.Path]::GetFileNameWithoutExtension($SourceFile)
Process-File -TargetLang "ar" -OutputFile "$baseName.ar.xaml"
Process-File -TargetLang "fr" -OutputFile "$baseName.fr.xaml"
