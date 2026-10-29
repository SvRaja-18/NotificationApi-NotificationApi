# Test runner script for Notification Processing API (.NET 10)
$env:DOTNET_CLI_HOME = "$PSScriptRoot\.dotnet"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_NOLOGO = "1"
$env:DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER = "1"

$dotnetExe = "dotnet"
if (-not (Get-Command "dotnet" -ErrorAction SilentlyContinue)) {
    if (Test-Path "$PSScriptRoot\.dotnet-sdk\dotnet.exe") {
        $dotnetExe = "$PSScriptRoot\.dotnet-sdk\dotnet.exe"
    }
}

Write-Host "Running tests with .NET 10 ($dotnetExe)..." -ForegroundColor Cyan
& $dotnetExe test "$PSScriptRoot\tests\NotificationApi.Tests\NotificationApi.Tests.csproj" @args
