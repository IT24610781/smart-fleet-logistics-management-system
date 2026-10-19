@echo off
cd /d C:\Users\chamo\Documents\smart-fleet-logistics-management-system
set QA_RUN_SPRINT3_UI=1
set QA_RUN_SPRINT4_UI=1
for /f "delims=" %%p in ('powershell -NoProfile -Command "$s=Read-Host -AsSecureString 'Admin password'; [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($s))"') do set QA_ADMIN_PASSWORD=%%p
cls
echo ===== SPRINT 1: Auth =====
dotnet test tests\Frontend.SeleniumTests --no-build --filter "FullyQualifiedName~Login_ValidCredentials" --logger "console;verbosity=normal"
echo ===== SPRINT 2: Fleet vehicles =====
dotnet test tests\Frontend.SeleniumTests --no-build --filter "FullyQualifiedName~CreateVehicle_ValidDetails" --logger "console;verbosity=normal"
echo ===== SPRINT 3: Trip dispatch =====
dotnet test tests\Frontend.SeleniumTests --no-build --filter "FullyQualifiedName~TripLifecycle_StatusUpdate" --logger "console;verbosity=normal"
echo ===== SPRINT 4: Reports and access control =====
dotnet test tests\Frontend.SeleniumTests --no-build --filter "FullyQualifiedName~RP_02_Dispatcher" --logger "console;verbosity=normal"
