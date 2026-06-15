@echo off
REM Codex developer note: Deploys the Oyako full-stack Docker image to Azure Container Apps.
setlocal EnableExtensions DisableDelayedExpansion

set "DEPLOY_ACA_SELF=%~f0"
set "DEPLOY_ACA_PS1=%TEMP%\oyako-deploy-aca-%RANDOM%-%RANDOM%.ps1"

powershell -NoProfile -ExecutionPolicy Bypass -Command "$ErrorActionPreference='Stop'; $src=Get-Content -Raw -LiteralPath $env:DEPLOY_ACA_SELF; $parts=$src -split '(?m)^:POWERSHELL_BEGIN\r?$',2; if($parts.Count -lt 2){ throw 'PowerShell payload marker was not found.' }; Set-Content -LiteralPath $env:DEPLOY_ACA_PS1 -Value $parts[1] -Encoding UTF8"
if errorlevel 1 exit /b %ERRORLEVEL%

powershell -NoProfile -ExecutionPolicy Bypass -File "%DEPLOY_ACA_PS1%" %*
set "DEPLOY_ACA_EXIT=%ERRORLEVEL%"

del "%DEPLOY_ACA_PS1%" >nul 2>nul
exit /b %DEPLOY_ACA_EXIT%

:POWERSHELL_BEGIN
param()

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
Set-Variable -Name PSNativeCommandUseErrorActionPreference -Value $false -Scope Script -ErrorAction SilentlyContinue

$Root = Split-Path -Parent $env:DEPLOY_ACA_SELF
$SubscriptionName = "az2vs"
$ResourceGroup = "rg-oyako"
$RequestedLocation = "austriaeast"
$FallbackContainerAppsLocation = "italynorth"
$Location = $RequestedLocation
$AcrLocation = "westeurope"
$EnvironmentName = "aca-oyako-env"
$ContainerAppName = "oyako"
$ImageRepository = "oyako"
$Version = "2026.6.18.300"
$Cpu = "0.5"
$Memory = "1.0Gi"
$MinReplicas = "0"
$MaxReplicas = "1"
$TargetPort = "3000"
$DefaultAiProvider = "ollama-cloud"
$PlaceholderImage = "mcr.microsoft.com/k8se/quickstart:latest"
$WebTimeoutSeconds = 600
$KnowledgeTimeoutSeconds = 900
$ChatTimeoutSeconds = 360
$SkipSmokeTest = $args -contains "--skip-smoke-test"
$script:SourceRevision = "unknown"
$ShowHelp = ($args -contains "--help") -or ($args -contains "-h") -or ($args -contains "/?")
$script:DeployContext = $null

function Show-Help {
    Write-Host "Oyako Azure Container Apps deploy script"
    Write-Host ""
    Write-Host "Usage:"
    Write-Host "  deploy-aca.cmd [--skip-smoke-test]"
    Write-Host ""
    Write-Host "What it does:"
    Write-Host "  - Selects Azure subscription: az2vs"
    Write-Host "  - Uses resource group: rg-oyako"
    Write-Host "  - Prefers Azure location: austriaeast"
    Write-Host "  - Falls back to ACA-supported nearest location: italynorth"
    Write-Host "  - Uses ACR in: westeurope"
    Write-Host "  - Deploys one container app named: oyako"
    Write-Host "  - Publishes Web UI at: https://oyako.<azure-managed-domain>/"
    Write-Host "  - Publishes Web API at: https://oyako.<azure-managed-domain>/api"
    Write-Host ""
    Write-Host "Required local files:"
    Write-Host "  - azure-cloud.env"
    Write-Host "  - ollama-cloud.env"
}

if ($ShowHelp) {
    Show-Help
    exit 0
}

function Write-Step([string]$Message) {
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Write-Ok([string]$Message) {
    Write-Host "OK: $Message" -ForegroundColor Green
}

function Write-Warn([string]$Message) {
    Write-Host "WARN: $Message" -ForegroundColor Yellow
}

function Fail([string]$Message) {
    throw $Message
}

function Format-CommandForLog([string]$Exe, [string[]]$Arguments, [switch]$Sensitive) {
    if ($Sensitive) {
        return "$Exe <sensitive arguments redacted>"
    }

    $escaped = $Arguments | ForEach-Object {
        if ($_ -match "\s") { '"' + ($_ -replace '"','\"') + '"' } else { $_ }
    }

    return "$Exe $($escaped -join ' ')"
}

function Invoke-External {
    param(
        [Parameter(Mandatory = $true)][string]$Exe,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [switch]$Sensitive
    )

    $commandForLog = Format-CommandForLog $Exe $Arguments -Sensitive:$Sensitive
    Write-Host $commandForLog -ForegroundColor DarkGray

    $previousNativePreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $output = & $Exe @Arguments 2>&1
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousNativePreference
    }

    if ($exitCode -ne 0) {
        if ($Sensitive) {
            Fail "$Exe failed with exit code $exitCode. Sensitive command output was suppressed."
        }

        Fail "$Exe failed with exit code $exitCode.`n$($output -join [Environment]::NewLine)"
    }

    return $output
}

