. "$PSScriptRoot\Environment.ps1"
Push-Location $script:ProjectRoot
try {
    & "$PSScriptRoot\Get-PresentMon.ps1"
    Invoke-DotNet restore XWhat.slnx --locked-mode
    Invoke-DotNet run --project tests/XWhat.Core.Tests -c Release --no-restore
    Invoke-DotNet run --project tests/XWhat.App.Tests -c Release --no-restore
    Invoke-DotNet build src/XWhat.App -c Release --no-restore
} finally { Pop-Location }
