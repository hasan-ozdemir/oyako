REM Codex developer note: Documents the local startup automation for Oyako.
@echo off
REM Runs a scripted startup or cleanup step for the local full-stack app.
setlocal

REM Runs a scripted startup or cleanup step for the local full-stack app.
set "ROOT=%~dp0"
REM Runs a scripted startup or cleanup step for the local full-stack app.
set "API_DIR=%ROOT%webapi-oyako"
REM Runs a scripted startup or cleanup step for the local full-stack app.
set "WEB_DIR=%ROOT%webapp-oyako"

REM Runs a scripted startup or cleanup step for the local full-stack app.
if not exist "%API_DIR%\webapi-oyako.csproj" (
  REM Runs a scripted startup or cleanup step for the local full-stack app.
  echo Web API project was not found: "%API_DIR%\webapi-oyako.csproj"
  REM Runs a scripted startup or cleanup step for the local full-stack app.
  exit /b 1
REM Runs a scripted startup or cleanup step for the local full-stack app.
)

REM Runs a scripted startup or cleanup step for the local full-stack app.
if not exist "%WEB_DIR%\package.json" (
  REM Runs a scripted startup or cleanup step for the local full-stack app.
  echo Web app project was not found: "%WEB_DIR%\package.json"
  REM Runs a scripted startup or cleanup step for the local full-stack app.
  exit /b 1
REM Runs a scripted startup or cleanup step for the local full-stack app.
)

REM Runs a scripted startup or cleanup step for the local full-stack app.
set "OYAKO_ROOT=%ROOT%"
REM Runs a scripted startup or cleanup step for the local full-stack app.
echo Closing stale Oyako terminal shells...
REM Runs a scripted startup or cleanup step for the local full-stack app.
powershell -NoProfile -ExecutionPolicy Bypass -Command "$titles=@('oyako-webapi','oyako-webapp'); Get-Process cmd,powershell,pwsh -ErrorAction SilentlyContinue | Where-Object { $windowTitle=$_.MainWindowTitle; $windowTitle -and ($titles | Where-Object { $windowTitle.IndexOf($_, [StringComparison]::OrdinalIgnoreCase) -ge 0 }) } | ForEach-Object { Write-Host ('Closing stale Oyako terminal PID ' + $_.Id + ' (' + $_.MainWindowTitle + ')'); Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue }"
REM Runs a scripted startup or cleanup step for the local full-stack app.
echo Stopping existing Oyako processes on ports 3000, 5000 and 5001...
REM Runs a scripted startup or cleanup step for the local full-stack app.
powershell -NoProfile -ExecutionPolicy Bypass -Command "$root=(Resolve-Path $env:OYAKO_ROOT).Path.TrimEnd('\'); $ports=3000,5000,5001; $listeners=Get-NetTCPConnection -LocalPort $ports -State Listen -ErrorAction SilentlyContinue; foreach($listener in $listeners){ $process=Get-Process -Id $listener.OwningProcess -ErrorAction SilentlyContinue; if(-not $process){ continue }; $commandLine=(Get-CimInstance Win32_Process -Filter ('ProcessId=' + $process.Id) -ErrorAction SilentlyContinue).CommandLine; $path=$process.Path; $isOyako=($process.ProcessName -eq 'webapi-oyako') -or ($path -and $path.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) -or ($commandLine -and $commandLine.IndexOf($root, [StringComparison]::OrdinalIgnoreCase) -ge 0); $isRestartable=$process.ProcessName -in @('webapi-oyako','dotnet','node','npm','vite'); if($isOyako -and $isRestartable){ Write-Host ('Stopping ' + $process.ProcessName + ' PID ' + $process.Id + ' on port ' + $listener.LocalPort); Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue } }; $deadline=(Get-Date).AddSeconds(10); do { Start-Sleep -Milliseconds 300; $busy=Get-NetTCPConnection -LocalPort $ports -State Listen -ErrorAction SilentlyContinue | Sort-Object LocalPort,OwningProcess -Unique } while($busy -and (Get-Date) -lt $deadline); if($busy){ Write-Host 'Required port(s) are still in use by non-Oyako or locked processes:'; foreach($item in $busy){ $p=Get-Process -Id $item.OwningProcess -ErrorAction SilentlyContinue; Write-Host ('  port ' + $item.LocalPort + ' -> ' + $p.ProcessName + ' PID ' + $item.OwningProcess) }; exit 1 }"
REM Runs a scripted startup or cleanup step for the local full-stack app.
if errorlevel 1 (
  REM Runs a scripted startup or cleanup step for the local full-stack app.
  echo Stop the listed process or free the listed ports, then run this script again.
  REM Runs a scripted startup or cleanup step for the local full-stack app.
  exit /b 1
REM Runs a scripted startup or cleanup step for the local full-stack app.
)