function Invoke-Capture {
    param(
        [Parameter(Mandatory = $true)][string]$Exe,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [switch]$AllowFailure,
        [switch]$Sensitive
    )

    $previousNativePreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $output = & $Exe @Arguments 2>&1
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousNativePreference
    }

    if ($exitCode -ne 0) {
        if ($AllowFailure) {
            return $null
        }

        if ($Sensitive) {
            Fail "$Exe failed with exit code $exitCode. Sensitive command output was suppressed."
        }

        Fail "$Exe failed with exit code $exitCode.`n$($output -join [Environment]::NewLine)"
    }

    return ($output -join [Environment]::NewLine).Trim()
}

function Invoke-Az([string[]]$Arguments, [switch]$Sensitive) {
    Invoke-External -Exe "az" -Arguments $Arguments -Sensitive:$Sensitive | Out-Null
}

function Capture-Az([string[]]$Arguments, [switch]$AllowFailure, [switch]$Sensitive) {
    return Invoke-Capture -Exe "az" -Arguments $Arguments -AllowFailure:$AllowFailure -Sensitive:$Sensitive
}

function Read-EnvFile([string]$Path) {
    $map = @{}

    foreach ($line in Get-Content -LiteralPath $Path) {
        $trimmed = $line.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmed) -or $trimmed.StartsWith("#")) {
            continue
        }

        $equalsIndex = $line.IndexOf("=")
        if ($equalsIndex -le 0) {
            continue
        }

        $key = $line.Substring(0, $equalsIndex).Trim()
        $value = $line.Substring($equalsIndex + 1).Trim()

        if (($value.StartsWith('"') -and $value.EndsWith('"')) -or ($value.StartsWith("'") -and $value.EndsWith("'"))) {
            $value = $value.Substring(1, $value.Length - 2)
        }

        $map[$key] = $value
    }

    return $map
}

function ConvertFrom-AzJsonOutput([string]$Text) {
    if ([string]::IsNullOrWhiteSpace($Text)) {
        Fail "Azure CLI returned an empty JSON payload."
    }

    $objectStart = $Text.IndexOf("{")
    $arrayStart = $Text.IndexOf("[")
    $starts = @($objectStart, $arrayStart) | Where-Object { $_ -ge 0 } | Sort-Object
    if (-not $starts -or $starts.Count -eq 0) {
        Fail "Azure CLI output did not contain JSON."
    }

    return ($Text.Substring($starts[0]) | ConvertFrom-Json)
}

function Require-Key([hashtable]$Map, [string]$Key, [string]$FileName) {
    if (-not $Map.ContainsKey($Key) -or [string]::IsNullOrWhiteSpace([string]$Map[$Key])) {
        Fail "Missing required key '$Key' in $FileName."
    }
}

function Get-OrDefault([hashtable]$Map, [string]$Key, [string]$DefaultValue) {
    if ($Map.ContainsKey($Key) -and -not [string]::IsNullOrWhiteSpace([string]$Map[$Key])) {
        return [string]$Map[$Key]
    }

    return $DefaultValue
}

function Ensure-Tool([string]$Name) {
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        Fail "$Name was not found on PATH."
    }
}

function Invoke-RobocopyCopy {
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Destination,
        [string[]]$ExcludedDirectories = @(),
        [string[]]$ExcludedFiles = @()
    )

    New-Item -ItemType Directory -Force -Path $Destination | Out-Null

    $robocopyArgs = @(
        $Source,
        $Destination,
        "/E",
        "/NFL",
        "/NDL",
        "/NJH",
        "/NJS",
        "/NP",
        "/R:2",
        "/W:1"
    )

    if ($ExcludedDirectories.Count -gt 0) {
        $robocopyArgs += "/XD"
        $robocopyArgs += $ExcludedDirectories
    }

    if ($ExcludedFiles.Count -gt 0) {
        $robocopyArgs += "/XF"
        $robocopyArgs += $ExcludedFiles
    }

    Write-Host "robocopy <source> <clean-build-context> ..." -ForegroundColor DarkGray
    $output = & robocopy @robocopyArgs 2>&1
    $exitCode = $LASTEXITCODE

    if ($exitCode -ge 8) {
        Fail "robocopy failed with exit code $exitCode.`n$($output -join [Environment]::NewLine)"
    }
}

