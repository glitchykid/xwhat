param([Parameter(Mandatory)][string] $PackagePath)
. "$PSScriptRoot/Environment.ps1"
& "$PSScriptRoot/Get-InnoSetup.ps1"
$props = [xml](Get-Content -LiteralPath (Join-Path $script:ProjectRoot 'Directory.Build.props') -Raw)
$version = $props.Project.PropertyGroup.Version
$destination = Join-Path $script:ProjectRoot 'artifacts/release'
$source = (Resolve-Path -LiteralPath $PackagePath).Path
$compiler = Join-Path $script:ProjectRoot '.tools/inno7/ISCC.exe'
& $compiler --quiet-progress "--define=AppVersion=$version" "--define=SourcePath=$source" "--output-dir=$destination" (Join-Path $script:ProjectRoot 'installer/XWhat.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed' }
$setup = Join-Path $destination "XWhat-$version-Setup-win-x64.exe"
$hash = (Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $([IO.Path]::GetFileName($setup))" | Set-Content -LiteralPath "$setup.sha256" -Encoding ascii
Write-Output "Installer: $setup"
