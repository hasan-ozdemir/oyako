@echo off
REM Codex developer note: Builds, runs, and purges the Oyako full-stack Docker development container.
setlocal EnableExtensions EnableDelayedExpansion
chcp 65001 >nul

set "ROOT=%~dp0"
set "IMAGE="
set "CONTAINER="
set "LABEL=com.oyako.app=oyako"
set "API_DIR=%ROOT%webapi-oyako"
set "WEB_DIR=%ROOT%webapp-oyako"
set "TENANT_NAME="

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
echo Unsupported argument: %~1
echo Usage: run-docker.cmd [--tenant-name ^<tenant^> ^| -t ^<tenant^>]
exit /b 1

:args_done
for /f "usebackq delims=" %%A in (`powershell -NoProfile -ExecutionPolicy Bypass -File "%ROOT%scripts\resolve-default-tenant.ps1" -Root "%ROOT%." -ExplicitTenantName "%TENANT_NAME%"`) do set "TENANT_NAME=%%A"
if errorlevel 1 exit /b 1

echo Validating tenant "%TENANT_NAME%"...
for /f "usebackq tokens=1,* delims==" %%A in (`powershell -NoProfile -ExecutionPolicy Bypass -Command "$ErrorActionPreference='Stop'; $OutputEncoding=[Text.Encoding]::UTF8; [Console]::OutputEncoding=[Text.Encoding]::UTF8; $tenant=$env:TENANT_NAME; if($tenant -notmatch '^[a-z0-9][a-z0-9-]*[a-z0-9]$|^[a-z0-9]$'){ throw ('Invalid tenant name: ' + $tenant) }; $root=Join-Path $env:ROOT '.tenants'; if(-not (Test-Path -LiteralPath $root -PathType Container)){ throw ('Tenant directory was not found: ' + $root) }; $files=@(Get-ChildItem -LiteralPath $root -Filter '*.env' -File | Sort-Object Name); if($files.Count -eq 0){ throw ('No tenant env files were discovered under ' + $root) }; $selected=$null; $known=New-Object System.Collections.Generic.List[string]; foreach($file in $files){ $map=@{}; foreach($line in Get-Content -Encoding UTF8 -LiteralPath $file.FullName){ $trim=$line.Trim(); if($trim.Length -eq 0 -or $trim.StartsWith('#')){ continue }; $idx=$trim.IndexOf('='); if($idx -le 0){ continue }; $key=$trim.Substring(0,$idx).Trim(); $value=$trim.Substring($idx+1).Trim().Trim([char]34).Trim([char]39); $map[$key]=[regex]::Replace($value, '%%?([A-Za-z0-9_]+)%%?', { param($m) $r=$m.Groups[1].Value; if($map.ContainsKey($r)){ $map[$r] } else { $m.Value } }) }; foreach($key in @('tenant_enabled','tenant_name')){ if(-not $map.ContainsKey($key) -or [string]::IsNullOrWhiteSpace([string]$map[$key])){ throw ('Missing required tenant env key ' + $key + ' in ' + $file.FullName) } }; $fileTenant=[IO.Path]::GetFileNameWithoutExtension($file.Name); if($map['tenant_name'] -ne $fileTenant){ throw ('Tenant file ' + $file.FullName + ' declares tenant_name=' + $map['tenant_name'] + ', expected ' + $fileTenant) }; [void]$known.Add($map['tenant_name']); if($map['tenant_name'] -eq $tenant){ $selected=[pscustomobject]@{ Path=$file.FullName; Values=$map } } }; if(-not $selected){ throw ('Tenant ' + $tenant + ' was not discovered. Discovered tenants: ' + ($known -join ', ')) }; $map=$selected.Values; foreach($key in @('tenant_enabled','tenant_id','tenant_order_number','tenant_name','tenant_display_name','tenant_knowledge_source_1_type','tenant_knowledge_source_1_url','tenant_knowledge_source_1_refresh_period')){ if(-not $map.ContainsKey($key) -or [string]::IsNullOrWhiteSpace([string]$map[$key])){ throw ('Missing required tenant env key: ' + $key) } }; if($map['tenant_enabled'] -notin @('true','false')){ throw 'tenant_enabled must be true or false.' }; if($map['tenant_enabled'] -ne 'true'){ throw ('Tenant ' + $tenant + ' is disabled. Set tenant_enabled=true in ' + $selected.Path) }; if($map['tenant_order_number'] -notmatch '^[1-9][0-9]*$'){ throw 'tenant_order_number must be a positive integer.' }; if($map['tenant_knowledge_source_1_type'] -ne 'web_site'){ throw 'tenant_knowledge_source_1_type must be web_site.' }; if($map['tenant_knowledge_source_1_url'] -notmatch '^https?://'){ throw 'tenant_knowledge_source_1_url must be http/https.' }; $image=$tenant + '-' + $map['tenant_order_number'] + ':latest'; Write-Host ('TENANT_ENV=' + $selected.Path); Write-Host ('TENANT_ORDER_NUMBER=' + $map['tenant_order_number']); Write-Host ('IMAGE=' + $image); Write-Host ('CONTAINER=oyako-app-' + $tenant + '-' + $map['tenant_order_number']); Write-Host ('TENANT_DISPLAY_NAME=' + $map['tenant_display_name'])"`) do set "%%A=%%B"
if errorlevel 1 (
  echo Tenant validation failed.
  exit /b 1
)
if not defined IMAGE (
  echo Tenant validation failed.
  exit /b 1
)

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

docker run --rm --init -it --name "%CONTAINER%" --label "%LABEL%" --label "com.oyako.role=fullstack-docker-dev" -p 8080:8080 --env-file "%ROOT%azure-cloud.env" --env-file "%ROOT%ollama-cloud.env" --env-file "%TENANT_ENV%" -e OYAKO_TENANT_NAME=%TENANT_NAME% -e OYAKO_DOCKER=1 -e ASPNETCORE_ENVIRONMENT=Production -e ASPNETCORE_URLS=http://+:8080 -e Storage__DataRoot=/app/data -e Sqlite__ConnectionString="Data Source=/app/data/%TENANT_NAME%/oyako.sqlite;Cache=Shared" -e Ai__DefaultProvider=ollama-cloud -e Ai__DisabledProviders__0=ollama-local --shm-size=1g "%IMAGE%"
set "RUN_EXIT=%ERRORLEVEL%"

echo Purging Oyako Docker artifacts...
call :purge_oyako

if not "%RUN_EXIT%"=="0" (
  echo Oyako Docker container exited with code %RUN_EXIT%.
  exit /b %RUN_EXIT%
)

echo Oyako Docker run finished and related Docker artifacts were purged.
exit /b 0

:purge_oyako
powershell -NoProfile -ExecutionPolicy Bypass -Command "$label='com.oyako.app=oyako'; $tag=$env:IMAGE; docker ps -aq --filter ('label=' + $label) 2>$null | ForEach-Object { if($_){ docker rm -f $_ 2>$null | Out-Null } }; docker images -q --filter ('label=' + $label) 2>$null | Sort-Object -Unique | ForEach-Object { if($_){ docker image rm -f $_ 2>$null | Out-Null } }; if($tag){ docker image rm -f $tag 2>$null | Out-Null }"
exit /b 0
