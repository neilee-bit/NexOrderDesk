@echo off
REM Builds NexOrderDesk 2.7 and writes any errors to build_errors.txt
cd /d "%~dp0"
dotnet --version
dotnet build "NexOrderDesk 2.7.sln" -c Debug > build_log.txt 2>&1
findstr /C:"error" build_log.txt > build_errors.txt
if exist "src\NexOrderDesk\bin\Debug\net8.0-windows\NexOrderDesk.exe" (
  echo BUILD OK - starting app
  start "" "src\NexOrderDesk\bin\Debug\net8.0-windows\NexOrderDesk.exe"
) else (
  echo BUILD FAILED - see build_errors.txt and send it to me
  type build_errors.txt
)
pause
