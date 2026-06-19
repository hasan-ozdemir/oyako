@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "OYAKO_SCRIPT_SELF=%~f0"
set "OYAKO_SCRIPT_PS1=%TEMP%\oyako-deploy-awa-%RANDOM%-%RANDOM%.ps1"

powershell -NoProfile -ExecutionPolicy Bypass -Command "$ErrorActionPreference='Stop'; $src=Get-Content -Raw -LiteralPath $env:OYAKO_SCRIPT_SELF; $parts=$src -split '(?m)^:POWERSHELL_BEGIN\r?$',2; if($parts.Count -lt 2){ throw 'PowerShell payload marker was not found.' }; Set-Content -LiteralPath $env:OYAKO_SCRIPT_PS1 -Value $parts[1] -Encoding UTF8"
if errorlevel 1 exit /b %ERRORLEVEL%

powershell -NoProfile -ExecutionPolicy Bypass -File "%OYAKO_SCRIPT_PS1%"
set "OYAKO_SCRIPT_EXIT=%ERRORLEVEL%"

del "%OYAKO_SCRIPT_PS1%" >nul 2>nul
exit /b %OYAKO_SCRIPT_EXIT%

:POWERSHELL_BEGIN
param()

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
Set-Variable -Name PSNativeCommandUseErrorActionPreference -Value $false -Scope Script -ErrorAction SilentlyContinue

$Root = Split-Path -Parent $env:OYAKO_SCRIPT_SELF
$Subscription = "az2vs"
$Location = "italynorth"
$ResourceGroup = "rg-oyako"
$PlanName = "oyako-awa-plan"
$Scope = "oyako-awa"
$ManagedBy = "deploy-awa"
$Sku = "B1"
$Runtime = "DOTNETCORE:10.0"
$LinuxFxVersion = "DOTNETCORE|10.0"
$SmokeTimeoutSeconds = 360
$Tags = @("app=oyako", "managed-by=$ManagedBy", "deployment-scope=$Scope")

function Step([string]$Message) { Write-Host ""; Write-Host "==> $Message" -ForegroundColor Cyan }
function Ok([string]$Message) { Write-Host "OK: $Message" -ForegroundColor Green }
function Fail([string]$Message) { throw $Message }

function Merge-ArgumentList {
    param([string[]]$BoundArguments, [object[]]$RemainingArguments)
    $merged = New-Object System.Collections.Generic.List[string]
    foreach ($item in @($BoundArguments)) {
        if ($null -ne $item) { $merged.Add([string]$item) }
    }
    foreach ($item in @($RemainingArguments)) {
        if ($null -eq $item) { continue }
        if ($item -is [Array]) {
            foreach ($nested in $item) {
                if ($null -ne $nested) { $merged.Add([string]$nested) }
            }
        }
        else {
            $merged.Add([string]$item)
        }
    }
    return [string[]]$merged.ToArray()
}

function Resolve-Exe([string]$Name) {
    $commands = @(Get-Command $Name -CommandType Application -ErrorAction SilentlyContinue)
    $command = $commands |
        Sort-Object @{ Expression = {
            if ($_.Source -like "*.exe") { 0 }
            elseif ($_.Source -like "*.cmd") { 1 }
            elseif ($_.Source -like "*.bat") { 2 }
            else { 3 }
        } } |
        Select-Object -First 1
    if (-not $command) { Fail "$Name was not found on PATH." }
    return [string]$command.Source
}

function Escape-CmdArguments([string]$CommandPath, [string[]]$Arguments) {
    if ($CommandPath -notlike "*.cmd") { return $Arguments }
    return [string[]]@($Arguments | ForEach-Object {
        $value = [string]$_
        if ($value -match "^[A-Za-z0-9_.:-]+\|[A-Za-z0-9_.:-]+$") { $value -replace "\|", "^|" } else { $value }
    })
}