REM Runs a scripted startup or cleanup step for the local full-stack app.
if not exist "%WEB_DIR%\node_modules" (
  REM Runs a scripted startup or cleanup step for the local full-stack app.
  echo Installing web app dependencies...
  REM Runs a scripted startup or cleanup step for the local full-stack app.
  pushd "%WEB_DIR%"
  REM Runs a scripted startup or cleanup step for the local full-stack app.
  call npm ci
  REM Runs a scripted startup or cleanup step for the local full-stack app.
  if errorlevel 1 (
    REM Runs a scripted startup or cleanup step for the local full-stack app.
    popd
    REM Runs a scripted startup or cleanup step for the local full-stack app.
    exit /b 1
  REM Runs a scripted startup or cleanup step for the local full-stack app.
  )
  REM Runs a scripted startup or cleanup step for the local full-stack app.
  popd
REM Runs a scripted startup or cleanup step for the local full-stack app.
)

REM Runs a scripted startup or cleanup step for the local full-stack app.
echo Preparing Web API and Playwright browser runtime...
REM Runs a scripted startup or cleanup step for the local full-stack app.
pushd "%API_DIR%"
REM Runs a scripted startup or cleanup step for the local full-stack app.
dotnet build
REM Runs a scripted startup or cleanup step for the local full-stack app.
if errorlevel 1 (
  REM Runs a scripted startup or cleanup step for the local full-stack app.
  popd
  REM Runs a scripted startup or cleanup step for the local full-stack app.
  exit /b 1
REM Runs a scripted startup or cleanup step for the local full-stack app.
)
REM Runs a scripted startup or cleanup step for the local full-stack app.
if exist "%API_DIR%\bin\Debug\net10.0\playwright.ps1" (
  REM Runs a scripted startup or cleanup step for the local full-stack app.
  powershell -NoProfile -ExecutionPolicy Bypass -File "%API_DIR%\bin\Debug\net10.0\playwright.ps1" install chromium
  REM Runs a scripted startup or cleanup step for the local full-stack app.
  if errorlevel 1 (
    REM Runs a scripted startup or cleanup step for the local full-stack app.
    popd
    REM Runs a scripted startup or cleanup step for the local full-stack app.
    exit /b 1
  REM Runs a scripted startup or cleanup step for the local full-stack app.
  )
REM Runs a scripted startup or cleanup step for the local full-stack app.
) else (
  REM Runs a scripted startup or cleanup step for the local full-stack app.
  echo Playwright installer script was not found. The API may fail to crawl rendered pages.
REM Runs a scripted startup or cleanup step for the local full-stack app.
)
REM Runs a scripted startup or cleanup step for the local full-stack app.
popd

REM Runs a scripted startup or cleanup step for the local full-stack app.
echo Starting Oyako Web API on http://localhost:5000 and https://localhost:5001
REM Runs a scripted startup or cleanup step for the local full-stack app.
start "oyako-webapi" /D "%API_DIR%" cmd /c "set ASPNETCORE_ENVIRONMENT=Development&& dotnet run --no-launch-profile --no-build"

