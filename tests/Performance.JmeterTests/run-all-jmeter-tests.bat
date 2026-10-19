@echo off
REM SE3112 ASE Take-Home - OPTIONAL "run the whole suite" script.
REM
REM This is NOT for the timed 2-minute live demo (use run-fleet-stress-demo.bat
REM for that - it's fast and purpose-built for the "under stress/high concurrency"
REM requirement). This script is a backup for VIVA, in case you're asked to show
REM the rest of the project's JMeter coverage too. It runs sequentially and will
REM take a few minutes (fixture prep + 4 separate JMeter runs + dashboards).
REM
REM Runs, in order:
REM   1. AuthPerformanceTest.jmx          (no credentials needed - generates its own QA user)
REM   2. FleetVehicleStressTest.jmx       (baseline + stress spike - supersedes the old
REM                                         FleetVehiclePerformanceTest.jmx, which is
REM                                         skipped here because it has a hardcoded
REM                                         placeholder "Bearer YOUR_TOKEN_HERE" and
REM                                         was never wired for auto-login)
REM   3. TripDispatchPerformanceTest.jmx  (fixtures regenerated first)
REM   4. TrackingPerformanceTest.jmx      (fixtures regenerated first; admin report
REM                                         sampler included if the password works)
REM
REM Usage: run-all-jmeter-tests.bat <admin_password>

if "%~1"=="" (
  echo Usage: run-all-jmeter-tests.bat ^<admin_password^>
  exit /b 1
)

set TIMESTAMP=%date:~-4%%date:~4,2%%date:~7,2%-%time:~0,2%%time:~3,2%%time:~6,2%
set TIMESTAMP=%TIMESTAMP: =0%
set RESULTS_DIR=results\all-%TIMESTAMP%
mkdir "%RESULTS_DIR%" >nul 2>nul

REM Locate jmeter.bat: prefer PATH, fall back to this machine's known install location.
where jmeter.bat >nul 2>nul
if %ERRORLEVEL%==0 (
  set JMETER_CMD=jmeter.bat
) else (
  set JMETER_CMD=C:\Users\ranju\Downloads\apache-jmeter-5.6.3\apache-jmeter-5.6.3\bin\jmeter.bat
)
if not exist "%JMETER_CMD%" (
  echo Could not find jmeter.bat on PATH or at %JMETER_CMD%
  echo Edit JMETER_CMD at the top of this script to your actual jmeter.bat location.
  exit /b 1
)

REM Locate a Python launcher: try "python" first, fall back to the "py" launcher.
where python >nul 2>nul
if %ERRORLEVEL%==0 (
  set PYTHON_CMD=python
) else (
  set PYTHON_CMD=py
)

REM Passed as an environment variable, not a -J command-line argument: jmeter.bat's
REM own batch-file argument parsing mangles "!" in -J values on Windows, which can
REM silently turn a password like "P@ssw0rd1!" into an empty string. FleetVehicleStressTest.jmx
REM reads this via System.getenv(). The other plans below either don't need a password
REM or read one from a fixtures file, so they aren't affected by this issue.
set QA_ADMIN_PASSWORD=%~1

echo ============================================
echo [1/4] Auth Performance Test
echo ============================================
mkdir "%RESULTS_DIR%\auth" >nul 2>nul
REM "call" is required on every jmeter.bat invocation below: without it, control
REM never returns to this script after the first one and everything after it
REM (steps 2-4, fixture prep, dashboard opening) silently never runs.
call "%JMETER_CMD%" -n -t AuthPerformanceTest.jmx ^
  -l "%RESULTS_DIR%\auth\results.jtl" -e -o "%RESULTS_DIR%\auth\dashboard"

echo ============================================
echo [2/4] Fleet Vehicle Stress Test (baseline + spike)
echo ============================================
mkdir "%RESULTS_DIR%\fleet-stress" >nul 2>nul
call "%JMETER_CMD%" -n -t FleetVehicleStressTest.jmx ^
  -l "%RESULTS_DIR%\fleet-stress\results.jtl" -e -o "%RESULTS_DIR%\fleet-stress\dashboard"