function New-DeploymentContext([string]$SourceRoot) {
    $contextRoot = Join-Path ([IO.Path]::GetTempPath()) ("oyako-aca-context-" + [Guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Force -Path $contextRoot | Out-Null

    Copy-Item -LiteralPath (Join-Path $SourceRoot "Dockerfile") -Destination (Join-Path $contextRoot "Dockerfile") -Force
    Copy-Item -LiteralPath (Join-Path $SourceRoot ".dockerignore") -Destination (Join-Path $contextRoot ".dockerignore") -Force -ErrorAction SilentlyContinue
    Copy-Item -LiteralPath (Join-Path $SourceRoot ".azignore") -Destination (Join-Path $contextRoot ".azignore") -Force -ErrorAction SilentlyContinue

    Invoke-RobocopyCopy `
        -Source (Join-Path $SourceRoot "docker") `
        -Destination (Join-Path $contextRoot "docker")

    Invoke-RobocopyCopy `
        -Source (Join-Path $SourceRoot "webapi-oyako") `
        -Destination (Join-Path $contextRoot "webapi-oyako") `
        -ExcludedDirectories @((Join-Path $SourceRoot "webapi-oyako\Data"), (Join-Path $SourceRoot "webapi-oyako\.certificates"), "bin", "obj", "webapi-oyako.Tests") `
        -ExcludedFiles @("*.db", "*.db-shm", "*.db-wal", "*.log")

    Invoke-RobocopyCopy `
        -Source (Join-Path $SourceRoot "webapp-oyako") `
        -Destination (Join-Path $contextRoot "webapp-oyako") `
        -ExcludedDirectories @("node_modules", "dist", "test-results", "playwright-report") `
        -ExcludedFiles @("*.log")

    return $contextRoot
}

function Remove-DeploymentContext {
    if ($script:DeployContext -and (Test-Path -LiteralPath $script:DeployContext)) {
        Remove-Item -LiteralPath $script:DeployContext -Recurse -Force -ErrorAction SilentlyContinue
        $script:DeployContext = $null
    }
}

function Wait-AcrDataPlane([string]$LoginServer, [int]$TimeoutSeconds = 300) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $url = "https://$LoginServer/v2/"
    $lastStatus = $null
    $lastError = $null

    do {
        $challenge = ""
        try {
            $response = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 15
            $lastStatus = $response.StatusCode
            $challenge = ($response.Headers["WWW-Authenticate"] -join "")
        }
        catch {
            if ($_.Exception.Response) {
                $lastStatus = [int]$_.Exception.Response.StatusCode
                $challenge = [string]$_.Exception.Response.Headers["WWW-Authenticate"]
            }
            else {
                $lastError = $_.Exception.Message
            }
        }

        if (($lastStatus -eq 401 -and $challenge.Contains("/oauth2/token")) -or $lastStatus -eq 200) {
            Write-Ok "ACR data-plane is ready: $LoginServer"
            return
        }

        Start-Sleep -Seconds 10
    } while ((Get-Date) -lt $deadline)

    Fail "ACR data-plane did not become ready within $TimeoutSeconds seconds. Last status: $lastStatus. Last error: $lastError"
}

function Invoke-ImageBuildAndPush {
    param(
        [Parameter(Mandatory = $true)][string]$AcrName,
        [Parameter(Mandatory = $true)][string]$ImageName,
        [Parameter(Mandatory = $true)][string]$FullImage,
        [Parameter(Mandatory = $true)][string]$DockerfilePath,
        [Parameter(Mandatory = $true)][string]$ContextPath
    )

    $adminTemporarilyEnabled = $false
    try {
        Ensure-Tool "docker"
        Invoke-External -Exe "docker" -Arguments @("version") | Out-Null
        $expectedLoginServer = ($FullImage -split "/", 2)[0]
        Wait-AcrDataPlane -LoginServer $expectedLoginServer -TimeoutSeconds 300

        $loginJson = Capture-Az @("acr", "login", "--name", $AcrName, "--expose-token", "-o", "json") -AllowFailure -Sensitive
        $dockerServer = $null
        $dockerUser = $null
        $dockerPassword = $null

        if ($loginJson) {
            $login = ConvertFrom-AzJsonOutput $loginJson
            $dockerServer = [string]$login.loginServer
            $dockerUser = "00000000-0000-0000-0000-000000000000"
            $dockerPassword = [string]$login.accessToken
        }

        if ([string]::IsNullOrWhiteSpace($dockerServer) -or [string]::IsNullOrWhiteSpace($dockerPassword)) {
            Write-Warn "AAD token login to ACR is unavailable. Temporarily enabling the ACR admin account for Docker push, then disabling it again."
            Invoke-Az @("acr", "update", "--name", $AcrName, "--admin-enabled", "true", "--only-show-errors")
            $adminTemporarilyEnabled = $true
            $credentialJson = Capture-Az @("acr", "credential", "show", "--name", $AcrName, "-o", "json") -Sensitive
            $credential = ConvertFrom-AzJsonOutput $credentialJson
            $dockerServer = "$AcrName.azurecr.io"
            $dockerUser = [string]$credential.username
            $dockerPassword = [string]$credential.passwords[0].value
        }

        if ([string]::IsNullOrWhiteSpace($dockerServer) -or [string]::IsNullOrWhiteSpace($dockerUser) -or [string]::IsNullOrWhiteSpace($dockerPassword)) {
            Fail "Could not resolve Docker credentials for ACR."
        }

        Write-Host "docker login <acr-login-server> --username <redacted> --password-stdin" -ForegroundColor DarkGray
        $previousNativePreference = $ErrorActionPreference
        $ErrorActionPreference = "Continue"
        try {
            $loginOutput = $dockerPassword | docker login $dockerServer --username $dockerUser --password-stdin 2>&1
            $loginExitCode = $LASTEXITCODE
        }
        finally {
            $ErrorActionPreference = $previousNativePreference
        }

        if ($loginExitCode -ne 0) {
            Fail "docker login to ACR failed with exit code $loginExitCode. Output:`n$($loginOutput -join [Environment]::NewLine)"
        }

        Invoke-External -Exe "docker" -Arguments @("build", "--force-rm", "--label", "com.oyako.app=oyako", "--label", "com.oyako.role=azure-container-apps", "--label", "org.opencontainers.image.version=$Version", "--label", "org.opencontainers.image.revision=$script:SourceRevision", "-t", $FullImage, "-f", $DockerfilePath, $ContextPath)
        Invoke-External -Exe "docker" -Arguments @("push", $FullImage)
    }
    finally {
        if ($adminTemporarilyEnabled) {
            Invoke-Az @("acr", "update", "--name", $AcrName, "--admin-enabled", "false", "--only-show-errors")
        }
    }
}


function Get-SourceRevision {
    if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
        return "unknown"
    }

    $revision = Invoke-Capture -Exe "git" -Arguments @("rev-parse", "--short=12", "HEAD") -AllowFailure
    if ([string]::IsNullOrWhiteSpace($revision)) {
        return "uncommitted"
    }

    return $revision.Trim()
}