REM Runs a scripted startup or cleanup step for the local full-stack app.
echo Waiting for Oyako Web API to become available on http://localhost:5000...
REM Runs a scripted startup or cleanup step for the local full-stack app.
powershell -NoProfile -ExecutionPolicy Bypass -Command "$deadline=(Get-Date).AddSeconds(90); $ready=$false; do { try { $response=Invoke-WebRequest -Uri 'http://localhost:5000/api/api-health' -UseBasicParsing -TimeoutSec 3; if($response.StatusCode -eq 200){ $ready=$true } } catch { Start-Sleep -Milliseconds 750 } } while(-not $ready -and (Get-Date) -lt $deadline); if($ready){ Write-Host 'Oyako Web API is ready on http://localhost:5000.'; exit 0 }; Write-Host 'Oyako Web API did not become ready on http://localhost:5000 within 90 seconds.'; exit 1"
REM Runs a scripted startup or cleanup step for the local full-stack app.
if errorlevel 1 (
  REM Runs a scripted startup or cleanup step for the local full-stack app.
  echo The web app was not started because the Web API readiness check failed.
  REM Runs a scripted startup or cleanup step for the local full-stack app.
  exit /b 1
REM Runs a scripted startup or cleanup step for the local full-stack app.
)

REM Runs a scripted startup or cleanup step for the local full-stack app.
echo Starting Oyako Web App on http://localhost:3000
REM Runs a scripted startup or cleanup step for the local full-stack app.
start "oyako-webapp" /D "%WEB_DIR%" cmd /c "set VITE_API_BASE=/api&& npm run dev -- --host 0.0.0.0 --port 3000"

REM Runs a scripted startup or cleanup step for the local full-stack app.
echo Waiting for Oyako Web App to become available on http://localhost:3000...
REM Runs a scripted startup or cleanup step for the local full-stack app.
powershell -NoProfile -ExecutionPolicy Bypass -Command "$deadline=(Get-Date).AddSeconds(45); $ready=$false; do { $client=[System.Net.Sockets.TcpClient]::new(); try { $task=$client.ConnectAsync('127.0.0.1', 3000); if($task.Wait(500) -and $client.Connected){ $ready=$true } } catch { } finally { $client.Dispose() }; if(-not $ready){ Start-Sleep -Milliseconds 500 } } while(-not $ready -and (Get-Date) -lt $deadline); if($ready){ Write-Host 'Opening http://localhost:3000 in the default browser...'; Start-Process 'http://localhost:3000'; exit 0 }; Write-Host 'The web app did not become ready on http://localhost:3000 within 45 seconds.'; exit 1"
REM Runs a scripted startup or cleanup step for the local full-stack app.
if errorlevel 1 (
  REM Runs a scripted startup or cleanup step for the local full-stack app.
  echo The browser was not opened because the web app readiness check did not pass yet.
  REM Runs a scripted startup or cleanup step for the local full-stack app.
  echo The web app may still be starting; if needed, open http://localhost:3000 manually.
REM Runs a scripted startup or cleanup step for the local full-stack app.
) else (
  REM Runs a scripted startup or cleanup step for the local full-stack app.
  echo Opened http://localhost:3000 in the default browser.
REM Runs a scripted startup or cleanup step for the local full-stack app.
)

REM Runs a scripted startup or cleanup step for the local full-stack app.
echo.
REM Runs a scripted startup or cleanup step for the local full-stack app.
echo Oyako is starting:
REM Runs a scripted startup or cleanup step for the local full-stack app.
echo   API:     http://localhost:5000
REM Runs a scripted startup or cleanup step for the local full-stack app.
echo   API TLS: https://localhost:5001
REM Runs a scripted startup or cleanup step for the local full-stack app.
echo   Web:     http://localhost:3000
REM Runs a scripted startup or cleanup step for the local full-stack app.
echo.
REM Runs a scripted startup or cleanup step for the local full-stack app.
echo Service terminal windows close automatically when their processes stop.
REM Runs a scripted startup or cleanup step for the local full-stack app.
echo Close a service terminal window if you want to stop that service manually.

REM Runs a scripted startup or cleanup step for the local full-stack app.
endlocal
