$ErrorActionPreference = 'Stop'
$script:ProjectRoot = Split-Path -Parent $PSScriptRoot
$script:DotNet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $script:DotNet) {
    $script:DotNet = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
}
if (-not (Test-Path -LiteralPath $script:DotNet)) {
    throw 'Install the .NET 10 SDK: https://dotnet.microsoft.com/download/dotnet/10.0'
}
$env:DOTNET_CLI_HOME = Join-Path $script:ProjectRoot '.tools\dotnet-home'
$env:NUGET_PACKAGES = Join-Path $script:ProjectRoot '.packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:MSBuildEnableWorkloadResolver = 'false'

function Invoke-DotNet {
    & $script:DotNet @args
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed with exit code $LASTEXITCODE" }
}
