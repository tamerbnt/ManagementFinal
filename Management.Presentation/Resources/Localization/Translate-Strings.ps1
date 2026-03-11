param(
    [string]$SourceFile = "Strings.en.xaml"
)

function Translate-Batch {
    param([string[]]$Texts, [string]$TargetLang)
    
    # Preserve {0}, {1} by wrapping or hoping it stays intact. GT usually keeps them.
    $combined = $Texts -join " ||| "
    $url = "https://translate.googleapis.com/translate_a/single?client=gtx&sl=en&tl=$TargetLang&dt=t&q=" + [uri]::EscapeDataString($combined)
    
    try {
        $response = Invoke-RestMethod -Uri $url
        $translatedCombined = ""
        foreach ($chunk in $response[0]) {
            $translatedCombined += $chunk[0]
        }
        
        $translatedArr = $translatedCombined -split '\|\|\|' | ForEach-Object { $_.Trim() }
        
        # Fallback if split count doesn't match
        if ($translatedArr.Length -ne $Texts.Length) {
            Write-Host "Warning: Split count mismatch. Doing individually..."
            $translatedArr = @()
            foreach ($t in $Texts) {
                if ([string]::IsNullOrWhiteSpace($t)) {
                    $translatedArr += $t
                    continue
                }
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
        Write-Host "Error translating batch: $_"
        return $Texts # fallback to English
    }
}

function Process-File {
    param([string]$TargetLang, [string]$OutputFile)
    Write-Host "Processing $TargetLang into $OutputFile..."
    
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
                $outLines += $line # Placeholder
            }
        } else {
            $outLines += $line
        }
    }
    
    Write-Host "Found $($pendingTexts.Count) strings to translate to $TargetLang"
    
    $batchSize = 20
    for ($b = 0; $b -lt $pendingTexts.Count; $b += $batchSize) {
        $chunkSize = [math]::Min($batchSize, $pendingTexts.Count - $b)
        $batchTexts = $pendingTexts[$b..($b + $chunkSize - 1)]
        $batchIndices = $pendingIndices[$b..($b + $chunkSize - 1)]
        
        Write-Host "Translating batch $($b / $batchSize)..."
        $translated = Translate-Batch -Texts $batchTexts -TargetLang $TargetLang
        
        for ($j = 0; $j -lt $chunkSize; $j++) {
            $idx = $batchIndices[$j]
            $origLine = $linesArray[$idx]
            if ($origLine -match $extractRegex) {
                $fixedText = $translated[$j]
                # Fix { 0 } spaces that GT might inject
                $fixedText = $fixedText -replace '\{ (\d+) \}', '{$1}'
                $fixedText = $fixedText -replace '\{(\d+) \}', '{$1}'
                $fixedText = $fixedText -replace '\{ (\d+)\}', '{$1}'
                # Fix special xml chars
                $fixedText = $fixedText -replace '&(?!amp;|lt;|gt;|quot;|apos;)', '&amp;'
                $fixedText = $fixedText -replace '<', '&lt;'
                $fixedText = $fixedText -replace '>', '&gt;'
                
                $outLines[$idx] = $matches['prefix'] + $fixedText + $matches['suffix']
            }
        }
        Start-Sleep -Milliseconds 200
    }
    
    [IO.File]::WriteAllText($OutputFile, ($outLines -join "`r`n"), [System.Text.Encoding]::UTF8)
    Write-Host "Saved $OutputFile successfully!"
}

Process-File -TargetLang "fr" -OutputFile "Strings.fr.xaml"
Process-File -TargetLang "ar" -OutputFile "Strings.ar.xaml"
