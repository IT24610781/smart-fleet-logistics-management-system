@echo off
REM SE3112 ASE Take-Home - Load & Performance Testing (JMeter) live demo runner.
REM Usage:  run-fleet-stress-demo.bat <admin_password>
REM
REM Runs FleetVehicleStressTest.jmx in non-GUI mode (reliable, scriptable, CI-friendly)
REM and generates a browsable HTML dashboard report. Requires AuthUserService
REM (port 5100) and FleetVehicleService (port 5041) to already be running locally.

if "%~1"=="" (
  echo Usage: run-fleet-stress-demo.bat ^<admin_password^>
  exit /b 1
)

set TIMESTAMP=%date:~-4%%date:~4,2%%date:~7,2%-%time:~0,2%%time:~3,2%%time:~6,2%
set TIMESTAMP=%TIMESTAMP: =0%
set RESULTS_DIR=results\%TIMESTAMP%

REM JMeter's -o creates the leaf "dashboard" folder but not missing parent
REM folders, and RESULTS_DIR is a fresh timestamped path every run - so it
REM never exists yet. Create it upfront to avoid "folder does not exist and
REM parent folder is not writable".
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

REM Passed as an environment variable, not a -J command-line argument: jmeter.bat's
REM own batch-file argument parsing mangles "!" in -J values (a known Windows batch
REM delayed-expansion gotcha), which silently turns a password like "P@ssw0rd1!" into
REM an empty string. Reading it via System.getenv() in the JSR223 script avoids that.
set QA_ADMIN_PASSWORD=%~1

echo Running Fleet Vehicle Stress Test (baseline load + concurrency spike)...
REM "call" is required here: invoking another .bat file without it hands control
REM to jmeter.bat and does not return to this script afterward, so the "Done" /
REM dashboard-opening lines below would silently never run.
call "%JMETER_CMD%" -n -t FleetVehicleStressTest.jmx ^
  -l "%RESULTS_DIR%\results.jtl" ^
  -e -o "%RESULTS_DIR%\dashboard"

echo.
echo Done. Opening the HTML dashboard...
start "" "%RESULTS_DIR%\dashboard\index.html"