function Remove-AcrImageRepositoryIfExists([string]$AcrName, [string]$Repository) {
    $existing = Capture-Az @("acr", "repository", "show", "--name", $AcrName, "--repository", $Repository, "-o", "json") -AllowFailure
    if ($existing) {
        Write-Warn "Deleting previous ACR repository '$Repository' so this deploy retains only the latest image."
        Invoke-Az @("acr", "repository", "delete", "--name", $AcrName, "--repository", $Repository, "--yes", "--only-show-errors")
    }
}

function Assert-AcrLatestOnly([string]$AcrName, [string]$Repository) {
    $tagsJson = Capture-Az @("acr", "repository", "show-tags", "--name", $AcrName, "--repository", $Repository, "-o", "json")
    $tags = @($tagsJson | ConvertFrom-Json)
    if ($tags.Count -ne 1 -or $tags[0] -ne "latest") {
        Fail "ACR repository '$Repository' must contain only the 'latest' tag, but found: $($tags -join ', ')"
    }

    Write-Ok "ACR repository '$Repository' contains only 'latest'."
}
function Ensure-AzureProvider([string]$Namespace) {
    $state = Capture-Az @("provider", "show", "--namespace", $Namespace, "--query", "registrationState", "-o", "tsv") -AllowFailure
    if ($state -eq "Registered") {
        Write-Ok "$Namespace provider is registered."
        return
    }

    Write-Warn "$Namespace provider is '$state'. Registering..."
    Invoke-Az @("provider", "register", "--namespace", $Namespace, "--only-show-errors")

    $deadline = (Get-Date).AddMinutes(10)
    do {
        Start-Sleep -Seconds 10
        $state = Capture-Az @("provider", "show", "--namespace", $Namespace, "--query", "registrationState", "-o", "tsv") -AllowFailure
        if ($state -eq "Registered") {
            Write-Ok "$Namespace provider registration completed."
            return
        }
    } while ((Get-Date) -lt $deadline)

    Fail "$Namespace provider registration did not complete within 10 minutes."
}

function Convert-LocationDisplayNameToName([string]$LocationDisplayName) {
    return ($LocationDisplayName -replace "\s+", "").ToLowerInvariant()
}

function Resolve-ContainerAppsLocation([string]$PreferredLocation, [string]$FallbackLocation) {
    $locationsJson = Capture-Az @(
        "provider", "show",
        "--namespace", "Microsoft.App",
        "--query", "resourceTypes[?resourceType=='managedEnvironments'].locations[]",
        "-o", "json"
    )

    $locations = ConvertFrom-Json -InputObject $locationsJson
    $eligibleNames = @()
    foreach ($providerLocation in $locations) {
        $eligibleNames += Convert-LocationDisplayNameToName ([string]$providerLocation)
    }

    if ($eligibleNames -contains $PreferredLocation) {
        return $PreferredLocation
    }

    if ($eligibleNames -contains $FallbackLocation) {
        Write-Warn "Azure Container Apps does not support '$PreferredLocation' in this subscription/provider metadata. Using '$FallbackLocation' as the nearest supported fallback."
        return $FallbackLocation
    }

    Fail "Neither preferred location '$PreferredLocation' nor fallback '$FallbackLocation' is supported by Azure Container Apps for this subscription."
}

function Remove-ContainerAppIfExists([string]$Name, [string]$Group) {
    $existing = Capture-Az @("containerapp", "show", "--name", $Name, "--resource-group", $Group, "--query", "id", "-o", "tsv") -AllowFailure
    if ($existing) {
        Write-Warn "Deleting Container App '$Name' to recreate a clean deployment target."
        Invoke-Az @("containerapp", "delete", "--name", $Name, "--resource-group", $Group, "--yes", "--only-show-errors")
    }
}

function Remove-ContainerAppsEnvironmentIfExists([string]$Name, [string]$Group) {
    $existing = Capture-Az @("containerapp", "env", "show", "--name", $Name, "--resource-group", $Group, "--query", "id", "-o", "tsv") -AllowFailure
    if ($existing) {
        Write-Warn "Deleting Container Apps environment '$Name' to recreate it in the supported location."
        Invoke-Az @("containerapp", "env", "delete", "--name", $Name, "--resource-group", $Group, "--yes", "--only-show-errors")
    }
}

function Wait-Http {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Url,
        [int]$TimeoutSeconds = 300,
        [scriptblock]$Accept
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = $null

    do {
        try {
            $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 30
            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 300) {
                if (-not $Accept -or (& $Accept $response)) {
                    Write-Ok "$Name is reachable: $Url"
                    return $response
                }
            }

            $lastError = "Unexpected status/content from $Url."
        }
        catch {
            $lastError = $_.Exception.Message
        }

        Start-Sleep -Seconds 5
    } while ((Get-Date) -lt $deadline)

    Fail "$Name did not become healthy within $TimeoutSeconds seconds. Last error: $lastError"
}

