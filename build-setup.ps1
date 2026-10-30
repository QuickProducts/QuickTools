$ErrorActionPreference='Stop'
$sdk=Join-Path $PSScriptRoot '.sdk\dotnet.exe'
if(-not (Test-Path $sdk)) { $sdk='dotnet' }
$env:DOTNET_CLI_HOME=Join-Path $PSScriptRoot '.sdk\cli'
Push-Location $PSScriptRoot
try {
    & (Join-Path $PSScriptRoot 'assets\Create-Icon.ps1')
    & $sdk publish QuickTools.csproj -c Release -r win-x64 --self-contained true -p:BaseOutputPath=.build/ -o dist
    if($LASTEXITCODE -ne 0) { throw 'Ошибка сборки приложения' }
    Compress-Archive -Path (Join-Path $PSScriptRoot 'dist\*') -DestinationPath (Join-Path $PSScriptRoot 'Setup\payload.zip') -Force
    (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'Setup\payload.zip') -Algorithm SHA256).Hash | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'Setup\payload.sha256') -Encoding ascii
    & $sdk publish Setup/QuickToolsSetup.csproj -c Release -o release
    if($LASTEXITCODE -ne 0) { throw 'Ошибка сборки установщика' }
} finally { Pop-Location }
