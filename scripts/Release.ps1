param([switch] $SkipTests)
. "$PSScriptRoot\Environment.ps1"
Push-Location $script:ProjectRoot
try {
    & "$PSScriptRoot\Get-PresentMon.ps1"
    if (-not $SkipTests) { & "$PSScriptRoot\Build.ps1" }
    $props = [xml](Get-Content -LiteralPath 'Directory.Build.props' -Raw)
    $version = $props.Project.PropertyGroup.Version
    $releaseRoot = Join-Path $script:ProjectRoot 'artifacts\release'
    # A fresh staging directory prevents obsolete files from leaking into a new archive.
    $stage = Join-Path $releaseRoot ([Guid]::NewGuid().ToString('N'))
    $package = Join-Path $stage 'XWhat'
    New-Item -ItemType Directory -Path $package -Force | Out-Null
    Invoke-DotNet publish src/XWhat.App --configuration Release --runtime win-x64 --self-contained true --output $package
    Copy-Item -LiteralPath 'README.md','LICENSE','CHANGELOG.md','THIRD-PARTY-NOTICES.md' -Destination $package
    Copy-Item -LiteralPath 'docs' -Destination $package -Recurse
    $dependencies = & $script:DotNet package list --project src/XWhat.App --include-transitive --format json --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Dependency report failed' }
    $dependencyReport = ($dependencies -join "`n") | ConvertFrom-Json
    foreach ($reportProject in $dependencyReport.projects) { $reportProject.path = 'src/XWhat.App/XWhat.App.csproj' }
    $dependencyReport | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $package 'docs\dependencies.json') -Encoding utf8
    $archive = Join-Path $releaseRoot "XWhat-$version-win-x64.zip"
    Compress-Archive -Path $package -DestinationPath $archive -CompressionLevel Optimal -Force
    $hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  XWhat-$version-win-x64.zip" | Set-Content -LiteralPath "$archive.sha256" -Encoding ascii
    Write-Output "Release executable: $package\XWhat.exe"
    Write-Output "Release archive: $archive"
} finally { Pop-Location }
