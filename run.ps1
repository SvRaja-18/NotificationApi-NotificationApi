# Launch script for Notification Processing API (.NET 10)
$env:DOTNET_CLI_HOME = "$PSScriptRoot\.dotnet"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_NOLOGO = "1"
$env:DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER = "1"

$dotnetExe = "$PSScriptRoot\.dotnet-sdk\dotnet.exe"
if (-not (Test-Path $dotnetExe)) {
    $dotnetExe = "dotnet"
}

Write-Host "Starting Notification API with .NET 10 ($dotnetExe)..." -ForegroundColor Cyan
& $dotnetExe run --project "$PSScriptRoot\src\NotificationApi\NotificationApi.csproj"
