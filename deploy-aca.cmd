@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "OYAKO_SCRIPT_SELF=%~f0"
set "OYAKO_SCRIPT_PS1=%TEMP%\oyako-deploy-aca-%RANDOM%-%RANDOM%.ps1"

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
$DesiredAppName = "oyako"
$DesiredEnvironmentName = "aca-oyako-env"
$DesiredDefaultDomain = "ambitiousrock-ed5a5643.italynorth.azurecontainerapps.io"
$ExpectedFqdn = "$DesiredAppName.$DesiredDefaultDomain"
$PreviousAppName = "oyako-aca"
$PreviousEnvironmentName = "oyako-aca-env"
$AppName = $DesiredAppName
$EnvironmentName = $DesiredEnvironmentName
$ImageRepository = "oyako"
$ImageTag = "latest"
$Scope = "oyako-aca"
$ManagedBy = "deploy-aca"
$BootstrapImage = "mcr.microsoft.com/k8se/quickstart:latest"
$TargetPort = "8080"
$Cpu = "0.25"
$Memory = "0.5Gi"
$SmokeTimeoutSeconds = 240
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

function Wait-ResourceGone([string]$Description, [scriptblock]$Exists) {
    $deadline = (Get-Date).AddMinutes(20)
    do {
        if (-not (& $Exists)) {
            Ok "$Description removed."
            return
        }
        Start-Sleep -Seconds 10
    } while ((Get-Date) -lt $deadline)

    Fail "$Description was still visible in Azure after 20 minutes."
}

function Wait-Text([string]$Description, [scriptblock]$Resolver, [int]$TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        $value = [string](& $Resolver)
        if (-not [string]::IsNullOrWhiteSpace($value)) {
            return $value
        }
        Start-Sleep -Seconds 5
    } while ((Get-Date) -lt $deadline)

    Fail "Azure did not return $Description within $TimeoutSeconds seconds."
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

function Get-Acr([string]$Name) {
    $result = TryAz @("acr", "show", "--name", $Name, "-o", "json")
    if (-not $result.Ok) { return $null }
    return FromJson $result.Text
}

function Get-ContainerAppByName([string]$Name) {
    $result = TryAz @("containerapp", "show", "--name", $Name, "--resource-group", $ResourceGroup, "-o", "json")
    if (-not $result.Ok) { return $null }
    return FromJson $result.Text
}

function Get-ContainerEnvironmentByName([string]$Name) {
    $result = TryAz @("containerapp", "env", "show", "--name", $Name, "--resource-group", $ResourceGroup, "-o", "json")
    if (-not $result.Ok) { return $null }
    return FromJson $result.Text
}

function Get-ContainerApp() {
    return Get-ContainerAppByName $AppName
}

function Get-ContainerEnvironment() {
    return Get-ContainerEnvironmentByName $EnvironmentName
}

function Remove-OwnedContainerAppByName([string]$Name, [string]$Description) {
    $app = Get-ContainerAppByName $Name
    Assert-OwnedOrMissing $app $Name "Container App"
    if ($app) {
        Step "Removing $Description Container App $Name"
        Az @("containerapp", "delete", "--name", $Name, "--resource-group", $ResourceGroup, "--yes", "--no-wait")
        Wait-ResourceGone "Container App $Name" { [bool](Get-ContainerAppByName $Name) }
    }
}

function Remove-OwnedEnvironmentByName([string]$Name, [string]$Description) {
    $env = Get-ContainerEnvironmentByName $Name
    Assert-OwnedOrMissing $env $Name "Container Apps Environment"
    if ($env) {
        Step "Removing $Description Container Apps Environment $Name"
        Az @("containerapp", "env", "delete", "--name", $Name, "--resource-group", $ResourceGroup, "--yes", "--no-wait")
        Wait-ResourceGone "Container Apps Environment $Name" { [bool](Get-ContainerEnvironmentByName $Name) }
    }
}

function Remove-OwnedContainerApp() {
    Remove-OwnedContainerAppByName $AppName "non-compliant"
}

function Remove-OwnedEnvironment() {
    Remove-OwnedEnvironmentByName $EnvironmentName "non-compliant"
}

