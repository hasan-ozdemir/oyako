@echo off
setlocal EnableExtensions EnableDelayedExpansion
chcp 65001 >nul

set "ROOT=%~dp0"
set "API_DIR=%ROOT%webapi-oyako"
set "WEB_DIR=%ROOT%webapp-oyako"
set "TENANT_NAME=oyakdijital"
set "OPEN_BROWSER=1"

:parse_args
if "%~1"=="" goto args_done
if /I "%~1"=="--tenant-name" (
  if "%~2"=="" (
    echo Missing value after --tenant-name.
    exit /b 1
  )
  set "TENANT_NAME=%~2"
  shift
  shift
  goto parse_args
)
if /I "%~1"=="-t" (
  if "%~2"=="" (
    echo Missing value after -t.
    exit /b 1
  )
  set "TENANT_NAME=%~2"
  shift
  shift
  goto parse_args
)
if /I "%~1"=="--no-browser" (
  set "OPEN_BROWSER=0"
  shift
  goto parse_args
)
if /I "%~1"=="--help" goto usage
echo Unsupported argument: %~1
goto usage_error

:usage
echo Usage: run-app.cmd [--tenant-name ^<tenant^> ^| -t ^<tenant^>] [--no-browser]
echo Default tenant: oyakdijital
exit /b 0

:usage_error
echo Usage: run-app.cmd [--tenant-name ^<tenant^> ^| -t ^<tenant^>] [--no-browser]
exit /b 1

:args_done
set "OYAKO_ROOT=%ROOT%"
set "OYAKO_TENANT_NAME=%TENANT_NAME%"

if not exist "%API_DIR%\webapi-oyako.csproj" (
  echo Web API project was not found: "%API_DIR%\webapi-oyako.csproj"
  exit /b 1
)

if not exist "%WEB_DIR%\package.json" (
  echo Web app project was not found: "%WEB_DIR%\package.json"
  exit /b 1
)

where dotnet >nul 2>nul
if errorlevel 1 (
  echo dotnet SDK was not found on PATH.
  exit /b 1
)

where npm >nul 2>nul
if errorlevel 1 (
  echo npm was not found on PATH.
  exit /b 1
)

echo Validating tenant "%TENANT_NAME%"...
powershell -NoProfile -ExecutionPolicy Bypass -Command "$ErrorActionPreference='Stop'; $OutputEncoding=[Text.Encoding]::UTF8; [Console]::OutputEncoding=[Text.Encoding]::UTF8; $tenant=$env:OYAKO_TENANT_NAME; if($tenant -notmatch '^[a-z0-9][a-z0-9-]*[a-z0-9]$|^[a-z0-9]$'){ throw ('Invalid tenant name: ' + $tenant) }; $path=Join-Path $env:OYAKO_ROOT ('.tenants\' + $tenant + '.env'); if(-not (Test-Path -LiteralPath $path)){ throw ('Tenant env file was not found: ' + $path) }; $map=@{}; foreach($line in Get-Content -Encoding UTF8 -LiteralPath $path){ $trim=$line.Trim(); if($trim.Length -eq 0 -or $trim.StartsWith('#')){ continue }; $idx=$trim.IndexOf('='); if($idx -le 0){ continue }; $map[$trim.Substring(0,$idx).Trim()]=$trim.Substring($idx+1).Trim().Trim([char]34).Trim([char]39) }; if($map['tenant_name'] -ne $tenant){ throw ('Tenant file declares tenant_name=' + $map['tenant_name'] + ', expected ' + $tenant) }; if(-not $map['tenant_display_name']){ throw 'tenant_display_name is required.' }; Write-Host ('Tenant ready: ' + $tenant + ' -> ' + $map['tenant_display_name'])"
if errorlevel 1 exit /b 1

echo Closing stale Oyako terminal shells...
powershell -NoProfile -ExecutionPolicy Bypass -Command "$ErrorActionPreference='Stop'; $OutputEncoding=[Text.Encoding]::UTF8; [Console]::OutputEncoding=[Text.Encoding]::UTF8; $titles=@('oyako-webapi','oyako-webapp'); Get-Process cmd,powershell,pwsh -ErrorAction SilentlyContinue | Where-Object { $title=$_.MainWindowTitle; $title -and ($titles | Where-Object { $title.IndexOf($_, [StringComparison]::OrdinalIgnoreCase) -ge 0 }) } | ForEach-Object { Write-Host ('Stopping stale Oyako terminal PID ' + $_.Id + ' (' + $_.MainWindowTitle + ')'); Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue }"
if errorlevel 1 exit /b 1

