param([switch]$Publish)
$ErrorActionPreference = 'Stop'
$sdk = Join-Path $PSScriptRoot '.sdk\dotnet.exe'
if (-not (Test-Path $sdk)) { $sdk = 'dotnet' }
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.sdk\cli'
Push-Location $PSScriptRoot
try {
    & (Join-Path $PSScriptRoot 'assets\Create-Icon.ps1')
    if ($Publish) { & $sdk publish QuickTools.csproj -c Release -r win-x64 --self-contained true -p:BaseOutputPath=.build/ -o dist }
    else { & $sdk build QuickTools.csproj -c Release -p:BaseOutputPath=.build/ }
    if ($LASTEXITCODE -ne 0) { throw 'Сборка не завершена' }
} finally { Pop-Location }
