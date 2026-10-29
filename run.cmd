@echo off
setlocal
set DOTNET_CLI_HOME=%~dp0.dotnet
set DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
set DOTNET_NOLOGO=1
set DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER=1

set DOTNET_EXE=%~dp0.dotnet-sdk\dotnet.exe
if not exist "%DOTNET_EXE%" (
    set DOTNET_EXE=dotnet
)

echo Starting Notification API with .NET 10 (%DOTNET_EXE%)...
"%DOTNET_EXE%" run --project "%~dp0src\NotificationApi\NotificationApi.csproj"
