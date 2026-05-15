$root = Split-Path $PSScriptRoot

function ReplaceInFile($rel, [scriptblock]$transform) {
    $path = Join-Path $root $rel
    $c = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
    $c = & $transform $c
    [System.IO.File]::WriteAllText($path, $c, [System.Text.Encoding]::UTF8)
    Write-Host "Updated: $rel"
}

# ── Localization ──────────────────────────────────────────────────────────────
ReplaceInFile 'Management.Presentation\Resources\Localization\Strings.en.xaml' {
    param($c)
    $c = $c.Replace('Welcome to Luxurya Management', 'Welcome to Atrium Management')
    $c = $c.Replace('Quit Luxurya?', 'Quit Atrium?')
    $c = $c.Replace('Luxurya Activation', 'Atrium Activation')
    $c = $c.Replace('>LUXURYA<', '>ATRIUM<')
    $c = $c.Replace('staff@luxurya.com', 'staff@atrium.com')
    $c
}

ReplaceInFile 'Management.Presentation\Resources\Localization\Strings.fr.xaml' {
    param($c)
    $c = $c.Replace('Bienvenue chez Luxurya Management', 'Bienvenue chez Atrium Management')
    $c = $c.Replace('Quitter Luxurya ?', 'Quitter Atrium ?')
    $c = $c.Replace('Activation Luxurya', 'Activation Atrium')
    $c = $c.Replace('LUXURYA', 'ATRIUM')
    $c = $c.Replace('staff@luxurya.com', 'staff@atrium.com')
    $c
}

ReplaceInFile 'Management.Presentation\Resources\Localization\Strings.ar.xaml' {
    param($c)
    $c = $c.Replace('staff@luxurya.com', 'staff@atrium.com')
    $c
}

# ── ViewModels ────────────────────────────────────────────────────────────────
ReplaceInFile 'Management.Presentation\ViewModels\Shell\DashboardViewModel.cs' {
    param($c)
    $c = $c.Replace('Welcome to Luxurya Management', 'Welcome to Atrium Management')
    $c = $c.Replace('"Luxurya", "Reports"', '"Atrium", "Reports"')
    $c
}

ReplaceInFile 'Management.Presentation\ViewModels\Shell\AppExitViewModel.cs' {
    param($c)
    $c = $c.Replace('Quit Luxurya?', 'Quit Atrium?')
    $c = $c.Replace('"Luxurya", "Reports"', '"Atrium", "Reports"')
    $c = $c.Replace('Documents\\Luxurya\\Reports', 'Documents\\Atrium\\Reports')
    $c
}

ReplaceInFile 'Management.Presentation\ViewModels\History\HistoryViewModel.cs' {
    param($c)
    $c = $c.Replace('"Luxurya", "Reports"', '"Atrium", "Reports"')
    $c
}

ReplaceInFile 'Management.Presentation\ViewModels\Finance\FinanceViewModel.cs' {
    param($c)
    $c = $c.Replace('"Luxurya",', '"Atrium",')
    $c
}

ReplaceInFile 'Management.Presentation\ViewModels\Auth\SplashOnboardingViewModel.cs' {
    param($c)
    $c = $c.Replace('Luxurya.Client;component', 'Atrium.Client;component')
    $c
}

# ── Services ──────────────────────────────────────────────────────────────────
ReplaceInFile 'Management.Presentation\Services\FacilityContextService.cs' {
    param($c)
    $c = $c.Replace('"Luxurya"', '"Atrium"')
    $c = $c.Replace('ProgramData\Luxurya', 'ProgramData\Atrium')
    $c
}

ReplaceInFile 'Management.Presentation\Services\Infrastructure\SecureStorageService.cs' {
    param($c)
    $c = $c.Replace('"Luxurya"', '"Atrium"')
    $c
}

ReplaceInFile 'Management.Presentation\Services\VelopackHooks.cs' {
    param($c)
    $c = $c.Replace('"Luxurya.Client"', '"Atrium.Client"')
    $c = $c.Replace('"Luxurya"', '"Atrium"')
    $c = $c.Replace('ProgramData\Luxurya', 'ProgramData\Atrium')
    $c = $c.Replace('LocalAppData\Luxurya', 'LocalAppData\Atrium')
    $c = $c.Replace('Luxurya.Client', 'Atrium.Client')
    $c
}

ReplaceInFile 'Management.Presentation\Services\ThemeManager.cs' {
    param($c)
    $c = $c.Replace('"Luxurya"', '"Atrium"')
    $c
}

Write-Host "`nAll files updated successfully."
