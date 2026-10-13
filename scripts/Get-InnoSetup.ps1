$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$compilerRoot = Join-Path $projectRoot '.tools/inno7'
$compiler = Join-Path $compilerRoot 'ISCC.exe'
if (Test-Path -LiteralPath $compiler) {
    if ((& $compiler --version) -ne '7.1.0') { throw 'Expected Inno Setup 7.1.0 compiler' }
    return
}
$setup = Join-Path $projectRoot '.tools/innosetup-7.1.0-x64.exe'
$expected = '0362A383ED217D4C4239B5933866DD96D3EB2102737DA92F80F6057A4B40DF2F'
New-Item -ItemType Directory -Path (Split-Path -Parent $setup) -Force | Out-Null
if (-not (Test-Path -LiteralPath $setup)) {
    Invoke-WebRequest -Uri 'https://github.com/jrsoftware/issrc/releases/download/is-7_1_0/innosetup-7.1.0-x64.exe' -OutFile $setup
}
if ((Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash -ne $expected) { throw 'Inno Setup download failed SHA-256 verification' }
if ((Get-AuthenticodeSignature -FilePath $setup).Status -ne 'Valid') { throw 'Inno Setup signature is not valid' }
$install = Start-Process -FilePath $setup -ArgumentList @('/CURRENTUSER','/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/NOICONS','/SP-',('/DIR="' + $compilerRoot + '"'),'/TASKS=""') -WindowStyle Hidden -PassThru
$install.WaitForExit()
if ($install.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $compiler)) { throw 'Inno Setup compiler installation failed' }
