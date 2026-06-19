REM Codex developer note: Builds, runs, and purges the Oyako full-stack Docker development container.
@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "ROOT=%~dp0"
set "IMAGE=oyako:latest"
set "CONTAINER=oyako-app"
set "LABEL=com.oyako.app=oyako"
set "API_DIR=%ROOT%webapi-oyako"
set "WEB_DIR=%ROOT%webapp-oyako"

where docker >nul 2>nul
if errorlevel 1 (
  echo Docker CLI was not found. Install Docker Desktop and ensure docker.exe is on PATH.
  exit /b 1
)

docker version >nul 2>nul
if errorlevel 1 (
  echo Docker Desktop is not reachable. Start Docker Desktop, then run this script again.
  exit /b 1
)

if not exist "%ROOT%azure-cloud.env" (
  echo Missing "%ROOT%azure-cloud.env". Rename azure.env to azure-cloud.env or create the Azure cloud env file.
  exit /b 1
)

if not exist "%ROOT%ollama-cloud.env" (
  echo Missing "%ROOT%ollama-cloud.env". Rename ollama.env to ollama-cloud.env or create the Ollama Cloud env file.
  exit /b 1
)

if exist "%ROOT%azure.env" echo Warning: legacy azure.env exists but Docker uses azure-cloud.env.
if exist "%ROOT%ollama.env" echo Warning: legacy ollama.env exists but Docker uses ollama-cloud.env.

echo Purging stale Oyako Docker containers and images...
call :purge_oyako

echo Building Oyako Web API locally...
pushd "%API_DIR%"
dotnet build
if errorlevel 1 (
  popd
  call :purge_oyako
  exit /b 1
)
popd

if not exist "%WEB_DIR%\node_modules" (
  echo Installing Oyako Web App dependencies...
  pushd "%WEB_DIR%"
  call npm ci
  if errorlevel 1 (
    popd
    call :purge_oyako
    exit /b 1
  )
  popd
)

echo Building Oyako Web App locally...
pushd "%WEB_DIR%"
call npm run build
if errorlevel 1 (
  popd
  call :purge_oyako
  exit /b 1
)
popd

echo Building Docker image %IMAGE%...
docker build --no-cache --force-rm --label "%LABEL%" --label "com.oyako.role=fullstack-docker-dev" -t "%IMAGE%" "%ROOT%"
if errorlevel 1 (
  call :purge_oyako
  exit /b 1
)

echo.
echo Starting Oyako Docker container in attached mode:
echo   Web/API: http://localhost:8080
echo.
echo Close this terminal or press Ctrl+C to stop the container. The script will best-effort purge Oyako Docker artifacts afterwards.
echo.

docker run --rm --init -it --name "%CONTAINER%" --label "%LABEL%" --label "com.oyako.role=fullstack-docker-dev" -p 8080:8080 --env-file "%ROOT%azure-cloud.env" --env-file "%ROOT%ollama-cloud.env" -e OYAKO_DOCKER=1 -e ASPNETCORE_ENVIRONMENT=Production -e ASPNETCORE_URLS=http://+:8080 -e Storage__DataRoot=/app/data -e Sqlite__ConnectionString="Data Source=/app/data/oyako.sqlite;Cache=Shared" -e Ai__DefaultProvider=ollama-cloud -e Ai__DisabledProviders__0=ollama-local --ipc=host "%IMAGE%"
set "RUN_EXIT=%ERRORLEVEL%"

if "%RUN_EXIT%"=="125" (
  echo Docker rejected the first run configuration. Retrying with --shm-size=1g instead of --ipc=host...
  docker run --rm --init -it --name "%CONTAINER%" --label "%LABEL%" --label "com.oyako.role=fullstack-docker-dev" -p 8080:8080 --env-file "%ROOT%azure-cloud.env" --env-file "%ROOT%ollama-cloud.env" -e OYAKO_DOCKER=1 -e ASPNETCORE_ENVIRONMENT=Production -e ASPNETCORE_URLS=http://+:8080 -e Storage__DataRoot=/app/data -e Sqlite__ConnectionString="Data Source=/app/data/oyako.sqlite;Cache=Shared" -e Ai__DefaultProvider=ollama-cloud -e Ai__DisabledProviders__0=ollama-local --shm-size=1g "%IMAGE%"
  set "RUN_EXIT=%ERRORLEVEL%"
)

echo Purging Oyako Docker artifacts...
call :purge_oyako

if not "%RUN_EXIT%"=="0" (
  echo Oyako Docker container exited with code %RUN_EXIT%.
  exit /b %RUN_EXIT%
)

echo Oyako Docker run finished and related Docker artifacts were purged.
exit /b 0

:purge_oyako
powershell -NoProfile -ExecutionPolicy Bypass -Command "$label='com.oyako.app=oyako'; $tag='oyako:latest'; docker ps -aq --filter ('label=' + $label) 2>$null | ForEach-Object { if($_){ docker rm -f $_ 2>$null | Out-Null } }; docker images -q --filter ('label=' + $label) 2>$null | Sort-Object -Unique | ForEach-Object { if($_){ docker image rm -f $_ 2>$null | Out-Null } }; docker image rm -f $tag 2>$null | Out-Null"
exit /b 0