function Remove-PreviousAcaCutoverResources([string]$LegacyAcrName) {
    if ($PreviousAppName -ne $AppName) {
        Remove-OwnedContainerAppByName $PreviousAppName "previous cutover"
    }

    if ($PreviousEnvironmentName -ne $EnvironmentName) {
        Remove-OwnedEnvironmentByName $PreviousEnvironmentName "previous cutover"
    }

    if ($LegacyAcrName -ne $acrName) {
        $legacyAcr = Get-Acr $LegacyAcrName
        if ($legacyAcr) {
            if ([string]$legacyAcr.resourceGroup -ne $ResourceGroup) {
                Fail "Legacy ACR '$LegacyAcrName' exists outside $ResourceGroup. Refusing to modify it."
            }

            Step "Removing legacy ACR $LegacyAcrName"
            Az @("acr", "delete", "--name", $LegacyAcrName, "--resource-group", $ResourceGroup, "--yes")
            Wait-ResourceGone "Legacy ACR $LegacyAcrName" { [bool](Get-Acr $LegacyAcrName) }
        }
    }
}

function Get-AllContainerEnvironments() {
    $text = AzText @("containerapp", "env", "list", "-o", "json") -Quiet
    $items = @(FromJson $text)
    return @($items)
}

function Get-EnvironmentDefaultDomain($Environment) {
    if (-not $Environment -or -not $Environment.properties) { return "" }
    return [string]$Environment.properties.defaultDomain
}

function Assert-DesiredEnvironment($Environment) {
    if (-not $Environment) { Fail "Container Apps Environment was not returned by Azure." }
    $name = [string]$Environment.name
    $resourceGroup = [string]$Environment.resourceGroup
    $locationMatches = (Normalize-Location ([string]$Environment.location)) -eq $Location
    $domain = Get-EnvironmentDefaultDomain $Environment

    if ($name -ne $DesiredEnvironmentName) {
        Fail "Desired ACA domain '$DesiredDefaultDomain' is active on environment '$name', but this script manages '$DesiredEnvironmentName'. Refusing to use or modify it."
    }
    if ($resourceGroup -ne $ResourceGroup) {
        Fail "Desired ACA domain '$DesiredDefaultDomain' is active in resource group '$resourceGroup', but '$ResourceGroup' is required."
    }
    if (-not $locationMatches) {
        Fail "Desired ACA domain '$DesiredDefaultDomain' is active in location '$($Environment.location)', but '$Location' is required."
    }
    if ($domain -ne $DesiredDefaultDomain) {
        Fail "Container Apps Environment '$name' returned domain '$domain'; expected '$DesiredDefaultDomain'."
    }
    Assert-OwnedOrMissing $Environment $name "Container Apps Environment"
}

function Ensure-DesiredManagedDomain() {
    Step "Checking deterministic ACA managed domain"
    $matches = @(Get-AllContainerEnvironments | Where-Object { (Get-EnvironmentDefaultDomain $_) -eq $DesiredDefaultDomain })
    if ($matches.Count -gt 1) {
        $names = ($matches | ForEach-Object { "$($_.resourceGroup)/$($_.name)" }) -join ", "
        Fail "Desired ACA domain '$DesiredDefaultDomain' is active on multiple environments: $names"
    }

    if ($matches.Count -eq 1) {
        Assert-DesiredEnvironment $matches[0]
        $script:EnvironmentName = [string]$matches[0].name
        Ok "Desired ACA managed domain is already active on $ResourceGroup/$EnvironmentName."
        return
    }

    $existing = Get-ContainerEnvironmentByName $DesiredEnvironmentName
    Assert-OwnedOrMissing $existing $DesiredEnvironmentName "Container Apps Environment"
    if ($existing) {
        $existingDomain = Get-EnvironmentDefaultDomain $existing
        $locationMatches = (Normalize-Location ([string]$existing.location)) -eq $Location
        if ($locationMatches -and $existingDomain -eq $DesiredDefaultDomain) {
            Ok "Desired ACA managed domain is already active on $ResourceGroup/$DesiredEnvironmentName."
            return
        }

        Remove-OwnedContainerAppByName $DesiredAppName "stale target"
        Remove-OwnedEnvironmentByName $DesiredEnvironmentName "stale target"
    }

    Step "Trying one ACA environment recreate for managed domain reclaim"
    Az ((@(
        "containerapp", "env", "create",
        "--name", $DesiredEnvironmentName,
        "--resource-group", $ResourceGroup,
        "--location", $Location,
        "--enable-workload-profiles", "false",
        "--logs-destination", "none",
        "--tags"
    ) + $Tags))

    $envId = Wait-Text "Container Apps Environment id" {
        $env = Get-ContainerEnvironmentByName $DesiredEnvironmentName
        if ($env) { [string]$env.id } else { "" }
    } 600
    Tag-Resource $envId

    $actualDomain = Wait-Text "Container Apps Environment default domain" {
        $env = Get-ContainerEnvironmentByName $DesiredEnvironmentName
        if ($env) { Get-EnvironmentDefaultDomain $env } else { "" }
    } 600

    if ($actualDomain -ne $DesiredDefaultDomain) {
        Remove-OwnedEnvironmentByName $DesiredEnvironmentName "failed managed-domain reclaim"
        $supportResourceId = "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroup/providers/Microsoft.App/managedEnvironments/$DesiredEnvironmentName"
        Fail "Azure assigned ACA default domain '$actualDomain', not '$DesiredDefaultDomain'. The deleted environment name cannot be reclaimed by this script. Open a Microsoft Support case for '$supportResourceId', reference old suffix '$DesiredDefaultDomain', old app '$DesiredAppName', and deletion correlation id 'af9fe413-26fd-448a-a7db-91d0640ea5af'."
    }

    Ok "Reclaimed desired ACA managed domain $DesiredDefaultDomain."
}