function Run {
    param([string]$Exe, [string[]]$Arguments, [switch]$Sensitive, [switch]$Quiet)
    $commandPath = Resolve-Exe $Exe
    $invokeArguments = [string[]]@(Escape-CmdArguments $commandPath $Arguments)
    if (-not $Quiet) {
        $display = if ($Sensitive) { "$Exe <sensitive arguments redacted>" } else { "$Exe $($Arguments -join ' ')" }
        Write-Host $display -ForegroundColor DarkGray
    }

    $oldPreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $output = & $commandPath @invokeArguments 2>&1
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $oldPreference
    }

    if ($exitCode -ne 0) {
        $text = if ($Sensitive) { "<sensitive output redacted>" } else { $output -join [Environment]::NewLine }
        Fail "$Exe failed with exit code $exitCode.`n$text"
    }

    return ($output -join [Environment]::NewLine).Trim()
}

function TryRun {
    param([string]$Exe, [string[]]$Arguments, [switch]$Sensitive)
    $commandPath = Resolve-Exe $Exe
    $invokeArguments = [string[]]@(Escape-CmdArguments $commandPath $Arguments)
    $oldPreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $output = & $commandPath @invokeArguments 2>&1
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $oldPreference
    }

    $text = if ($Sensitive) { "<sensitive output redacted>" } else { ($output -join [Environment]::NewLine).Trim() }
    return [pscustomobject]@{ Ok = ($exitCode -eq 0); ExitCode = $exitCode; Text = $text }
}

function Az([string[]]$Arguments, [switch]$Sensitive, [switch]$Quiet) {
    $Arguments = Merge-ArgumentList $Arguments $args
    Run "az" ($Arguments + @("--only-show-errors")) -Sensitive:$Sensitive -Quiet:$Quiet | Out-Null
}

function AzText([string[]]$Arguments, [switch]$Sensitive, [switch]$Quiet) {
    $Arguments = Merge-ArgumentList $Arguments $args
    return Run "az" ($Arguments + @("--only-show-errors")) -Sensitive:$Sensitive -Quiet:$Quiet
}

function TryAz([string[]]$Arguments, [switch]$Sensitive) {
    $Arguments = Merge-ArgumentList $Arguments $args
    return TryRun "az" ($Arguments + @("--only-show-errors")) -Sensitive:$Sensitive
}

function FromJson([string]$Text) {
    if ([string]::IsNullOrWhiteSpace($Text)) { return $null }
    $startObject = $Text.IndexOf("{")
    $startArray = $Text.IndexOf("[")
    $starts = @($startObject, $startArray) | Where-Object { $_ -ge 0 } | Sort-Object
    if ($starts.Count -eq 0) { return $null }
    return $Text.Substring([int]$starts[0]) | ConvertFrom-Json
}

function Normalize-Location([string]$Value) {
    return ($Value -replace "\s+", "").ToLowerInvariant()
}

function Get-TagValue($Resource, [string]$Name) {
    if (-not $Resource -or -not $Resource.tags) { return "" }
    $property = $Resource.tags.PSObject.Properties[$Name]
    if ($property) { return [string]$property.Value }
    return ""
}

function Is-Owned($Resource) {
    if (-not $Resource) { return $false }
    return (Get-TagValue $Resource "app") -eq "oyako" `
        -and (Get-TagValue $Resource "managed-by") -eq $ManagedBy `
        -and (Get-TagValue $Resource "deployment-scope") -eq $Scope
}

function Assert-OwnedOrMissing($Resource, [string]$Name, [string]$Kind) {
    if ($Resource -and -not (Is-Owned $Resource)) {
        Fail "$Kind '$Name' exists but is not tagged as managed by $ManagedBy/$Scope. Refusing to modify it."
    }
}

function Tag-Resource([string]$ResourceId) {
    if ([string]::IsNullOrWhiteSpace($ResourceId)) {
        Fail "Azure returned an empty resource id."
    }
    Az ((@("resource", "tag", "--ids", $ResourceId, "--tags") + $Tags))
}

function Read-EnvFile([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        Fail "Required env file was not found: $Path"
    }

    $values = @{}
    foreach ($rawLine in Get-Content -LiteralPath $Path) {
        $line = $rawLine.Trim()
        if ($line.Length -eq 0 -or $line.StartsWith("#")) { continue }
        $separator = $line.IndexOf("=")
        if ($separator -le 0) { continue }
        $key = $line.Substring(0, $separator).Trim()
        $value = $line.Substring($separator + 1).Trim().Trim('"').Trim("'")
        if ($key.Length -gt 0) { $values[$key] = $value }
    }

    return $values
}