echo Freeing local Oyako ports 3000, 5000 and 5001...
powershell -NoProfile -ExecutionPolicy Bypass -Command "$ErrorActionPreference='Stop'; $OutputEncoding=[Text.Encoding]::UTF8; [Console]::OutputEncoding=[Text.Encoding]::UTF8; $root=(Resolve-Path $env:OYAKO_ROOT).Path.TrimEnd('\'); $ports=3000,5000,5001; $listeners=Get-NetTCPConnection -LocalPort $ports -State Listen -ErrorAction SilentlyContinue; foreach($listener in $listeners){ $process=Get-Process -Id $listener.OwningProcess -ErrorAction SilentlyContinue; if(-not $process){ continue }; $commandLine=(Get-CimInstance Win32_Process -Filter ('ProcessId=' + $process.Id) -ErrorAction SilentlyContinue).CommandLine; $path=$process.Path; $isOyako=($process.ProcessName -eq 'webapi-oyako') -or ($path -and $path.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) -or ($commandLine -and $commandLine.IndexOf($root, [StringComparison]::OrdinalIgnoreCase) -ge 0); $isRestartable=$process.ProcessName -in @('webapi-oyako','dotnet','node','npm','vite'); if($isOyako -and $isRestartable){ Write-Host ('Stopping ' + $process.ProcessName + ' PID ' + $process.Id + ' on port ' + $listener.LocalPort); Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue } }; $deadline=(Get-Date).AddSeconds(10); do { Start-Sleep -Milliseconds 300; $busy=Get-NetTCPConnection -LocalPort $ports -State Listen -ErrorAction SilentlyContinue | Sort-Object LocalPort,OwningProcess -Unique } while($busy -and (Get-Date) -lt $deadline); if($busy){ Write-Host 'Required port(s) are still in use:'; foreach($item in $busy){ $p=Get-Process -Id $item.OwningProcess -ErrorAction SilentlyContinue; Write-Host ('  port ' + $item.LocalPort + ' -> ' + $p.ProcessName + ' PID ' + $item.OwningProcess) }; exit 1 }"
if errorlevel 1 exit /b 1

if not exist "%WEB_DIR%\node_modules" (
  echo Installing web app dependencies with npm ci...
  pushd "%WEB_DIR%"
  call npm ci
  if errorlevel 1 (
    popd
    exit /b 1
  )
  popd
)

echo Building Web API...
dotnet build "%API_DIR%\webapi-oyako.csproj" --nologo
if errorlevel 1 exit /b 1

set "PLAYWRIGHT_SCRIPT=%API_DIR%\bin\Debug\net10.0\playwright.ps1"
if exist "%PLAYWRIGHT_SCRIPT%" (
  echo Ensuring Playwright Chromium runtime is available with 120s timeout...
  powershell -NoProfile -ExecutionPolicy Bypass -Command "$ErrorActionPreference='Stop'; $script=$env:PLAYWRIGHT_SCRIPT; $process=Start-Process -FilePath 'powershell' -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-File',$script,'install','chromium') -NoNewWindow -PassThru; if(-not $process.WaitForExit(120000)){ Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue; throw 'Playwright Chromium install timed out after 120 seconds.' }; exit $process.ExitCode"
  if errorlevel 1 exit /b 1
) else (
  echo Playwright installer script was not found; crawler browser health may fail until the project is rebuilt.
)

echo Starting Web API for tenant "%TENANT_NAME%" on http://localhost:5000...
start "oyako-webapi-%TENANT_NAME%" /D "%API_DIR%" cmd /k "chcp 65001>nul && set ASPNETCORE_ENVIRONMENT=Development&& set OYAKO_TENANT_NAME=%TENANT_NAME%&& dotnet run --no-launch-profile --no-build"

echo Waiting for Web API tenant configuration...
powershell -NoProfile -ExecutionPolicy Bypass -Command "$ErrorActionPreference='Stop'; $tenant=$env:OYAKO_TENANT_NAME; $deadline=(Get-Date).AddSeconds(90); do { try { $health=Invoke-RestMethod -Uri 'http://localhost:5000/health' -TimeoutSec 3; $config=Invoke-RestMethod -Uri 'http://localhost:5000/api/tenant-config' -TimeoutSec 3; if($health.status -eq 'ok' -and $config.tenantName -eq $tenant){ Write-Host ('Web API ready for tenant ' + $config.tenantName + ' (' + $config.tenantDisplayName + ').'); exit 0 } } catch { Start-Sleep -Milliseconds 750 } } while((Get-Date) -lt $deadline); throw ('Web API did not become ready for tenant ' + $tenant + ' within 90 seconds.')"
if errorlevel 1 (
  echo The web app was not started because the Web API readiness check failed.
  exit /b 1
)

echo Starting Web App on http://localhost:3000...
start "oyako-webapp-%TENANT_NAME%" /D "%WEB_DIR%" cmd /k "chcp 65001>nul && set VITE_API_BASE=/api&& npm run dev -- --host 0.0.0.0 --port 3000"

echo Waiting for Web App and proxied tenant configuration...
powershell -NoProfile -ExecutionPolicy Bypass -Command "$ErrorActionPreference='Stop'; $tenant=$env:OYAKO_TENANT_NAME; $deadline=(Get-Date).AddSeconds(60); do { try { $root=Invoke-WebRequest -Uri 'http://localhost:3000/' -UseBasicParsing -TimeoutSec 3; $config=Invoke-RestMethod -Uri 'http://localhost:3000/api/tenant-config' -TimeoutSec 3; if($root.StatusCode -eq 200 -and $config.tenantName -eq $tenant){ Write-Host ('Web App ready for tenant ' + $config.tenantName + ' (' + $config.uiWebAssistantName + ').'); exit 0 } } catch { Start-Sleep -Milliseconds 750 } } while((Get-Date) -lt $deadline); throw ('Web App did not become ready for tenant ' + $tenant + ' within 60 seconds.')"
if errorlevel 1 exit /b 1

if "%OPEN_BROWSER%"=="1" (
  echo Opening http://localhost:3000 in the default browser...
  start "" "http://localhost:3000"
)

echo.
echo Oyako local stack is running:
echo   Tenant:  %TENANT_NAME%
echo   API:     http://localhost:5000
echo   API TLS: https://localhost:5001
echo   Web:     http://localhost:3000
echo.
echo Close the oyako-webapi-%TENANT_NAME% and oyako-webapp-%TENANT_NAME% terminal windows to stop the services.

endlocal