function Ensure-Environment() {
    $env = Get-ContainerEnvironment
    Assert-OwnedOrMissing $env $EnvironmentName "Container Apps Environment"
    if ($env) {
        $locationMatches = (Normalize-Location ([string]$env.location)) -eq $Location
        $domainMatches = (Get-EnvironmentDefaultDomain $env) -eq $DesiredDefaultDomain
        if ($locationMatches -and $domainMatches) {
            Ok "Using existing Container Apps Environment $EnvironmentName."
            Tag-Resource ([string]$env.id)
            return
        }

        Fail "Container Apps Environment '$EnvironmentName' is not the reclaimed target. Location='$($env.location)', domain='$(Get-EnvironmentDefaultDomain $env)'."
    }

    Fail "Container Apps Environment '$EnvironmentName' is missing after managed-domain reclaim."
}

function Ensure-ContainerAppBootstrap() {
    $app = Get-ContainerApp
    Assert-OwnedOrMissing $app $AppName "Container App"
    if ($app) {
        $environmentMatches = ([string]$app.properties.environmentId).EndsWith("/$EnvironmentName", [StringComparison]::OrdinalIgnoreCase)
        $locationMatches = (Normalize-Location ([string]$app.location)) -eq $Location
        if ($environmentMatches -and $locationMatches) {
            Ok "Using existing Container App $AppName."
            return
        }

        Remove-OwnedContainerApp
    }

    Step "Creating Container App bootstrap identity"
    Az ((@(
        "containerapp", "create",
        "--name", $AppName,
        "--resource-group", $ResourceGroup,
        "--environment", $EnvironmentName,
        "--image", $BootstrapImage,
        "--ingress", "external",
        "--target-port", "80",
        "--revisions-mode", "single",
        "--min-replicas", "1",
        "--max-replicas", "1",
        "--cpu", $Cpu,
        "--memory", $Memory,
        "--system-assigned",
        "--tags"
    ) + $Tags))
    $appId = Wait-Text "Container App id" {
        $app = Get-ContainerApp
        if ($app) { [string]$app.id } else { "" }
    } 240
}

function Ensure-AcrPull([string]$AcrId) {
    Step "Ensuring managed identity AcrPull"
    $principalId = Wait-Text "Container App principal id" {
        $app = Get-ContainerApp
        if ($app) { [string]$app.identity.principalId } else { "" }
    } 240

    $assignmentId = AzText @("role", "assignment", "list", "--assignee", $principalId, "--role", "AcrPull", "--scope", $AcrId, "--query", "[0].id", "-o", "tsv") -Quiet
    if ([string]::IsNullOrWhiteSpace($assignmentId)) {
        Az @("role", "assignment", "create", "--assignee", $principalId, "--role", "AcrPull", "--scope", $AcrId)
        $assignmentId = Wait-Text "AcrPull role assignment id" {
            AzText @("role", "assignment", "list", "--assignee", $principalId, "--role", "AcrPull", "--scope", $AcrId, "--query", "[0].id", "-o", "tsv") -Quiet
        } 120
        Start-Sleep -Seconds 15
    }

    Az @("containerapp", "registry", "set", "--name", $AppName, "--resource-group", $ResourceGroup, "--server", $acrLoginServer, "--identity", "system")
}

