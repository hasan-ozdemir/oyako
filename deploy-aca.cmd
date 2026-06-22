@echo off
setlocal EnableExtensions EnableDelayedExpansion
chcp 65001 >nul

set "OYAKO_SCRIPT_SELF=%~f0"
set "OYAKO_SCRIPT_PS1=%TEMP%\oyako-deploy-aca-%RANDOM%-%RANDOM%.ps1"

powershell -NoProfile -ExecutionPolicy Bypass -Command "$ErrorActionPreference='Stop'; $src=Get-Content -Raw -Encoding UTF8 -LiteralPath $env:OYAKO_SCRIPT_SELF; $parts=$src -split '(?m)^:POWERSHELL_BEGIN\r?$',2; if($parts.Count -lt 2){ throw 'PowerShell payload marker was not found.' }; Set-Content -LiteralPath $env:OYAKO_SCRIPT_PS1 -Value $parts[1] -Encoding UTF8"
if errorlevel 1 exit /b %ERRORLEVEL%

powershell -NoProfile -ExecutionPolicy Bypass -File "%OYAKO_SCRIPT_PS1%" %*
set "OYAKO_SCRIPT_EXIT=%ERRORLEVEL%"

del "%OYAKO_SCRIPT_PS1%" >nul 2>nul
exit /b %OYAKO_SCRIPT_EXIT%

:POWERSHELL_BEGIN
param([Parameter(ValueFromRemainingArguments = $true)][string[]]$ScriptArgs)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
$OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
Set-Variable -Name PSNativeCommandUseErrorActionPreference -Value $false -Scope Script -ErrorAction SilentlyContinue

$Root = Split-Path -Parent $env:OYAKO_SCRIPT_SELF
$Subscription = "az2vs"
$Location = "italynorth"
$ResourceGroup = ""
$DefaultTenantName = "oyakdijital"
$AppName = ""
$EnvironmentName = ""
$ImageRepository = ""
$ImageTag = "latest"
$Scope = "oyako-aca"
$ManagedBy = "deploy-aca"
$BootstrapImage = "mcr.microsoft.com/k8se/quickstart:latest"
$TargetPort = "8080"
$Cpu = "0.25"
$Memory = "0.5Gi"
$SmokeTimeoutSeconds = 240
$Tags = @("app=oyako", "managed-by=$ManagedBy", "deployment-scope=$Scope")
$script:TargetTenantName = $DefaultTenantName