echo ============================================
echo Preparing Trip Dispatch fixtures...
echo ============================================
%PYTHON_CMD% prepare_trip_fixtures.py --output trip-fixtures.json
if %ERRORLEVEL% NEQ 0 (
  echo Trip fixtures failed - skipping Trip Dispatch test.
  goto tracking
)
echo ============================================
echo [3/4] Trip Dispatch Performance Test
echo ============================================
mkdir "%RESULTS_DIR%\trip" >nul 2>nul
call "%JMETER_CMD%" -n -t TripDispatchPerformanceTest.jmx -Jqa.fixtures=trip-fixtures.json ^
  -l "%RESULTS_DIR%\trip\results.jtl" -e -o "%RESULTS_DIR%\trip\dashboard"

:tracking
echo ============================================
echo Preparing Tracking fixtures...
echo ============================================
%PYTHON_CMD% prepare_tracking_fixtures.py --output tracking-fixtures.json
if %ERRORLEVEL% NEQ 0 (
  echo Tracking fixtures failed - skipping Tracking test.
  goto summary
)
echo ============================================
echo [4/4] Tracking Performance Test
echo ============================================
mkdir "%RESULTS_DIR%\tracking" >nul 2>nul
call "%JMETER_CMD%" -n -t TrackingPerformanceTest.jmx -Jqa.fixtures=tracking-fixtures.json -Jqa.adminPassword=%QA_ADMIN_PASSWORD% ^
  -l "%RESULTS_DIR%\tracking\results.jtl" -e -o "%RESULTS_DIR%\tracking\dashboard"

:summary
echo.
echo ============================================
echo All runs complete. Merging into one combined report...
echo ============================================
REM Each plan uses its own distinct sample labels (Login, Create Vehicle,
REM Spike: Create Vehicle, etc.), so concatenating the .jtl files - keeping
REM only the first file's header row - and generating one dashboard from the
REM merged file gives a single Statistics table covering every plan that ran.
set COMBINED_JTL=%RESULTS_DIR%\combined.jtl
set FIRST_JTL=
if exist "%RESULTS_DIR%\auth\results.jtl" set FIRST_JTL=%RESULTS_DIR%\auth\results.jtl
if "%FIRST_JTL%"=="" if exist "%RESULTS_DIR%\fleet-stress\results.jtl" set FIRST_JTL=%RESULTS_DIR%\fleet-stress\results.jtl
if "%FIRST_JTL%"=="" if exist "%RESULTS_DIR%\trip\results.jtl" set FIRST_JTL=%RESULTS_DIR%\trip\results.jtl
if "%FIRST_JTL%"=="" if exist "%RESULTS_DIR%\tracking\results.jtl" set FIRST_JTL=%RESULTS_DIR%\tracking\results.jtl

if "%FIRST_JTL%"=="" (
  echo No results.jtl files found - nothing to combine.
  goto done
)

copy /y "%FIRST_JTL%" "%COMBINED_JTL%" >nul
for %%P in (auth fleet-stress trip tracking) do (
  if exist "%RESULTS_DIR%\%%P\results.jtl" if not "%RESULTS_DIR%\%%P\results.jtl"=="%FIRST_JTL%" (
    more +1 "%RESULTS_DIR%\%%P\results.jtl" >> "%COMBINED_JTL%"
  )
)

call "%JMETER_CMD%" -g "%COMBINED_JTL%" -o "%RESULTS_DIR%\combined-dashboard"

:done
echo.
echo ============================================
echo Done. Results in %RESULTS_DIR%
echo ============================================
echo Opening the combined dashboard...
if exist "%RESULTS_DIR%\combined-dashboard\index.html" start "" "%RESULTS_DIR%\combined-dashboard\index.html"
echo (Per-plan dashboards are also available under %RESULTS_DIR%\^<plan^>\dashboard\index.html if needed.)