try {
    Set-Location -LiteralPath $Root

    Step "Checking local prerequisites and strict env files"
    Resolve-Exe "az" | Out-Null
    Resolve-Exe "docker" | Out-Null
    Run -Exe "docker" -Arguments @("version") -Quiet | Out-Null

    $azureEnv = Read-EnvFile (Join-Path $Root "azure-cloud.env")
    $ollamaEnv = Read-EnvFile (Join-Path $Root "ollama-cloud.env")
    Require-EnvKeys $azureEnv @("AzureAi__Endpoint", "AzureAi__DeploymentName", "AzureAi__Deployments__0", "AzureAi__ApiVersion", "AzureAi__ApiKey") "azure-cloud.env"
    Require-EnvKeys $ollamaEnv @("ollama_api_key") "ollama-cloud.env"
    Ok "Required env files and keys are present."

    Step "Selecting Azure subscription"
    $account = TryAz @("account", "show", "-o", "json")
    if (-not $account.Ok) {
        Fail "Azure CLI is not logged in. Run 'az login' manually, then retry deploy-aca.cmd."
    }
    Az @("account", "set", "--subscription", $Subscription)
    $subscriptionId = AzText @("account", "show", "--query", "id", "-o", "tsv") -Quiet
    $script:SubscriptionId = $subscriptionId
    if ([string]::IsNullOrWhiteSpace($subscriptionId)) { Fail "Azure subscription id could not be resolved after selecting '$Subscription'." }
    Ok "Using subscription $Subscription ($subscriptionId)."

    Step "Validating Azure capabilities"
    Ensure-Provider "Microsoft.App"
    Ensure-Provider "Microsoft.ContainerRegistry"
    $locationName = AzText @("account", "list-locations", "--query", "[?name=='$Location'].name | [0]", "-o", "tsv") -Quiet
    if ($locationName -ne $Location) { Fail "Azure location '$Location' is not available for this subscription." }
    $acaLocations = AzText @("provider", "show", "--namespace", "Microsoft.App", "--query", "resourceTypes[?resourceType=='managedEnvironments'].locations[]", "-o", "json") -Quiet | ConvertFrom-Json
    if (@($acaLocations | ForEach-Object { Normalize-Location ([string]$_) }) -notcontains $Location) {
        Fail "Azure Container Apps managed environments are not available in '$Location'."
    }
    $envHelp = Run "az" @("containerapp", "env", "create", "--help") -Quiet
    if ($envHelp -notmatch "--logs-destination" -or $envHelp -notmatch "--enable-workload-profiles") {
        Fail "Azure CLI containerapp env create lacks required no-Log-Analytics arguments."
    }

    Step "Ensuring resource group"
    $rg = TryAz @("group", "show", "--name", $ResourceGroup, "--query", "id", "-o", "tsv")
    if (-not $rg.Ok -or [string]::IsNullOrWhiteSpace($rg.Text)) {
        Az @("group", "create", "--name", $ResourceGroup, "--location", $Location)
    }

    Ensure-DesiredManagedDomain

    Step "Ensuring deterministic ACR"
    $subscriptionCompact = $subscriptionId.Replace("-", "").ToLowerInvariant()
    $acrName = "oyakoacr$($subscriptionCompact.Substring(0, 12))"
    $legacyAcrName = "acaoyako$($subscriptionCompact.Substring(0, 8))weacr"
    $acr = Get-Acr $acrName
    Assert-OwnedOrMissing $acr $acrName "Azure Container Registry"
    if ($acr -and (([string]$acr.resourceGroup -ne $ResourceGroup -or (Normalize-Location ([string]$acr.location)) -ne $Location))) {
        Step "Removing non-compliant ACR $acrName"
        Az @("acr", "delete", "--name", $acrName, "--resource-group", $acr.resourceGroup, "--yes")
        $acr = $null
    }
    if (-not $acr) {
        Az ((@("acr", "create", "--name", $acrName, "--resource-group", $ResourceGroup, "--location", $Location, "--sku", "Basic", "--admin-enabled", "false", "--tags") + $Tags))
    }
    $acr = Get-Acr $acrName
    if (-not $acr) { Fail "ACR '$acrName' was not available after create/show." }
    if ([string]$acr.sku.name -ne "Basic") { Fail "ACR '$acrName' exists but is not Basic SKU." }
    Tag-Resource ([string]$acr.id)
    Az @("acr", "update", "--name", $acrName, "--admin-enabled", "false")
    $acrLoginServer = [string]$acr.loginServer
    $acrId = [string]$acr.id
    Ok "Using ACR $acrName ($acrLoginServer)."

    Step "Building and pushing oyako:latest"
    $env:DOCKER_BUILDKIT = "1"
    $localImage = "$ImageRepository`:$ImageTag"
    $remoteImage = "$acrLoginServer/$ImageRepository`:$ImageTag"
    $repoProbe = TryAz @("acr", "repository", "show", "--name", $acrName, "--repository", $ImageRepository, "-o", "json")
    if ($repoProbe.Ok) {
        Az @("acr", "repository", "delete", "--name", $acrName, "--repository", $ImageRepository, "--yes")
    }
    Run "docker" @("build", "--quiet", "--force-rm", "-t", $localImage, ".") | Out-Null
    Run -Exe "docker" -Arguments @("tag", $localImage, $remoteImage) -Quiet | Out-Null
    Az @("acr", "login", "--name", $acrName)
    Run "docker" @("push", "--quiet", $remoteImage) | Out-Null
    $imageTags = @(AzText @("acr", "repository", "show-tags", "--name", $acrName, "--repository", $ImageRepository, "-o", "json") -Quiet | ConvertFrom-Json)
    if ($imageTags.Count -ne 1 -or $imageTags[0] -ne $ImageTag) {
        Fail "ACR repository '$ImageRepository' must contain only '$ImageTag', found: $($imageTags -join ', ')"
    }

    Step "Ensuring Container Apps resources"
    Ensure-Environment
    Ensure-ContainerAppBootstrap

    Step "Applying Container App secrets"
    Az @(
        "containerapp", "secret", "set",
        "--name", $AppName,
        "--resource-group", $ResourceGroup,
        "--secrets",
        "azure-ai-api-key=$($azureEnv["AzureAi__ApiKey"])",
        "ollama-api-key=$($ollamaEnv["ollama_api_key"])"
    ) -Sensitive
    Ensure-AcrPull $acrId

    Step "Deploying Container App image"
    $aiProvider = EnvValue $azureEnv "Ai__DefaultProvider" "ollama-cloud"
    $envVars = @(
        "ASPNETCORE_ENVIRONMENT=Production",
        "ASPNETCORE_URLS=http://+:8080",
        "OYAKO_DOCKER=1",
        "Storage__DataRoot=/app/data",
        "Sqlite__ConnectionString=Data Source=/app/data/oyako.sqlite;Cache=Shared",
        "Ai__DefaultProvider=$aiProvider",
        "Ai__DisabledProviders__0=ollama-local",
        "AzureAi__Endpoint=$($azureEnv["AzureAi__Endpoint"])",
        "AzureAi__DeploymentName=$($azureEnv["AzureAi__DeploymentName"])",
        "AzureAi__Deployments__0=$($azureEnv["AzureAi__Deployments__0"])",
        "AzureAi__ApiVersion=$($azureEnv["AzureAi__ApiVersion"])",
        "AzureAi__TimeoutSeconds=$(EnvValue $azureEnv "AzureAi__TimeoutSeconds" "180")",
        "AzureAi__Temperature=$(EnvValue $azureEnv "AzureAi__Temperature" "0.2")",
        "AzureAi__ApiKey=secretref:azure-ai-api-key",
        "OllamaCloud__BaseUrl=$(EnvValue $ollamaEnv "OllamaCloud__BaseUrl" "https://ollama.com")",
        "OllamaCloud__Model=$(EnvValue $ollamaEnv "OllamaCloud__Model" "minimax-m3:cloud")",
        "OllamaCloud__TimeoutSeconds=$(EnvValue $ollamaEnv "OllamaCloud__TimeoutSeconds" "180")",
        "OllamaCloud__Temperature=$(EnvValue $ollamaEnv "OllamaCloud__Temperature" "0.2")",
        "OllamaCloud__ApiKey=secretref:ollama-api-key",
        "ollama_api_key=secretref:ollama-api-key",
        "DEPLOYMENT_TIMESTAMP=$([DateTimeOffset]::UtcNow.ToUnixTimeSeconds())"
    )
    $revisionSuffix = "d$([DateTimeOffset]::UtcNow.ToUnixTimeSeconds())"
    Az ((@(
        "containerapp", "update",
        "--name", $AppName,
        "--resource-group", $ResourceGroup,
        "--image", $remoteImage,
        "--revision-suffix", $revisionSuffix,
        "--cpu", $Cpu,
        "--memory", $Memory,
        "--min-replicas", "1",
        "--max-replicas", "1",
        "--replace-env-vars"
    ) + $envVars))
    Az @("containerapp", "ingress", "enable", "--name", $AppName, "--resource-group", $ResourceGroup, "--type", "external", "--target-port", $TargetPort, "--transport", "auto")

    $fqdn = Wait-Text "Container App FQDN" {
        AzText @("containerapp", "show", "--name", $AppName, "--resource-group", $ResourceGroup, "--query", "properties.configuration.ingress.fqdn", "-o", "tsv") -Quiet
    } 120
    if ($fqdn -ne $ExpectedFqdn) {
        Fail "Azure returned Container App FQDN '$fqdn'; expected '$ExpectedFqdn'."
    }
    $baseUrl = "https://$fqdn"
    $apiBaseUrl = "$baseUrl/api"

    Step "Running smoke tests"
    $rootSmoke = Wait-Smoke "ACA frontend root" "$baseUrl/" $SmokeTimeoutSeconds "Oyako"
    $healthSmoke = Wait-Smoke "ACA /health" "$baseUrl/health" $SmokeTimeoutSeconds '"service":"oyako"'
    $apiHealthSmoke = Wait-Smoke "ACA /api/health" "$apiBaseUrl/health" $SmokeTimeoutSeconds '"status":"ready"'
    $browserSmoke = Wait-Smoke "ACA /health/browser" "$baseUrl/health/browser" $SmokeTimeoutSeconds '"browser":"chromium"'
    $smokeResults = @($rootSmoke, $healthSmoke, $apiHealthSmoke, $browserSmoke)
    if ($smokeResults.Where({ -not $_.Ok }).Count -gt 0) {
        $details = ($smokeResults | ForEach-Object { "$($_.Name): HTTP $($_.StatusCode) $($_.Snippet)" }) -join [Environment]::NewLine
        Fail "ACA smoke tests failed.`n$details"
    }

    Step "Removing previous ACA cutover resources"
    Remove-PreviousAcaCutoverResources $legacyAcrName

    Step "Collecting final ACA resource list"
    $resources = @()
    foreach ($resource in @((Get-Acr $acrName), (Get-ContainerEnvironment), (Get-ContainerApp))) {
        if ($resource) {
            $resources += [pscustomobject]@{
                name = $resource.name
                type = $resource.type
                location = $resource.location
                id = $resource.id
            }
        }
    }

    Write-Host ""
    Write-Host "Oyako ACA deployment completed." -ForegroundColor Green
    Write-Host "URL: $baseUrl/"
    Write-Host "API base: $apiBaseUrl"
    Write-Host "API health: $baseUrl/health"
    Write-Host "API routed health: $apiBaseUrl/health"
    Write-Host "Browser health: $baseUrl/health/browser"
    Write-Host "Image: $remoteImage"
    Write-Host "Selected size: $Cpu vCPU / $Memory, min=1, max=1"
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
    Write-Host "Cost shape: ACR Basic + ACA Consumption, one always-on replica at 0.25 vCPU / 0.5Gi. No Storage Account, Log Analytics, Key Vault, or managed database was created."
    exit 0
}
catch {
    Write-Host ""
    Write-Host "Oyako ACA deployment failed." -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ""
    Write-Host "Retry command:"
    Write-Host "  deploy-aca.cmd"
    exit 1
}
