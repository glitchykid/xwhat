$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$destination = Join-Path $projectRoot 'tools\PresentMon\PresentMon.exe'
$expectedHash = 'B2A706BC6AD475749E3B7E3409263AA1E6906D45BDCF993F6DBC0F660188F1AF'
if (Test-Path -LiteralPath $destination) {
    if ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -eq $expectedHash) {
        Write-Output 'PresentMon 2.6.0: checksum verified'
        return
    }
    throw 'Existing PresentMon.exe has an unexpected SHA-256. Move it aside and retry.'
}
New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
$temporary = $destination + '.download'
Invoke-WebRequest -Uri 'https://github.com/GameTechDev/PresentMon/releases/download/v2.6.0/PresentMon-2.6.0-x64.exe' -OutFile $temporary
if ((Get-FileHash -LiteralPath $temporary -Algorithm SHA256).Hash -ne $expectedHash) {
    throw 'PresentMon download failed integrity verification. It will not be executed.'
}
Move-Item -LiteralPath $temporary -Destination $destination
Write-Output 'PresentMon 2.6.0: downloaded and verified'
