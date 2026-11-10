@echo off
REM ============================================================================
REM  NexOrderDesk - make a stand-alone program (one .exe file)
REM  Result: publish\NexOrderDesk.exe
REM  Copy that one file to any Windows 10/11 64-bit PC and double-click it.
REM  The other PC does NOT need .NET installed.
REM  Needs: .NET 8 SDK on THIS PC + internet the first time.
REM ============================================================================
cd /d "%~dp0"
echo Making the stand-alone NexOrderDesk.exe ... (1-3 minutes)
if exist publish rmdir /s /q publish

dotnet publish "src\NexOrderDesk\NexOrderDesk.csproj" -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true ^
  -p:DebugType=none -o publish > publish_log.txt 2>&1

if exist "publish\NexOrderDesk.exe" (
  echo.
  echo DONE:  %~dp0publish\NexOrderDesk.exe
  echo Copy that file anywhere ^(desktop, USB, office PCs^) and double-click it.
  explorer "%~dp0publish"
) else (
  echo.
  echo PUBLISH FAILED - send me publish_errors.txt
  findstr /C:"error" publish_log.txt > publish_errors.txt
  type publish_errors.txt
)
pause