function Require-EnvKeys([hashtable]$Map, [string[]]$Keys, [string]$FileName) {
    foreach ($key in $Keys) {
        if (-not $Map.ContainsKey($key) -or [string]::IsNullOrWhiteSpace([string]$Map[$key])) {
            Fail "Missing or empty key '$key' in $FileName. Deployment stops before Azure mutation."
        }
    }
}

function EnvValue([hashtable]$Map, [string]$Key, [string]$DefaultValue) {
    if ($Map.ContainsKey($Key) -and -not [string]::IsNullOrWhiteSpace([string]$Map[$Key])) {
        return [string]$Map[$Key]
    }

    return $DefaultValue
}

function Ensure-Provider([string]$Namespace) {
    $state = AzText @("provider", "show", "--namespace", $Namespace, "--query", "registrationState", "-o", "tsv") -Quiet
    if ($state -eq "Registered") {
        Ok "$Namespace provider is registered."
        return
    }

    Step "Registering Azure provider $Namespace"
    Az @("provider", "register", "--namespace", $Namespace)
    $deadline = (Get-Date).AddMinutes(10)
    do {
        Start-Sleep -Seconds 10
        $state = AzText @("provider", "show", "--namespace", $Namespace, "--query", "registrationState", "-o", "tsv") -Quiet
        if ($state -eq "Registered") {
            Ok "$Namespace provider registration completed."
            return
        }
    } while ((Get-Date) -lt $deadline)

    Fail "$Namespace provider registration did not complete within 10 minutes."
}