function Wait-Json {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Url,
        [int]$TimeoutSeconds = 300,
        [Parameter(Mandatory = $true)][scriptblock]$Accept
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastPayload = $null
    $lastError = $null

    do {
        try {
            $payload = Invoke-RestMethod -Uri $Url -TimeoutSec 30
            $lastPayload = $payload | ConvertTo-Json -Depth 8
            if (& $Accept $payload) {
                Write-Ok "$Name is healthy: $Url"
                return $payload
            }
        }
        catch {
            $lastError = $_.Exception.Message
        }

        Start-Sleep -Seconds 10
    } while ((Get-Date) -lt $deadline)

    if ($lastPayload) {
        Fail "$Name did not satisfy health criteria within $TimeoutSeconds seconds. Last payload:`n$lastPayload"
    }

    Fail "$Name did not satisfy health criteria within $TimeoutSeconds seconds. Last error: $lastError"
}

function Test-ChatSmoke([string]$Url) {
    $body = @{ message = "Oyak Dijital hangi hizmetleri sunar?" } | ConvertTo-Json
    $deadline = (Get-Date).AddSeconds($ChatTimeoutSeconds)
    $lastError = $null
    Ensure-Tool "curl.exe"

    do {
        $bodyFile = Join-Path ([IO.Path]::GetTempPath()) ("oyako-chat-smoke-" + [Guid]::NewGuid().ToString("N") + ".json")
        try {
            Set-Content -LiteralPath $bodyFile -Value $body -Encoding UTF8 -NoNewline
            $curlOutput = & curl.exe -sS -L --max-time 240 -H "Content-Type: application/json" --data-binary "@$bodyFile" $Url 2>&1
            $curlExitCode = $LASTEXITCODE
            $content = [string]($curlOutput -join [Environment]::NewLine)
            $flat = ($content -replace "\s+", " ")
            $hasError = $flat.Contains('"type":"error"') -or $flat.Contains("Service Unavailable") -or $flat.Contains("Beklenmedik bir hata")
            $hasStreamData = $flat.Contains("data:") -and $flat.Contains("answer_content")

            if ($curlExitCode -eq 0 -and -not $hasError -and $hasStreamData) {
                Write-Ok "Chat smoke test passed."
                return
            }

            $lastError = "Chat response did not contain a successful streaming answer payload. curl exit code: $curlExitCode"
        }
        catch {
            $lastError = $_.Exception.Message
        }
        finally {
            Remove-Item -LiteralPath $bodyFile -Force -ErrorAction SilentlyContinue
        }

        Start-Sleep -Seconds 10
    } while ((Get-Date) -lt $deadline)

    Fail "Chat smoke test failed within $ChatTimeoutSeconds seconds. Last error: $lastError"
}

