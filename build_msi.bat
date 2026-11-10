@echo off
REM ============================================================================
REM  NEX ORDER DESK - make one installer quickly (version from Directory.Build.props)
REM    build_msi.bat        -> release\NexOrderDesk_<version>_Setup.msi       (light, 2-4 MB, needs .NET 8 Desktop Runtime)
REM    build_msi.bat full   -> release\NexOrderDesk_<version>_Setup_Full.msi  (self-contained, 60-70 MB)
REM  For a complete online release (both MSIs + update.json) use build_release.bat
REM ============================================================================
cd /d "%~dp0"
set VARIANT=light
if /i "%~1"=="full" set VARIANT=full
powershell -NoProfile -ExecutionPolicy Bypass -File "tools\build-release.ps1" -Variants %VARIANT% -SkipSetup
if errorlevel 1 (
  echo.
  echo FAILED - see the red message above.
) else (
  explorer "%~dp0release"
)
pause
