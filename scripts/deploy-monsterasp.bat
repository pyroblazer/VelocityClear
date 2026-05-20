@echo off
setlocal

set MSDEPLOY="C:\Program Files (x86)\IIS\Microsoft Web Deploy V3\msdeploy.exe"
set PUBLISH_DIR=C:\Users\ROG Flow X16\Documents\github\VelocityClear\backend\src\FinancialPlatform.AllServices\publish
set SITE=site69774
set SERVER=https://site69774.siteasp.net:8172/msdeploy.axd?site=site69774
set USER=site69774
set "PASS=Ar8+g4!K7=Qc"

if not exist "%PUBLISH_DIR%" (
  echo Error: Publish directory not found: %PUBLISH_DIR%
  echo Run: dotnet publish backend/src/FinancialPlatform.AllServices --configuration Release --runtime win-x86 --output backend/src/FinancialPlatform.AllServices/publish
  exit /b 1
)

echo Publishing to MonsterASP.net...
echo   Site: %SITE%
echo   Server: %SERVER%
echo   Source: %PUBLISH_DIR%
echo.

%MSDEPLOY% -source:contentPath="%PUBLISH_DIR%" -dest:contentPath="%SITE%",computerName="%SERVER%",userName="%USER%",password="%PASS%",authtype="Basic" -verb:sync -allowUntrusted -enableRule:DoNotDeleteRule -enableRule:AppOffline

if %ERRORLEVEL% EQU 0 (
  echo.
  echo Deploy complete!
) else (
  echo.
  echo Deploy failed with error %ERRORLEVEL%
)

endlocal
