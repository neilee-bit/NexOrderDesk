@echo off
REM ============================================================================
REM  NEX ORDER DESK - build a complete release on THIS PC (both MSIs + update.json + SHA256SUMS.txt)
REM  Version / repository come from Directory.Build.props. Result: the "release" folder.
REM  (On GitHub the same script runs automatically when you push a version tag - see RELEASING.md.)
REM ============================================================================
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "tools\build-release.ps1" %*
if errorlevel 1 (
  echo.
  echo RELEASE BUILD FAILED - see the red message above.
) else (
  explorer "%~dp0release"
)
pause
