. "$PSScriptRoot\Environment.ps1"
Push-Location $script:ProjectRoot
try {
    Invoke-DotNet build src/XWhat.App -c Release
    $app = Join-Path $script:ProjectRoot 'src\XWhat.App\bin\Release\net10.0-windows\XWhat.exe'
    $image = Join-Path $script:ProjectRoot 'artifacts\preview\overlay.png'
    $previewProcess = Start-Process -FilePath $app -ArgumentList @('--demo', ('"--render-preview=' + $image + '"')) -WindowStyle Hidden -PassThru
    $previewProcess.WaitForExit()
    if ($previewProcess.ExitCode -ne 0) { throw 'Preview failed' }
    Write-Output $image
    Write-Output ([IO.Path]::ChangeExtension($image, '.settings.png'))
    Write-Output ([IO.Path]::ChangeExtension($image, '.tray.png'))
} finally { Pop-Location }