function Remove-PathInsideRoot([string]$Path) {
    $rootFull = [IO.Path]::GetFullPath($Root).TrimEnd('\') + '\'
    $targetFull = [IO.Path]::GetFullPath($Path)
    if (-not $targetFull.StartsWith($rootFull, [StringComparison]::OrdinalIgnoreCase)) {
        Fail "Refusing to remove path outside repo root: $targetFull"
    }
    if (Test-Path -LiteralPath $targetFull) {
        Remove-Item -LiteralPath $targetFull -Recurse -Force
    }
}

function Wait-ResourceGone([string]$Description, [scriptblock]$Exists) {
    $deadline = (Get-Date).AddMinutes(15)
    do {
        if (-not (& $Exists)) {
            Ok "$Description removed."
            return
        }
        Start-Sleep -Seconds 10
    } while ((Get-Date) -lt $deadline)

    Fail "$Description was still visible in Azure after 15 minutes."
}

function Wait-Smoke([string]$Name, [string]$Url, [int]$TimeoutSeconds, [string]$RequiredText = "") {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $last = ""
    do {
        try {
            $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 20
            $snippet = (($response.Content -replace "\s+", " ").Trim())
            if ($snippet.Length -gt 220) { $snippet = $snippet.Substring(0, 220) }
            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 400) {
                if (-not [string]::IsNullOrWhiteSpace($RequiredText) -and $response.Content.IndexOf($RequiredText, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
                    $last = "HTTP $($response.StatusCode): missing '$RequiredText'. $snippet"
                }
                else {
                    return [pscustomobject]@{ Name = $Name; Url = $Url; StatusCode = $response.StatusCode; Snippet = $snippet; Ok = $true }
                }
            }
            else {
                $last = "HTTP $($response.StatusCode): $snippet"
            }
        }
        catch {
            $last = $_.Exception.Message
            if ($_.Exception.Response) {
                try { $last = "HTTP $([int]$_.Exception.Response.StatusCode): $last" } catch { }
            }
        }

        Start-Sleep -Seconds 5
    } while ((Get-Date) -lt $deadline)

    return [pscustomobject]@{ Name = $Name; Url = $Url; StatusCode = 0; Snippet = $last; Ok = $false }
}

function Get-WebApp([string]$Name) {
    $result = TryAz @("resource", "list", "--name", $Name, "--resource-type", "Microsoft.Web/sites", "-o", "json")
    if (-not $result.Ok -or [string]::IsNullOrWhiteSpace($result.Text)) { return $null }
    return @(FromJson $result.Text | Select-Object -First 1)[0]
}

function Get-Plan() {
    $result = TryAz @("appservice", "plan", "show", "--name", $PlanName, "--resource-group", $ResourceGroup, "-o", "json")
    if (-not $result.Ok) { return $null }
    return FromJson $result.Text
}

function Remove-OwnedWebApp([string]$Name) {
    $site = Get-WebApp $Name
    Assert-OwnedOrMissing $site $Name "Web App"
    if ($site) {
        Step "Removing non-compliant Web App $Name"
        Az @("webapp", "delete", "--name", $site.name, "--resource-group", $site.resourceGroup)
        Wait-ResourceGone "Web App $Name" { [bool](Get-WebApp $Name) }
    }
}

function Remove-OwnedPlan() {
    $plan = Get-Plan
    Assert-OwnedOrMissing $plan $PlanName "App Service Plan"
    if ($plan) {
        Step "Removing non-compliant App Service Plan $PlanName"
        Az @("appservice", "plan", "delete", "--name", $PlanName, "--resource-group", $ResourceGroup, "--yes")
        Wait-ResourceGone "App Service Plan $PlanName" { [bool](Get-Plan) }
    }
}

function Build-PublishPackage([string]$PublishDir, [string]$ZipPath) {
    $apiProject = Join-Path $Root "webapi-oyako\webapi-oyako.csproj"
    $webDir = Join-Path $Root "webapp-oyako"
    $webDist = Join-Path $webDir "dist"
    $wwwroot = Join-Path $PublishDir "wwwroot"

    Step "Building React frontend"
    Run "npm" @("ci", "--prefix", $webDir) | Out-Null
    Run "npm" @("run", "build", "--prefix", $webDir) | Out-Null
    if (-not (Test-Path -LiteralPath (Join-Path $webDist "index.html"))) {
        Fail "React build did not produce $webDist\index.html."
    }

    Step "Publishing ASP.NET backend"
    Run "dotnet" @("restore", $apiProject, "-r", "linux-x64") | Out-Null
    Run "dotnet" @("publish", $apiProject, "-c", "Release", "-r", "linux-x64", "--self-contained", "false", "--no-restore", "-o", $PublishDir, "/p:UseAppHost=false") | Out-Null

    Step "Copying React dist into ASP.NET wwwroot"
    if (Test-Path -LiteralPath $wwwroot) {
        Remove-Item -LiteralPath $wwwroot -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path $wwwroot | Out-Null
    Copy-Item -Path (Join-Path $webDist "*") -Destination $wwwroot -Recurse -Force
    if (-not (Test-Path -LiteralPath (Join-Path $wwwroot "index.html"))) {
        Fail "React dist was not copied into $wwwroot."
    }

    if (-not (Test-Path -LiteralPath (Join-Path $PublishDir "Microsoft.Playwright.dll"))) {
        Fail "Microsoft.Playwright.dll was not found after publish. Direct App Service deploy cannot validate Chromium."
    }
    if (-not (Test-Path -LiteralPath (Join-Path $PublishDir ".playwright\node\linux-x64\node"))) {
        Fail "Playwright linux-x64 node driver was not found after publish. Direct App Service deploy cannot validate Chromium."
    }

    $startupScript = @'
#!/usr/bin/env bash
set -euo pipefail

export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Production}"
export ASPNETCORE_URLS="${ASPNETCORE_URLS:-http://0.0.0.0:${PORT:-8080}}"
export PLAYWRIGHT_BROWSERS_PATH="${PLAYWRIGHT_BROWSERS_PATH:-/home/oyako-playwright/ms-playwright}"
mkdir -p /home/oyako-data "${PLAYWRIGHT_BROWSERS_PATH}"

if [ ! -f ./webapi-oyako.dll ]; then
  echo "[startup] ERROR: webapi-oyako.dll is missing from the deployed package."
  exit 90
fi

if [ ! -f ./Microsoft.Playwright.dll ]; then
  echo "[startup] ERROR: Microsoft.Playwright.dll is missing from the deployed package."
  exit 91
fi

if [ ! -f ./.playwright/node/linux-x64/node ]; then
  echo "[startup] ERROR: Playwright linux-x64 node driver is missing from the deployed package."
  exit 92
fi

chmod +x ./.playwright/node/linux-x64/node
if [ ! -e /usr/lib/x86_64-linux-gnu/libnspr4.so ] && [ ! -e /lib/x86_64-linux-gnu/libnspr4.so ]; then
  dotnet ./webapi-oyako.dll --install-playwright-deps
fi
dotnet ./webapi-oyako.dll --install-playwright
exec dotnet ./webapi-oyako.dll
'@
    [IO.File]::WriteAllText(
        (Join-Path $PublishDir "startup.sh"),
        ($startupScript -replace "`r`n", "`n"),
        [System.Text.ASCIIEncoding]::new())

    Step "Creating App Service ZIP package"
    if (Test-Path -LiteralPath $ZipPath) {
        Remove-Item -LiteralPath $ZipPath -Force
    }
    Compress-Archive -Path (Join-Path $PublishDir "*") -DestinationPath $ZipPath -Force
}

try {
    Set-Location -LiteralPath $Root

    Step "Checking local prerequisites and strict env files"
    Resolve-Exe "az" | Out-Null
    Resolve-Exe "dotnet" | Out-Null
    Resolve-Exe "node" | Out-Null
    Resolve-Exe "npm" | Out-Null

    $azureEnv = Read-EnvFile (Join-Path $Root "azure-cloud.env")
    $ollamaEnv = Read-EnvFile (Join-Path $Root "ollama-cloud.env")
    Require-EnvKeys $azureEnv @("AzureAi__Endpoint", "AzureAi__DeploymentName", "AzureAi__Deployments__0", "AzureAi__ApiVersion", "AzureAi__ApiKey") "azure-cloud.env"
    Require-EnvKeys $ollamaEnv @("ollama_api_key") "ollama-cloud.env"
    Ok "Required env files and keys are present."

    $buildRoot = Join-Path $Root ".oyako-deploy\awa"
    $publishDir = Join-Path $buildRoot "publish"
    $zipPath = Join-Path $buildRoot "oyako-awa.zip"
    Remove-PathInsideRoot $buildRoot
    New-Item -ItemType Directory -Force -Path $publishDir | Out-Null
    Build-PublishPackage $publishDir $zipPath

    Step "Selecting Azure subscription"
    $account = TryAz @("account", "show", "-o", "json")
    if (-not $account.Ok) {
        Fail "Azure CLI is not logged in. Run 'az login' manually, then retry deploy-awa.cmd."
    }
    Az @("account", "set", "--subscription", $Subscription)
    $subscriptionId = AzText @("account", "show", "--query", "id", "-o", "tsv") -Quiet
    if ([string]::IsNullOrWhiteSpace($subscriptionId)) { Fail "Azure subscription id could not be resolved after selecting '$Subscription'." }
    $subscriptionCompact = $subscriptionId.Replace("-", "").ToLowerInvariant()
    $WebAppName = "oyako-awa-$($subscriptionCompact.Substring(0, 8))"
    Ok "Using subscription $Subscription ($subscriptionId)."

    Step "Validating Azure capabilities"
    Ensure-Provider "Microsoft.Web"
    $locationName = AzText @("account", "list-locations", "--query", "[?name=='$Location'].name | [0]", "-o", "tsv") -Quiet
    if ($locationName -ne $Location) { Fail "Azure location '$Location' is not available for this subscription." }
    $appServiceLocations = AzText @("appservice", "list-locations", "--sku", $Sku, "--linux-workers-enabled", "-o", "tsv") -Quiet
    if (@($appServiceLocations -split "\r?\n" | Where-Object { $_ } | ForEach-Object { Normalize-Location ([string]$_) }) -notcontains $Location) {
        Fail "Linux App Service $Sku is not available in '$Location'."
    }

    Step "Ensuring resource group"
    $rg = TryAz @("group", "show", "--name", $ResourceGroup, "--query", "id", "-o", "tsv")
    if (-not $rg.Ok -or [string]::IsNullOrWhiteSpace($rg.Text)) {
        Az @("group", "create", "--name", $ResourceGroup, "--location", $Location)
    }

    Step "Ensuring App Service resources"
    $site = Get-WebApp $WebAppName
    Assert-OwnedOrMissing $site $WebAppName "Web App"
    $plan = Get-Plan
    Assert-OwnedOrMissing $plan $PlanName "App Service Plan"

    if ($site -and (([string]$site.resourceGroup -ne $ResourceGroup) -or ((Normalize-Location ([string]$site.location)) -ne $Location))) {
        Remove-OwnedWebApp $WebAppName
        $site = $null
    }

    if ($plan -and (((Normalize-Location ([string]$plan.location)) -ne $Location) -or ([string]$plan.sku.name -ne $Sku))) {
        Remove-OwnedWebApp $WebAppName
        Remove-OwnedPlan
        $site = $null
        $plan = $null
    }

    if (-not $plan) {
        Az ((@("appservice", "plan", "create", "--name", $PlanName, "--resource-group", $ResourceGroup, "--location", $Location, "--sku", $Sku, "--is-linux", "--number-of-workers", "1", "--tags") + $Tags))
        $plan = Get-Plan
        if (-not $plan) { Fail "App Service Plan '$PlanName' was not available after create." }
    }
    Tag-Resource ([string]$plan.id)

    if (-not $site) {
        Az @("webapp", "create", "--name", $WebAppName, "--resource-group", $ResourceGroup, "--plan", $PlanName, "--runtime", $Runtime, "--https-only", "true")
        $site = Get-WebApp $WebAppName
        if (-not $site) { Fail "Web App '$WebAppName' was not available after create." }
    }
    Tag-Resource ([string]$site.id)

    Step "Configuring Web App"
    $currentLinuxFxVersion = AzText @("webapp", "config", "show", "--name", $WebAppName, "--resource-group", $ResourceGroup, "--query", "linuxFxVersion", "-o", "tsv") -Quiet
    if ($currentLinuxFxVersion -ne $LinuxFxVersion) {
        Fail "Web App runtime is '$currentLinuxFxVersion', expected '$LinuxFxVersion'. Recreate the script-owned Web App or update Azure CLI/runtime support before deploying."
    }
    Az @("webapp", "config", "set", "--name", $WebAppName, "--resource-group", $ResourceGroup, "--always-on", "true", "--ftps-state", "Disabled", "--startup-file", "bash startup.sh")

    $aiProvider = EnvValue $azureEnv "Ai__DefaultProvider" "ollama-cloud"
    $appSettings = @(
        "ASPNETCORE_ENVIRONMENT=Production",
        "SCM_DO_BUILD_DURING_DEPLOYMENT=false",
        "WEBSITE_RUN_FROM_PACKAGE=0",
        "WEBSITES_CONTAINER_START_TIME_LIMIT=900",
        "Storage__DataRoot=/home/oyako-data",
        "Sqlite__ConnectionString=Data Source=/home/oyako-data/oyako.sqlite;Cache=Shared",
        "PLAYWRIGHT_BROWSERS_PATH=/home/oyako-playwright/ms-playwright",
        "Ai__DefaultProvider=$aiProvider",
        "Ai__DisabledProviders__0=ollama-local",
        "AzureAi__Endpoint=$($azureEnv["AzureAi__Endpoint"])",
        "AzureAi__DeploymentName=$($azureEnv["AzureAi__DeploymentName"])",
        "AzureAi__Deployments__0=$($azureEnv["AzureAi__Deployments__0"])",
        "AzureAi__ApiVersion=$($azureEnv["AzureAi__ApiVersion"])",
        "AzureAi__TimeoutSeconds=$(EnvValue $azureEnv "AzureAi__TimeoutSeconds" "180")",
        "AzureAi__Temperature=$(EnvValue $azureEnv "AzureAi__Temperature" "0.2")",
        "AzureAi__ApiKey=$($azureEnv["AzureAi__ApiKey"])",
        "OllamaCloud__BaseUrl=$(EnvValue $ollamaEnv "OllamaCloud__BaseUrl" "https://ollama.com")",
        "OllamaCloud__Model=$(EnvValue $ollamaEnv "OllamaCloud__Model" "minimax-m3:cloud")",
        "OllamaCloud__TimeoutSeconds=$(EnvValue $ollamaEnv "OllamaCloud__TimeoutSeconds" "180")",
        "OllamaCloud__Temperature=$(EnvValue $ollamaEnv "OllamaCloud__Temperature" "0.2")",
        "OllamaCloud__ApiKey=$($ollamaEnv["ollama_api_key"])",
        "ollama_api_key=$($ollamaEnv["ollama_api_key"])"
    )
    Az ((@("webapp", "config", "appsettings", "set", "--name", $WebAppName, "--resource-group", $ResourceGroup, "--settings") + $appSettings)) -Sensitive

    Step "Deploying ZIP package"
    Az @("webapp", "deploy", "--name", $WebAppName, "--resource-group", $ResourceGroup, "--src-path", $ZipPath, "--type", "zip", "--clean", "true", "--restart", "true")

    $baseUrl = "https://$WebAppName.azurewebsites.net"
    Step "Running Web App smoke tests"
    $rootSmoke = Wait-Smoke "AWA frontend root" "$baseUrl/" $SmokeTimeoutSeconds "Oyako"
    $healthSmoke = Wait-Smoke "AWA /health" "$baseUrl/health" $SmokeTimeoutSeconds '"service":"oyako"'
    $browserSmoke = Wait-Smoke "AWA /health/browser" "$baseUrl/health/browser" $SmokeTimeoutSeconds '"browser":"chromium"'
    $smokeResults = @($rootSmoke, $healthSmoke, $browserSmoke)
    if ($smokeResults.Where({ -not $_.Ok }).Count -gt 0) {
        $details = ($smokeResults | ForEach-Object { "$($_.Name): HTTP $($_.StatusCode) $($_.Snippet)" }) -join [Environment]::NewLine
        Fail "AWA smoke tests failed.`n$details"
    }

    Step "Collecting final App Service resource list"
    $resources = @()
    foreach ($resource in @((Get-Plan), (Get-WebApp $WebAppName))) {
        if ($resource) {
            $resources += [pscustomobject]@{
                name = $resource.name
                type = $resource.type
                location = $resource.location
                id = $resource.id
            }
        }
    }
    $alwaysOn = AzText @("webapp", "config", "show", "--name", $WebAppName, "--resource-group", $ResourceGroup, "--query", "alwaysOn", "-o", "tsv") -Quiet
    $finalSku = AzText @("appservice", "plan", "show", "--name", $PlanName, "--resource-group", $ResourceGroup, "--query", "sku.name", "-o", "tsv") -Quiet

    Write-Host ""
    Write-Host "Oyako App Service deployment completed." -ForegroundColor Green
    Write-Host "URL: $baseUrl/"
    Write-Host "API health: $baseUrl/health"
    Write-Host "Browser health: $baseUrl/health/browser"
    Write-Host "SKU: $finalSku"
    Write-Host "Always On: $alwaysOn"
    Write-Host "Location: $Location"
    Write-Host ""
    Write-Host "Smoke tests:"
    foreach ($result in $smokeResults) {
        Write-Host "  $($result.Name): HTTP $($result.StatusCode) - $($result.Snippet)"
    }
    Write-Host ""
    Write-Host "Resources:"
    foreach ($resource in $resources) {
        Write-Host "  $($resource.type) :: $($resource.name) :: $($resource.location)"
    }
    Write-Host ""
    Write-Host "Cost shape: one Linux App Service Plan at Basic B1 and one Web App. No Storage Account, ACR, Key Vault, or managed database was created by this script."
    exit 0
}
catch {
    Write-Host ""
    Write-Host "Oyako App Service deployment failed." -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ""
    Write-Host "Retry command:"
    Write-Host "  deploy-awa.cmd"
    exit 1
}