try {
    Set-Location -LiteralPath $Root

    Write-Step "Checking local prerequisites"
    Ensure-Tool "az"
    Ensure-Tool "dotnet"
    Ensure-Tool "npm"
    Ensure-Tool "robocopy"
    Ensure-Tool "docker"

    $azureEnvPath = Join-Path $Root "azure-cloud.env"
    $ollamaEnvPath = Join-Path $Root "ollama-cloud.env"
    $apiDir = Join-Path $Root "webapi-oyako"
    $webDir = Join-Path $Root "webapp-oyako"

    foreach ($path in @($azureEnvPath, $ollamaEnvPath, (Join-Path $Root "Dockerfile"), (Join-Path $Root "docker\nginx.conf"), (Join-Path $Root "docker\entrypoint.sh"))) {
        if (-not (Test-Path -LiteralPath $path)) {
            Fail "Required file was not found: $path"
        }
    }

    $azureEnv = Read-EnvFile $azureEnvPath
    $ollamaEnv = Read-EnvFile $ollamaEnvPath

    foreach ($key in @("AzureAi__Endpoint", "AzureAi__DeploymentName", "AzureAi__Deployments__0", "AzureAi__ApiVersion", "AzureAi__ApiKey")) {
        Require-Key $azureEnv $key "azure-cloud.env"
    }

    Require-Key $ollamaEnv "ollama_api_key" "ollama-cloud.env"
    Write-Ok "Required env files and secret keys are present."

    Write-Step "Selecting Azure subscription"
    $accountsJson = Capture-Az @("account", "list", "-o", "json")
    $accounts = $accountsJson | ConvertFrom-Json
    $account = @($accounts | Where-Object { $_.name -eq $SubscriptionName }) | Select-Object -First 1
    if (-not $account) {
        Fail "Azure subscription '$SubscriptionName' was not found. Run 'az login' and ensure the subscription is available."
    }

    Invoke-Az @("account", "set", "--subscription", $SubscriptionName)
    $subscriptionId = Capture-Az @("account", "show", "--query", "id", "-o", "tsv")
    Write-Ok "Using subscription $SubscriptionName ($subscriptionId)."

    Write-Step "Validating Azure resource group and location"
    $resourceGroupId = Capture-Az @("group", "show", "--name", $ResourceGroup, "--query", "id", "-o", "tsv") -AllowFailure
    if (-not $resourceGroupId) {
        Fail "Resource group '$ResourceGroup' was not found in subscription '$SubscriptionName'."
    }

    $locationName = Capture-Az @("account", "list-locations", "--query", "[?name=='$RequestedLocation'].name | [0]", "-o", "tsv")
    if ($locationName -ne $RequestedLocation) {
        Fail "Azure location '$RequestedLocation' is not available for this subscription."
    }

    Ensure-AzureProvider "Microsoft.App"
    Ensure-AzureProvider "Microsoft.ContainerRegistry"
    $Location = Resolve-ContainerAppsLocation -PreferredLocation $RequestedLocation -FallbackLocation $FallbackContainerAppsLocation
    Write-Ok "Using Azure resource location '$Location'."

    $script:SourceRevision = Get-SourceRevision
    Write-Step "Building local projects"
    Invoke-External -Exe "dotnet" -Arguments @("build", $apiDir)

    if (-not (Test-Path -LiteralPath (Join-Path $webDir "node_modules"))) {
        Push-Location -LiteralPath $webDir
        try {
            Invoke-External -Exe "npm" -Arguments @("ci") | Out-Null
        }
        finally {
            Pop-Location
        }
    }

    Push-Location -LiteralPath $webDir
    try {
        Invoke-External -Exe "npm" -Arguments @("run", "build") | Out-Null
    }
    finally {
        Pop-Location
    }

    Write-Step "Creating or validating Azure Container Registry"
    $subscriptionKey = ($subscriptionId -replace "-", "").Substring(0, 8).ToLowerInvariant()
    $legacyAcrName = "acaoyako$subscriptionKey" + "acr"
    $acrName = "acaoyako$subscriptionKey" + "weacr"

    if ($legacyAcrName -ne $acrName) {
        $legacyAcrJson = Capture-Az @("acr", "show", "--name", $legacyAcrName, "-o", "json") -AllowFailure
        if ($legacyAcrJson) {
            $legacyAcr = $legacyAcrJson | ConvertFrom-Json
            Write-Warn "Deleting legacy script-owned ACR '$legacyAcrName' to avoid stale data-plane name reuse."
            Invoke-Az @("acr", "delete", "--name", $legacyAcrName, "--resource-group", $legacyAcr.resourceGroup, "--yes", "--only-show-errors")
        }
    }

    $acrJson = Capture-Az @("acr", "show", "--name", $acrName, "-o", "json") -AllowFailure

    if ($acrJson) {
        $existingAcr = $acrJson | ConvertFrom-Json
        if ($existingAcr.resourceGroup -ne $ResourceGroup -or (Convert-LocationDisplayNameToName ([string]$existingAcr.location)) -ne $AcrLocation) {
            Write-Warn "Existing ACR '$acrName' is in resource group '$($existingAcr.resourceGroup)' and location '$($existingAcr.location)'. Recreating it in '$ResourceGroup/$AcrLocation'."
            Invoke-Az @("acr", "delete", "--name", $acrName, "--resource-group", $existingAcr.resourceGroup, "--yes", "--only-show-errors")
            $acrJson = $null
        }
    }

    if (-not $acrJson) {
        Invoke-Az @("acr", "create", "--name", $acrName, "--resource-group", $ResourceGroup, "--location", $AcrLocation, "--sku", "Basic", "--admin-enabled", "false", "--only-show-errors")
    }

    $acr = (Capture-Az @("acr", "show", "--name", $acrName, "-o", "json")) | ConvertFrom-Json
    if ($acr.resourceGroup -ne $ResourceGroup) {
        Fail "ACR '$acrName' exists in resource group '$($acr.resourceGroup)', but '$ResourceGroup' is required."
    }

    if ((Convert-LocationDisplayNameToName ([string]$acr.location)) -ne $AcrLocation) {
        Fail "ACR '$acrName' exists in location '$($acr.location)', but '$AcrLocation' is required."
    }

    $acrId = [string]$acr.id
    $acrLoginServer = [string]$acr.loginServer
    Write-Ok "Using ACR $acrName ($acrLoginServer)."

    Write-Step "Clearing previous ACR image repository"
    Remove-AcrImageRepositoryIfExists -AcrName $acrName -Repository $ImageRepository

    Write-Step "Building and pushing image with local Docker"
    $imageTag = "latest"
    $imageName = "$ImageRepository`:$imageTag"
    $fullImage = "$acrLoginServer/$imageName"
    $script:DeployContext = New-DeploymentContext $Root
    Invoke-ImageBuildAndPush -AcrName $acrName -ImageName $imageName -FullImage $fullImage -DockerfilePath (Join-Path $script:DeployContext "Dockerfile") -ContextPath $script:DeployContext

    Write-Step "Creating or validating Azure Container Apps environment"
    $envJson = Capture-Az @("containerapp", "env", "show", "--name", $EnvironmentName, "--resource-group", $ResourceGroup, "-o", "json") -AllowFailure
    if ($envJson) {
        $existingEnv = $envJson | ConvertFrom-Json
        if ((Convert-LocationDisplayNameToName ([string]$existingEnv.location)) -ne $Location) {
            Remove-ContainerAppIfExists -Name $ContainerAppName -Group $ResourceGroup
            Remove-ContainerAppsEnvironmentIfExists -Name $EnvironmentName -Group $ResourceGroup
            $envJson = $null
        }
    }

    if (-not $envJson) {
        Invoke-Az @(
            "containerapp", "env", "create",
            "--name", $EnvironmentName,
            "--resource-group", $ResourceGroup,
            "--location", $Location,
            "--logs-destination", "none",
            "--enable-workload-profiles", "false",
            "--only-show-errors"
        )
    }

    $acaEnv = (Capture-Az @("containerapp", "env", "show", "--name", $EnvironmentName, "--resource-group", $ResourceGroup, "-o", "json")) | ConvertFrom-Json
    if ((Convert-LocationDisplayNameToName ([string]$acaEnv.location)) -ne $Location) {
        Fail "Container Apps environment '$EnvironmentName' exists in '$($acaEnv.location)', but '$Location' is required."
    }

    Write-Ok "Using Container Apps environment $EnvironmentName."

    Write-Step "Creating or validating Container App"
    $appJson = Capture-Az @("containerapp", "show", "--name", $ContainerAppName, "--resource-group", $ResourceGroup, "-o", "json") -AllowFailure
    if ($appJson) {
        $app = $appJson | ConvertFrom-Json
        $actualEnvironmentId = [string]$app.properties.managedEnvironmentId
        if ($actualEnvironmentId -and -not $actualEnvironmentId.EndsWith("/managedEnvironments/$EnvironmentName", [StringComparison]::OrdinalIgnoreCase)) {
            Write-Warn "Container App '$ContainerAppName' is attached to another environment. Recreating it in '$EnvironmentName'."
            Invoke-Az @("containerapp", "delete", "--name", $ContainerAppName, "--resource-group", $ResourceGroup, "--yes", "--only-show-errors")
            $appJson = $null
        }
    }

    if (-not $appJson) {
        Invoke-Az @(
            "containerapp", "create",
            "--name", $ContainerAppName,
            "--resource-group", $ResourceGroup,
            "--environment", $EnvironmentName,
            "--image", $PlaceholderImage,
            "--ingress", "external",
            "--target-port", "80",
            "--revisions-mode", "single",
            "--min-replicas", $MinReplicas,
            "--max-replicas", $MaxReplicas,
            "--cpu", $Cpu,
            "--memory", $Memory,
            "--system-assigned",
            "--only-show-errors"
        )
    }
    else {
        Invoke-Az @("containerapp", "identity", "assign", "--name", $ContainerAppName, "--resource-group", $ResourceGroup, "--system-assigned", "--only-show-errors")
    }

    $principalId = $null
    $deadline = (Get-Date).AddMinutes(3)
    do {
        $principalId = Capture-Az @("containerapp", "show", "--name", $ContainerAppName, "--resource-group", $ResourceGroup, "--query", "identity.principalId", "-o", "tsv") -AllowFailure
        if ($principalId) {
            break
        }

        Start-Sleep -Seconds 5
    } while ((Get-Date) -lt $deadline)

    if (-not $principalId) {
        Fail "Container App system-assigned identity principalId was not created."
    }

    Write-Step "Granting Container App access to ACR"
    $roleAssignmentId = Capture-Az @(
        "role", "assignment", "list",
        "--assignee", $principalId,
        "--role", "AcrPull",
        "--scope", $acrId,
        "--query", "[0].id",
        "-o", "tsv"
    ) -AllowFailure

    if (-not $roleAssignmentId) {
        Invoke-Az @("role", "assignment", "create", "--assignee", $principalId, "--role", "AcrPull", "--scope", $acrId, "--only-show-errors")
        Start-Sleep -Seconds 20
    }

    Invoke-Az @("containerapp", "registry", "set", "--name", $ContainerAppName, "--resource-group", $ResourceGroup, "--server", $acrLoginServer, "--identity", "system", "--only-show-errors")

    Write-Step "Applying Container App secrets and environment variables"
    Invoke-Az @(
        "containerapp", "secret", "set",
        "--name", $ContainerAppName,
        "--resource-group", $ResourceGroup,
        "--secrets",
        "azure-ai-api-key=$($azureEnv["AzureAi__ApiKey"])",
        "ollama-api-key=$($ollamaEnv["ollama_api_key"])",
        "--only-show-errors"
    ) -Sensitive

    $envVars = @(
        "OYAKO_DOCKER=1",
        "ASPNETCORE_ENVIRONMENT=Production",
        "Ai__DefaultProvider=$DefaultAiProvider",
        "Ai__DisabledProviders__0=ollama-local",
        "AzureAi__Endpoint=$($azureEnv["AzureAi__Endpoint"])",
        "AzureAi__DeploymentName=$($azureEnv["AzureAi__DeploymentName"])",
        "AzureAi__Deployments__0=$($azureEnv["AzureAi__Deployments__0"])",
        "AzureAi__ApiVersion=$($azureEnv["AzureAi__ApiVersion"])",
        "AzureAi__TimeoutSeconds=$(Get-OrDefault $azureEnv "AzureAi__TimeoutSeconds" "180")",
        "AzureAi__Temperature=$(Get-OrDefault $azureEnv "AzureAi__Temperature" "0.2")",
        "AzureAi__ApiKey=secretref:azure-ai-api-key",
        "OllamaCloud__BaseUrl=https://ollama.com",
        "OllamaCloud__Model=minimax-m3:cloud",
        "OllamaCloud__TimeoutSeconds=180",
        "OllamaCloud__Temperature=0.2",
        "OllamaCloud__ApiKey=secretref:ollama-api-key",
        "ollama_api_key=secretref:ollama-api-key"
    )

    $updateArgs = @(
        "containerapp", "update",
        "--name", $ContainerAppName,
        "--resource-group", $ResourceGroup,
        "--image", $fullImage,
        "--cpu", $Cpu,
        "--memory", $Memory,
        "--min-replicas", $MinReplicas,
        "--max-replicas", $MaxReplicas,
        "--only-show-errors",
        "--set-env-vars"
    ) + $envVars

    Invoke-Az $updateArgs

    Invoke-Az @(
        "containerapp", "ingress", "enable",
        "--name", $ContainerAppName,
        "--resource-group", $ResourceGroup,
        "--type", "external",
        "--target-port", $TargetPort,
        "--transport", "auto",
        "--only-show-errors"
    )

    Write-Step "Reading public Azure Container Apps URLs"
    $fqdn = Capture-Az @("containerapp", "show", "--name", $ContainerAppName, "--resource-group", $ResourceGroup, "--query", "properties.configuration.ingress.fqdn", "-o", "tsv")
    if (-not $fqdn) {
        Fail "Container App FQDN was empty."
    }

    if (-not $fqdn.StartsWith("oyako.", [StringComparison]::OrdinalIgnoreCase)) {
        Fail "Expected FQDN prefix 'oyako', but Azure returned '$fqdn'."
    }

    $webUrl = "https://$fqdn/"
    $apiBaseUrl = "https://$fqdn/api"
    $apiHealthUrl = "$apiBaseUrl/api-health"
    $knowledgeHealthUrl = "$apiBaseUrl/knowledge-health"
    $chatStreamUrl = "$apiBaseUrl/chat/stream"

    Write-Step "Running public endpoint checks"
    Wait-Http -Name "Web UI" -Url $webUrl -TimeoutSeconds $WebTimeoutSeconds -Accept { param($response) $response.Content.Contains("Oyako") } | Out-Null

    $apiHealth = Wait-Json -Name "API health" -Url $apiHealthUrl -TimeoutSeconds $WebTimeoutSeconds -Accept {
        param($payload)
        $providers = @($payload.providerStatuses | ForEach-Object { $_.name })
        $expectedModel = if ($DefaultAiProvider -eq "azure") { [string]$azureEnv["AzureAi__DeploymentName"] } else { "minimax-m3:cloud" }
        return $payload.status -eq "ready" `
            -and $payload.activeAiProvider -eq $DefaultAiProvider `
            -and $payload.activeAiModel -eq $expectedModel `
            -and ($providers -contains "azure") `
            -and ($providers -contains "ollama-cloud") `
            -and -not ($providers -contains "ollama-local")
    }

    $knowledgeHealth = Wait-Json -Name "Knowledge health" -Url $knowledgeHealthUrl -TimeoutSeconds $KnowledgeTimeoutSeconds -Accept {
        param($payload)
        return ([int]$payload.sourceCount -ge 1) -and ([int]$payload.pageCount -ge 1) -and (($payload.cache -eq "ok") -or ($payload.status -eq "ready") -or ($payload.status -eq "ready_with_warnings"))
    }

    Assert-AcrLatestOnly -AcrName $acrName -Repository $ImageRepository

    if (-not $SkipSmokeTest) {
        Test-ChatSmoke -Url $chatStreamUrl
    }
    else {
        Write-Warn "Chat smoke test was skipped by --skip-smoke-test."
    }

    $revisionName = Capture-Az @("containerapp", "revision", "list", "--name", $ContainerAppName, "--resource-group", $ResourceGroup, "--query", "[?properties.active==``true``][0].name", "-o", "tsv") -AllowFailure

    Write-Host ""
    Write-Host "Oyako Azure Container Apps deployment completed." -ForegroundColor Green
    Write-Host ""
    Write-Host "Web UI:            $webUrl"
    Write-Host "API base:          $apiBaseUrl"
    Write-Host "API health:        $apiHealthUrl"
    Write-Host "Knowledge health:  $knowledgeHealthUrl"
    Write-Host "ACR image:         $fullImage"
    Write-Host "Resource group:    $ResourceGroup"
    Write-Host "Location:          $Location"
    Write-Host "ACA environment:   $EnvironmentName"
    Write-Host "Container app:     $ContainerAppName"
    if ($revisionName) {
        Write-Host "Active revision:   $revisionName"
    }
    Write-Host ""
    Write-Host "API status:        $($apiHealth.status)"
    Write-Host "Knowledge status:  $($knowledgeHealth.status)"
    Write-Host "Sources/pages:     $($knowledgeHealth.sourceCount)/$($knowledgeHealth.pageCount)"
    Write-Host ""
    Write-Host "Logs command:"
    Write-Host "  az containerapp logs show --name $ContainerAppName --resource-group $ResourceGroup --follow"
    Remove-DeploymentContext
    exit 0
}
catch {
    Remove-DeploymentContext
    Write-Host ""
    Write-Host "Oyako Azure Container Apps deployment failed." -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ""
    Write-Host "Useful diagnostics:"
    Write-Host "  az account show"
    Write-Host "  az containerapp show --name $ContainerAppName --resource-group $ResourceGroup"
    Write-Host "  az containerapp logs show --name $ContainerAppName --resource-group $ResourceGroup --tail 200"
    exit 1
}