function Step([string]$Message) { Write-Host ""; Write-Host "==> $Message" -ForegroundColor Cyan }
function Ok([string]$Message) { Write-Host "OK: $Message" -ForegroundColor Green }
function Warn([string]$Message) { Write-Host "WARNING: $Message" -ForegroundColor Yellow }
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
    foreach ($rawLine in Get-Content -Encoding UTF8 -LiteralPath $Path) {
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

function Read-OptionalEnvFile([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        return @{}
    }

    return Read-EnvFile $Path
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

function Normalize-AiProvider([string]$Value, [string]$Name) {
    $normalized = $Value.Trim().ToLowerInvariant()
    switch ($normalized) {
        "azure" { return "azure" }
        "azure-cloud" { return "azure" }
        "ollama-cloud" { return "ollama-cloud" }
        "ollama-local" { return "ollama-local" }
        default { Fail "$Name '$Value' is not a supported AI provider." }
    }
}

function Assert-BoolValue([string]$Value, [string]$Name) {
    if ($Value -notin @("true", "false", "True", "False", "TRUE", "FALSE")) {
        Fail "$Name must be true or false."
    }
    return $Value.ToLowerInvariant()
}

function Assert-PositiveInt([string]$Value, [string]$Name) {
    $parsed = 0
    if (-not [int]::TryParse($Value, [ref]$parsed) -or $parsed -lt 1) {
        Fail "$Name must be a positive integer."
    }
    return [string]$parsed
}

function Expand-EnvReferences([hashtable]$Map) {
    $expanded = @{}
    foreach ($key in $Map.Keys) {
        $value = [string]$Map[$key]
        $expanded[$key] = [regex]::Replace($value, "%([A-Za-z0-9_]+)%", {
            param($match)
            $reference = $match.Groups[1].Value
            if ($Map.ContainsKey($reference)) { return [string]$Map[$reference] }
            return $match.Value
        })
    }
    return $expanded
}

function Resolve-TenantName {
    $tenantName = $DefaultTenantName
    for ($index = 0; $index -lt $ScriptArgs.Count; $index++) {
        $arg = [string]$ScriptArgs[$index]
        if ($arg -eq "--tenant-name" -or $arg -eq "-t") {
            if ($index + 1 -ge $ScriptArgs.Count) { Fail "$arg requires a tenant name." }
            $tenantName = [string]$ScriptArgs[$index + 1]
            $index++
            continue
        }
        if ($arg.StartsWith("--tenant-name=", [StringComparison]::OrdinalIgnoreCase)) {
            $tenantName = $arg.Substring("--tenant-name=".Length)
            continue
        }
        Fail "Unsupported argument '$arg'. Usage: deploy-aca.cmd [--tenant-name <name>|-t <name>]"
    }

    if ($tenantName -notmatch "^[a-z0-9][a-z0-9-]*[a-z0-9]$|^[a-z0-9]$") {
        Fail "Tenant name '$tenantName' must be lowercase letters, numbers, or hyphen."
    }
    return $tenantName
}

function Load-TenantEnv([string]$TenantName) {
    $tenantsRoot = Join-Path $Root ".tenants"
    if (-not (Test-Path -LiteralPath $tenantsRoot -PathType Container)) {
        Fail "Tenant directory was not found: $tenantsRoot"
    }

    $tenantFiles = @(Get-ChildItem -LiteralPath $tenantsRoot -Filter "*.env" -File | Sort-Object Name)
    if ($tenantFiles.Count -eq 0) {
        Fail "No tenant env files were discovered under $tenantsRoot."
    }

    $path = $null
    $tenantEnv = $null
    $discoveredTenants = New-Object System.Collections.Generic.List[string]
    foreach ($file in $tenantFiles) {
        $candidateEnv = Expand-EnvReferences (Read-EnvFile $file.FullName)
        Require-EnvKeys $candidateEnv @("tenant_name", "tenant_enabled") $file.FullName
        $fileTenantName = [IO.Path]::GetFileNameWithoutExtension($file.Name)
        if ([string]$candidateEnv["tenant_name"] -ne $fileTenantName) {
            Fail "Tenant file '$($file.FullName)' declares tenant_name='$($candidateEnv["tenant_name"])', expected '$fileTenantName'."
        }
        [void]$discoveredTenants.Add([string]$candidateEnv["tenant_name"])
        if ([string]$candidateEnv["tenant_name"] -eq $TenantName) {
            $path = $file.FullName
            $tenantEnv = $candidateEnv
        }
    }

    if (-not $path -or -not $tenantEnv) {
        Fail "Tenant '$TenantName' was not discovered under .tenants. Discovered tenants: $($discoveredTenants -join ', ')"
    }

    if ((Assert-BoolValue ([string]$tenantEnv["tenant_enabled"]) "tenant_enabled") -ne "true") {
        Fail "Tenant '$TenantName' is disabled. Set tenant_enabled=true in '$path' to deploy it."
    }

    $requiredKeys = @(
        "tenant_enabled",
        "tenant_id",
        "tenant_order_number",
        "tenant_name",
        "tenant_display_name",
        "tenant_azure_domain_name",
        "tenant_custom_domain_name",
        "tenant_web_url",
        "tenant_admin_email",
        "tenant_feedback_email",
        "tenant_knowledge_source_1_type",
        "tenant_knowledge_source_1_url",
        "tenant_knowledge_source_1_refresh_period",
        "primary_ai_provider",
        "secondary_ai_provider",
        "ai_provider_ollama_cloud_model",
        "ai_provider_azure_cloud_model",
        "ui_web_brand_name",
        "ui_web_assistant_name",
        "ui_web_title",
        "ui_web_header_title",
        "ui_web_brand_logo_url",
        "ui_web_assistant_welcome_message",
        "ui_web_assistant_header_title",
        "ui_web_more_menu_brand_link",
        "ui_web_more_menu_feedback_link",
        "ui_web_more_menu_help_link",
        "ui_web_settings_page_title",
        "ui_web_settings_header_title",
        "ui_web_knowledge_bank_header_title",
        "ui_web_knowledge_source_header_title",
        "ui_web_knowledge_source_header_message",
        "ui_web_knowledge_sources_table_title",
        "ui_web_knowledge_documents_table_title"
    )
    Require-EnvKeys $tenantEnv $requiredKeys $path
    if ([string]$tenantEnv["tenant_name"] -ne $TenantName) {
        Fail "Tenant file '$path' declares tenant_name='$($tenantEnv["tenant_name"])', expected '$TenantName'."
    }
    if ([string]$tenantEnv["tenant_id"] -notmatch "^[a-f0-9]{32}$") {
        Fail "tenant_id must be 32 lowercase hex characters."
    }
    [void](Assert-PositiveInt ([string]$tenantEnv["tenant_order_number"]) "tenant_order_number")
    Assert-DnsLabel ([string]$tenantEnv["tenant_azure_domain_name"]) "tenant_azure_domain_name" 32
    Validate-TenantKnowledgeSources $tenantEnv
    return [pscustomobject]@{ Path = $path; Values = $tenantEnv }
}

function Validate-TenantKnowledgeSources([hashtable]$TenantEnv) {
    $index = 1
    while ($TenantEnv.ContainsKey("tenant_knowledge_source_${index}_type")) {
        Require-EnvKeys $TenantEnv @(
            "tenant_knowledge_source_${index}_type",
            "tenant_knowledge_source_${index}_url",
            "tenant_knowledge_source_${index}_refresh_period"
        ) ".tenants source $index"
        if ([string]$TenantEnv["tenant_knowledge_source_${index}_type"] -ne "web_site") {
            Fail "tenant_knowledge_source_${index}_type must be web_site."
        }
        if ([string]$TenantEnv["tenant_knowledge_source_${index}_url"] -notmatch "^https?://") {
            Fail "tenant_knowledge_source_${index}_url must be an http/https URL."
        }
        if ([string]$TenantEnv["tenant_knowledge_source_${index}_refresh_period"] -notmatch "^(?:[1-9]|[1-5][0-9]|60)minutes?$|^(?:[1-9]|1[0-9]|2[0-4])hours?$|^[1-4]days?$|^[1-4]weeks?$") {
            Fail "tenant_knowledge_source_${index}_refresh_period is invalid."
        }
        $index++
    }
}

function Build-TenantKnowledgeSourceAppSettings([hashtable]$TenantEnv) {
    $settings = @()
    $index = 1
    while ($TenantEnv.ContainsKey("tenant_knowledge_source_${index}_type")) {
        $zeroIndex = $index - 1
        $settings += "Tenant__KnowledgeSources__${zeroIndex}__Key=source_$index"
        $settings += "Tenant__KnowledgeSources__${zeroIndex}__Type=$($TenantEnv["tenant_knowledge_source_${index}_type"])"
        $settings += "Tenant__KnowledgeSources__${zeroIndex}__Url=$($TenantEnv["tenant_knowledge_source_${index}_url"])"
        $settings += "Tenant__KnowledgeSources__${zeroIndex}__RefreshPeriod=$($TenantEnv["tenant_knowledge_source_${index}_refresh_period"])"
        $settings += "Tenant__KnowledgeSources__${zeroIndex}__Name=$(EnvValue $TenantEnv "tenant_knowledge_source_${index}_name" $TenantEnv["tenant_display_name"])"
        $settings += "Tenant__KnowledgeSources__${zeroIndex}__Description=$(EnvValue $TenantEnv "tenant_knowledge_source_${index}_description" "$($TenantEnv["tenant_display_name"]) seed web sitesi bilgi kaynağı.")"
        $settings += "Tenant__KnowledgeSources__${zeroIndex}__Enabled=$(EnvValue $TenantEnv "tenant_knowledge_source_${index}_enabled" "true")"
        $index++
    }
    return $settings
}

function Build-TenantListAppSettings([hashtable]$TenantEnv, [string]$EnvKey, [string]$ConfigKey) {
    $settings = @()
    if (-not $TenantEnv.ContainsKey($EnvKey)) {
        return $settings
    }

    $values = @([string]$TenantEnv[$EnvKey] -split "\|" | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    for ($index = 0; $index -lt $values.Count; $index++) {
        $settings += "$ConfigKey`__$index=$($values[$index])"
    }
    return $settings
}

function Assert-DnsLabel([string]$Value, [string]$Description, [int]$MaxLength) {
    if ([string]::IsNullOrWhiteSpace($Value)) {
        Fail "$Description cannot be empty."
    }
    if ($Value.Length -gt $MaxLength) {
        Fail "$Description '$Value' is too long. Maximum length is $MaxLength characters."
    }
    if ($Value -cne $Value.ToLowerInvariant() -or $Value -notmatch "^[a-z0-9](?:[a-z0-9-]*[a-z0-9])?$") {
        Fail "$Description '$Value' must be a lowercase DNS label: letters, numbers, hyphen, and no leading/trailing hyphen."
    }
}

function Try-ConfigureCustomDomain([string]$CustomDomain, [string]$DefaultFqdn) {
    if ([string]::IsNullOrWhiteSpace($CustomDomain)) {
        Warn "tenant_custom_domain_name is empty. Continuing with https://$DefaultFqdn/."
        return
    }

    $cname = Resolve-DnsName -Name $CustomDomain -Type CNAME -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if (-not $cname -or ([string]$cname.NameHost).TrimEnd(".") -ne $DefaultFqdn) {
        Warn "Custom domain '$CustomDomain' is not configured as a CNAME to '$DefaultFqdn'. Skipping optional custom domain binding."
        return
    }

    $addResult = TryAz @("containerapp", "hostname", "add", "--hostname", $CustomDomain, "--name", $AppName, "--resource-group", $ResourceGroup)
    if (-not $addResult.Ok) {
        Warn "Could not add Container App custom hostname '$CustomDomain'. $($addResult.Text)"
        return
    }

    $bindResult = TryAz @("containerapp", "hostname", "bind", "--hostname", $CustomDomain, "--name", $AppName, "--resource-group", $ResourceGroup, "--environment", $EnvironmentName, "--validation-method", "CNAME")
    if (-not $bindResult.Ok) {
        Warn "Could not bind managed certificate for Container App custom hostname '$CustomDomain'. $($bindResult.Text)"
        return
    }

    Ok "Custom domain '$CustomDomain' is configured."
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

function Wait-TenantConfigSmoke([string]$Name, [string]$Url, [string]$ExpectedTenantName, [string]$ExpectedDisplayName, [int]$TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $last = ""
    do {
        try {
            $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 20
            $snippet = (($response.Content -replace "\s+", " ").Trim())
            if ($snippet.Length -gt 220) { $snippet = $snippet.Substring(0, 220) }
            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 400) {
                $config = $response.Content | ConvertFrom-Json
                if ([string]$config.tenantName -eq $ExpectedTenantName -and [string]$config.tenantDisplayName -eq $ExpectedDisplayName) {
                    return [pscustomobject]@{ Name = $Name; Url = $Url; StatusCode = $response.StatusCode; Snippet = $snippet; Ok = $true }
                }
                $last = "HTTP $($response.StatusCode): tenant config mismatch. $snippet"
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

function Remove-LegacyOwnedAcrs([string]$TargetName, [hashtable]$TenantEnv, [string]$TenantName) {
    $result = TryAz @("resource", "list", "--resource-group", $ResourceGroup, "--resource-type", "Microsoft.ContainerRegistry/registries", "-o", "json")
    if (-not $result.Ok) {
        Fail "Could not list Container Registries in resource group '$ResourceGroup'. $($result.Text)"
    }

    $rawRegistries = FromJson $result.Text
    $registries = if ($null -eq $rawRegistries) { @() } elseif ($rawRegistries -is [Array]) { $rawRegistries } else { @($rawRegistries) }
    foreach ($registry in $registries) {
        $name = [string]$registry.name
        if ($name -eq $TargetName) { continue }
        $isSameTenant = (Get-TagValue $registry "tenant-name") -eq $TenantName `
            -and (Get-TagValue $registry "tenant-id") -eq [string]$TenantEnv["tenant_id"] `
            -and (Get-TagValue $registry "tenant-order-number") -eq [string]$TenantEnv["tenant_order_number"]
        if ((Is-Owned $registry) -and $isSameTenant) {
            Step "Removing legacy script-owned ACR $name"
            Az @("acr", "delete", "--name", $name, "--resource-group", $ResourceGroup, "--yes")
        }
    }
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

function Deactivate-InactiveContainerAppRevisions() {
    $app = Get-ContainerApp
    if (-not $app) { return }

    $latestRevision = [string]$app.properties.latestRevisionName
    $result = TryAz @("containerapp", "revision", "list", "--name", $AppName, "--resource-group", $ResourceGroup, "-o", "json")
    if (-not $result.Ok) {
        Fail "Could not list Container App revisions for $AppName. $($result.Text)"
    }

    $rawRevisions = FromJson $result.Text
    $revisions = if ($null -eq $rawRevisions) { @() } elseif ($rawRevisions -is [Array]) { $rawRevisions } else { @($rawRevisions) }
    foreach ($revision in $revisions) {
        $name = [string]$revision.name
        $activeValue = $revision.properties.active
        if ($activeValue -is [Array]) { $activeValue = @($activeValue | Select-Object -First 1)[0] }
        $active = [bool]$activeValue
        $trafficWeightValue = $revision.properties.trafficWeight
        if ($trafficWeightValue -is [Array]) { $trafficWeightValue = @($trafficWeightValue | Select-Object -First 1)[0] }
        $trafficWeight = 0
        if ($null -ne $trafficWeightValue) { [void][int]::TryParse([string]$trafficWeightValue, [ref]$trafficWeight) }
        if ($active -and $trafficWeight -eq 0 -and $name -ne $latestRevision) {
            Step "Deactivating inactive Container App revision $name"
            Az @("containerapp", "revision", "deactivate", "--name", $AppName, "--resource-group", $ResourceGroup, "--revision", $name)
        }
    }
}

function Wait-ContainerAppProvisioned([int]$TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        $app = Get-ContainerApp
        if ($app) {
            $state = [string]$app.properties.provisioningState
            if ($state -eq "Succeeded") {
                return
            }
            if ($state -eq "Failed") {
                Fail "Container App $AppName provisioning failed."
            }
        }
        Start-Sleep -Seconds 10
    } while ((Get-Date) -lt $deadline)

    Fail "Container App $AppName did not reach provisioningState=Succeeded within $TimeoutSeconds seconds."
}

function Get-ContainerEnvironment() {
    return Get-ContainerEnvironmentByName $EnvironmentName
}

function Remove-OwnedContainerAppByName([string]$Name, [string]$Description) {
    $app = Get-ContainerAppByName $Name
    Assert-OwnedOrMissing $app $Name "Container App"
    if ($app) {
        Step "Removing $Description Container App $Name"
        Az @("containerapp", "delete", "--name", $Name, "--resource-group", $ResourceGroup, "--yes")
        Wait-ResourceGone "Container App $Name" { [bool](Get-ContainerAppByName $Name) }
    }
}

function Create-ContainerAppBootstrap([string[]]$CreateArguments) {
    $deadline = (Get-Date).AddMinutes(10)
    do {
        $result = TryAz $CreateArguments
        if ($result.Ok) {
            return
        }

        if ($result.Text -match "pending delete|Conflict|already exists") {
            Write-Host "Container App name is still settling after delete; retrying create in 20 seconds."
            Start-Sleep -Seconds 20
            continue
        }

        Fail "Container App bootstrap create failed. $($result.Text)"
    } while ((Get-Date) -lt $deadline)

    Fail "Container App bootstrap create did not succeed within 10 minutes after delete."
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

function Get-EnvironmentDefaultDomain($Environment) {
    if (-not $Environment -or -not $Environment.properties) { return "" }
    return [string]$Environment.properties.defaultDomain
}

function Ensure-Environment() {
    $env = Get-ContainerEnvironment
    Assert-OwnedOrMissing $env $EnvironmentName "Container Apps Environment"
    if ($env) {
        $locationMatches = (Normalize-Location ([string]$env.location)) -eq $Location
        if ($locationMatches) {
            $domain = Get-EnvironmentDefaultDomain $env
            if ([string]::IsNullOrWhiteSpace($domain)) {
                Fail "Container Apps Environment '$EnvironmentName' has no default domain yet."
            }
            Ok "Using existing Container Apps Environment $EnvironmentName."
            Tag-Resource ([string]$env.id)
            return
        }

        Remove-OwnedContainerAppByName $AppName "non-compliant"
        Remove-OwnedEnvironmentByName $EnvironmentName "non-compliant"
    }

    Step "Creating Container Apps Environment $EnvironmentName"
    Az ((@(
        "containerapp", "env", "create",
        "--name", $EnvironmentName,
        "--resource-group", $ResourceGroup,
        "--location", $Location,
        "--enable-workload-profiles", "false",
        "--logs-destination", "none",
        "--tags"
    ) + $Tags))

    $envId = Wait-Text "Container Apps Environment id" {
        $created = Get-ContainerEnvironment
        if ($created) { [string]$created.id } else { "" }
    } 600
    Tag-Resource $envId

    $domain = Wait-Text "Container Apps Environment default domain" {
        $created = Get-ContainerEnvironment
        if ($created) { Get-EnvironmentDefaultDomain $created } else { "" }
    } 600
    Ok "Created Container Apps Environment $EnvironmentName with default domain $domain."
}

function Ensure-ContainerAppBootstrap() {
    $app = Get-ContainerApp
    Assert-OwnedOrMissing $app $AppName "Container App"
    if ($app) {
        $environmentMatches = ([string]$app.properties.environmentId).EndsWith("/$EnvironmentName", [StringComparison]::OrdinalIgnoreCase)
        $locationMatches = (Normalize-Location ([string]$app.location)) -eq $Location
        if ($environmentMatches -and $locationMatches) {
            $provisioningState = [string]$app.properties.provisioningState
            if ($provisioningState -eq "Failed") {
                Remove-OwnedContainerAppByName $AppName "failed"
                $app = $null
            }
            elseif ($provisioningState -ne "Succeeded") {
                Wait-ContainerAppProvisioned 600
            }
        }

        if ($app -and $environmentMatches -and $locationMatches) {
            Ok "Using existing Container App $AppName."
            return
        }

        if ($app) {
            Remove-OwnedContainerApp
        }
    }

    Step "Creating Container App bootstrap identity"
    Create-ContainerAppBootstrap ((@(
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
    Wait-ContainerAppProvisioned 600
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
    $tenantName = Resolve-TenantName
    $script:TargetTenantName = $tenantName
    $tenantInfo = Load-TenantEnv $tenantName
    $tenantEnv = $tenantInfo.Values
    Require-EnvKeys $azureEnv @("AzureAi__Endpoint", "AzureAi__DeploymentName", "AzureAi__Deployments__0", "AzureAi__ApiVersion", "AzureAi__ApiKey") "azure-cloud.env"
    Require-EnvKeys $ollamaEnv @("ollama_api_key") "ollama-cloud.env"
    $tenantSlug = [string]$tenantEnv["tenant_azure_domain_name"]
    $ResourceGroup = "rg-$($tenantEnv["tenant_id"])-$($tenantEnv["tenant_order_number"])"
    if ($ResourceGroup -eq "rg-oyako") {
        Fail "Application resources must not target rg-oyako. rg-oyako is reserved for Azure AI resources."
    }
    $AppName = $tenantSlug
    $EnvironmentName = "$tenantSlug-aca-env"
    $ImageRepository = "$tenantName-$($tenantEnv["tenant_order_number"])"
    if ($ImageRepository -notmatch "^[a-z0-9]+(?:[._-][a-z0-9]+)*$") {
        Fail "Docker image repository '$ImageRepository' is invalid. tenant_name and tenant_order_number must produce a lowercase repository name."
    }
    $Tags = @(
        "app=oyako",
        "managed-by=$ManagedBy",
        "deployment-scope=$Scope",
        "tenant-name=$tenantName",
        "tenant-id=$($tenantEnv["tenant_id"])",
        "tenant-order-number=$($tenantEnv["tenant_order_number"])"
    )
    Assert-DnsLabel $tenantSlug "tenant_azure_domain_name" 32
    Assert-DnsLabel $AppName "Container App name" 32
    Assert-DnsLabel $EnvironmentName "Container Apps Environment name" 32
    Ok "Required env files and keys are present."
    Ok "Tenant '$tenantName' loaded from $($tenantInfo.Path)."
    Ok "ACA naming: resource group '$ResourceGroup', app '$AppName', environment '$EnvironmentName'."
    Ok "ACA image naming: repository '$ImageRepository', tag '$ImageTag'."

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

    Step "Ensuring deterministic ACR"
    $acrName = "acr$($tenantEnv["tenant_order_number"])$($tenantEnv["tenant_id"])"
    if ($acrName -notmatch "^[a-z0-9]{5,50}$") {
        Fail "ACR name '$acrName' is invalid. Expected acr<tenant_order_number><tenant_id>, 5-50 lowercase alphanumeric characters."
    }
    Remove-LegacyOwnedAcrs $acrName $tenantEnv $tenantName
    $acr = Get-Acr $acrName
    Assert-OwnedOrMissing $acr $acrName "Azure Container Registry"
    if ($acr -and [string]$acr.resourceGroup -ne $ResourceGroup) {
        Fail "ACR '$acrName' already exists in resource group '$($acr.resourceGroup)'. Deterministic tenant ACR names must be owned in '$ResourceGroup'."
    }
    if ($acr -and ((Normalize-Location ([string]$acr.location)) -ne $Location)) {
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

    Step "Building and pushing $ImageRepository`:$ImageTag"
    $env:DOCKER_BUILDKIT = "1"
    $localImage = "$ImageRepository`:$ImageTag"
    $remoteImage = "$acrLoginServer/$ImageRepository`:$ImageTag"
    & docker image rm -f $localImage 2>$null | Out-Null
    $existingRepositories = @(AzText @("acr", "repository", "list", "--name", $acrName, "-o", "json") -Quiet | ConvertFrom-Json)
    foreach ($repository in $existingRepositories) {
        if (-not [string]::IsNullOrWhiteSpace([string]$repository)) {
            Az @("acr", "repository", "delete", "--name", $acrName, "--repository", [string]$repository, "--yes")
        }
    }
    Run "docker" @("build", "--quiet", "--force-rm", "-t", $localImage, ".") | Out-Null
    Run -Exe "docker" -Arguments @("tag", $localImage, $remoteImage) -Quiet | Out-Null
    Az @("acr", "login", "--name", $acrName)
    Run "docker" @("push", "--quiet", $remoteImage) | Out-Null
    $repositories = @(AzText @("acr", "repository", "list", "--name", $acrName, "-o", "json") -Quiet | ConvertFrom-Json)
    if ($repositories.Count -ne 1 -or [string]$repositories[0] -ne $ImageRepository) {
        Fail "ACR '$acrName' must contain only repository '$ImageRepository', found: $($repositories -join ', ')"
    }
    $imageTags = @(AzText @("acr", "repository", "show-tags", "--name", $acrName, "--repository", $ImageRepository, "-o", "json") -Quiet | ConvertFrom-Json)
    if ($imageTags.Count -ne 1 -or $imageTags[0] -ne $ImageTag) {
        Fail "ACR repository '$ImageRepository' must contain only '$ImageTag', found: $($imageTags -join ', ')"
    }

    Step "Ensuring Container Apps resources"
    Ensure-Environment
    $environmentDefaultDomain = Wait-Text "Container Apps Environment default domain" {
        $env = Get-ContainerEnvironment
        if ($env) { Get-EnvironmentDefaultDomain $env } else { "" }
    } 120
    $expectedFqdn = "$AppName.$environmentDefaultDomain"
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
    $aiProvider = Normalize-AiProvider (EnvValue $tenantEnv "primary_ai_provider" "ollama-cloud") "primary_ai_provider"
    $aiFallbackProvider = Normalize-AiProvider (EnvValue $tenantEnv "secondary_ai_provider" "azure-cloud") "secondary_ai_provider"
    $crawlerDomainOnly = Assert-BoolValue (EnvValue $tenantEnv "domain_only_crawling" "true") "domain_only_crawling"
    $crawlerMaxPages = Assert-PositiveInt (EnvValue $tenantEnv "web_document_max_count" "1000") "web_document_max_count"
    $crawlerMaxDepth = Assert-PositiveInt (EnvValue $tenantEnv "web_document_max_depth" "10") "web_document_max_depth"
    $tenantAppSettings = @(
        "OYAKO_TENANT_NAME=$tenantName",
        "Tenant__Enabled=true",
        "Tenant__Id=$($tenantEnv["tenant_id"])",
        "Tenant__OrderNumber=$($tenantEnv["tenant_order_number"])",
        "Tenant__Name=$($tenantEnv["tenant_name"])",
        "Tenant__DisplayName=$($tenantEnv["tenant_display_name"])",
        "Tenant__AzureDomainName=$($tenantEnv["tenant_azure_domain_name"])",
        "Tenant__CustomDomainName=$($tenantEnv["tenant_custom_domain_name"])",
        "Tenant__WebUrl=$($tenantEnv["tenant_web_url"])",
        "Tenant__AdminEmail=$($tenantEnv["tenant_admin_email"])",
        "Tenant__FeedbackEmail=$($tenantEnv["tenant_feedback_email"])",
        "Tenant__UiWebBrandName=$($tenantEnv["ui_web_brand_name"])",
        "Tenant__UiWebAssistantName=$($tenantEnv["ui_web_assistant_name"])",
        "Tenant__UiWebTitle=$($tenantEnv["ui_web_title"])",
        "Tenant__UiWebHeaderTitle=$($tenantEnv["ui_web_header_title"])",
        "Tenant__UiWebBrandLogoUrl=$($tenantEnv["ui_web_brand_logo_url"])",
        "Tenant__UiWebAssistantWelcomeMessage=$($tenantEnv["ui_web_assistant_welcome_message"])",
        "Tenant__UiWebAssistantHeaderTitle=$($tenantEnv["ui_web_assistant_header_title"])",
        "Tenant__UiWebMoreMenuBrandLink=$($tenantEnv["ui_web_more_menu_brand_link"])",
        "Tenant__UiWebMoreMenuFeedbackLink=$($tenantEnv["ui_web_more_menu_feedback_link"])",
        "Tenant__UiWebMoreMenuHelpLink=$($tenantEnv["ui_web_more_menu_help_link"])",
        "Tenant__UiWebSettingsPageTitle=$($tenantEnv["ui_web_settings_page_title"])",
        "Tenant__UiWebSettingsHeaderTitle=$($tenantEnv["ui_web_settings_header_title"])",
        "Tenant__UiWebKnowledgeBankHeaderTitle=$($tenantEnv["ui_web_knowledge_bank_header_title"])",
        "Tenant__UiWebKnowledgeSourceHeaderTitle=$($tenantEnv["ui_web_knowledge_source_header_title"])",
        "Tenant__UiWebKnowledgeSourceHeaderMessage=$($tenantEnv["ui_web_knowledge_source_header_message"])",
        "Tenant__UiWebKnowledgeSourcesTableTitle=$($tenantEnv["ui_web_knowledge_sources_table_title"])",
        "Tenant__UiWebKnowledgeDocumentsTableTitle=$($tenantEnv["ui_web_knowledge_documents_table_title"])"
    )
    $tenantAppSettings += Build-TenantKnowledgeSourceAppSettings $tenantEnv
    $tenantAppSettings += Build-TenantListAppSettings $tenantEnv "tenant_text_cleaner_leading_boilerplate_terms" "Tenant__TextCleanerLeadingBoilerplateTerms"
    $tenantAppSettings += Build-TenantListAppSettings $tenantEnv "tenant_text_cleaner_exact_boilerplate_lines" "Tenant__TextCleanerExactBoilerplateLines"
    $tenantAppSettings += Build-TenantListAppSettings $tenantEnv "tenant_text_cleaner_footer_line_prefixes" "Tenant__TextCleanerFooterLinePrefixes"
    $envVars = @(
        "ASPNETCORE_ENVIRONMENT=Production",
        "ASPNETCORE_URLS=http://+:8080",
        "OYAKO_DOCKER=1",
        "Storage__DataRoot=/app/data",
        "Sqlite__ConnectionString=Data Source=/app/data/$tenantName/oyako.sqlite;Cache=Shared",
        "Ai__DefaultProvider=$aiProvider",
        "Ai__FallbackProviders__0=$aiFallbackProvider",
        "Ai__DisabledProviders__0=ollama-local",
        "Crawler__DomainOnlyCrawling=$crawlerDomainOnly",
        "Crawler__IncludeSubdomains=true",
        "Crawler__MaxPagesToCrawl=$crawlerMaxPages",
        "Crawler__MaxDepth=$crawlerMaxDepth",
        "AzureAi__Endpoint=$($azureEnv["AzureAi__Endpoint"])",
        "AzureAi__DeploymentName=$($tenantEnv["ai_provider_azure_cloud_model"])",
        "AzureAi__Deployments__0=$($tenantEnv["ai_provider_azure_cloud_model"])",
        "AzureAi__ApiVersion=$($azureEnv["AzureAi__ApiVersion"])",
        "AzureAi__TimeoutSeconds=$(EnvValue $azureEnv "AzureAi__TimeoutSeconds" "180")",
        "AzureAi__Temperature=$(EnvValue $azureEnv "AzureAi__Temperature" "0.2")",
        "AzureAi__ApiKey=secretref:azure-ai-api-key",
        "OllamaCloud__BaseUrl=$(EnvValue $ollamaEnv "OllamaCloud__BaseUrl" "https://ollama.com")",
        "OllamaCloud__Model=$($tenantEnv["ai_provider_ollama_cloud_model"])",
        "OllamaCloud__Models__0=$($tenantEnv["ai_provider_ollama_cloud_model"])",
        "OllamaCloud__TimeoutSeconds=$(EnvValue $ollamaEnv "OllamaCloud__TimeoutSeconds" "180")",
        "OllamaCloud__Temperature=$(EnvValue $ollamaEnv "OllamaCloud__Temperature" "0.2")",
        "OllamaCloud__ApiKey=secretref:ollama-api-key",
        "ollama_api_key=secretref:ollama-api-key",
        "DEPLOYMENT_TIMESTAMP=$([DateTimeOffset]::UtcNow.ToUnixTimeSeconds())"
    ) + $tenantAppSettings
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
    Wait-ContainerAppProvisioned 600
    Az @("containerapp", "ingress", "enable", "--name", $AppName, "--resource-group", $ResourceGroup, "--type", "external", "--target-port", $TargetPort, "--transport", "auto")
    Wait-ContainerAppProvisioned 300

    $fqdn = Wait-Text "Container App FQDN" {
        AzText @("containerapp", "show", "--name", $AppName, "--resource-group", $ResourceGroup, "--query", "properties.configuration.ingress.fqdn", "-o", "tsv") -Quiet
    } 120
    if ($fqdn -ne $expectedFqdn) {
        Fail "Azure returned Container App FQDN '$fqdn'; expected '$expectedFqdn'."
    }
    $baseUrl = "https://$fqdn"
    $apiBaseUrl = "$baseUrl/api"
    Try-ConfigureCustomDomain ([string]$tenantEnv["tenant_custom_domain_name"]) $fqdn

    Step "Running smoke tests"
    Deactivate-InactiveContainerAppRevisions

    $rootSmoke = Wait-Smoke "ACA frontend root" "$baseUrl/" $SmokeTimeoutSeconds "manifest.webmanifest"
    $tenantConfigSmoke = Wait-TenantConfigSmoke "ACA /api/tenant-config" "$apiBaseUrl/tenant-config" $tenantName ([string]$tenantEnv["tenant_display_name"]) $SmokeTimeoutSeconds
    $healthSmoke = Wait-Smoke "ACA /health" "$baseUrl/health" $SmokeTimeoutSeconds '"service":"oyako"'
    $apiHealthSmoke = Wait-Smoke "ACA /api/health" "$apiBaseUrl/health" $SmokeTimeoutSeconds '"status":"ready"'
    $browserSmoke = Wait-Smoke "ACA /health/browser" "$baseUrl/health/browser" $SmokeTimeoutSeconds '"browser":"chromium"'
    $smokeResults = @($rootSmoke, $tenantConfigSmoke, $healthSmoke, $apiHealthSmoke, $browserSmoke)
    if ($smokeResults.Where({ -not $_.Ok }).Count -gt 0) {
        $details = ($smokeResults | ForEach-Object { "$($_.Name): HTTP $($_.StatusCode) $($_.Snippet)" }) -join [Environment]::NewLine
        Fail "ACA smoke tests failed.`n$details"
    }

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
    Write-Host "Tenant: $tenantName ($($tenantEnv["tenant_display_name"]))"
    Write-Host "Resource group: $ResourceGroup"
    Write-Host "URL: $baseUrl/"
    Write-Host "API base: $apiBaseUrl"
    if (-not [string]::IsNullOrWhiteSpace([string]$tenantEnv["tenant_custom_domain_name"])) {
        Write-Host "Optional custom URL: https://$($tenantEnv["tenant_custom_domain_name"])/"
    }
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
    Write-Host "  deploy-aca.cmd --tenant-name $script:TargetTenantName"
    exit 1
}
