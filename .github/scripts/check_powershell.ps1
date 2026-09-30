$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Push-Location $repoRoot
try {
    $paths = @(git ls-files -- '*.ps1' ':!:node_modules/**')
    if ($LASTEXITCODE -ne 0 -or $paths.Count -eq 0) {
        throw 'Could not enumerate tracked PowerShell scripts.'
    }

    $failures = 0
    foreach ($path in $paths) {
        $tokens = $null
        $parseErrors = $null
        [void][System.Management.Automation.Language.Parser]::ParseFile(
            (Join-Path $repoRoot $path), [ref]$tokens, [ref]$parseErrors)
        foreach ($parseError in $parseErrors) {
            Write-Output "${path}:$($parseError.Extent.StartLineNumber): $($parseError.Message)"
            $failures++
        }
    }

    if ($failures -gt 0) {
        throw "Found $failures PowerShell syntax errors."
    }

    $result = "Parsed $($paths.Count) tracked PowerShell scripts. No scripts were executed; deployment and external tools remain untested."
    Write-Output $result
    if ($env:GITHUB_STEP_SUMMARY) {
        Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value $result -Encoding utf8
    }
}
finally {
    Pop-Location
}
