@echo off
setlocal EnableExtensions EnableDelayedExpansion
chcp 65001 >nul

set "OYAKO_SCRIPT_SELF=%~f0"
set "OYAKO_SCRIPT_PS1=%TEMP%\oyako-deploy-awa-%RANDOM%-%RANDOM%.ps1"

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
$PlanName = ""
$WebAppName = ""
$Scope = "oyako-awa"
$ManagedBy = "deploy-awa"
$Sku = "B1"
$Runtime = "DOTNETCORE:10.0"
$LinuxFxVersion = "DOTNETCORE|10.0"
$ScmSettleSeconds = 90
$SmokeTimeoutSeconds = 360
$Tags = @("app=oyako", "managed-by=$ManagedBy", "deployment-scope=$Scope")
$GlobalEnv = @{}
$script:TargetTenantName = $DefaultTenantName
$script:PackageOnly = $false

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

function Apply-GlobalConfig([hashtable]$Config) {
    $script:Subscription = EnvValue $Config "azure_subscription" $script:Subscription
    $script:Location = EnvValue $Config "azure_location" $script:Location
    $script:Sku = EnvValue $Config "awa_sku" $script:Sku
    $script:Runtime = EnvValue $Config "awa_runtime" $script:Runtime
    $script:LinuxFxVersion = EnvValue $Config "awa_linux_fx_version" $script:LinuxFxVersion
    $script:ScmSettleSeconds = [int](Assert-PositiveInt (EnvValue $Config "awa_scm_settle_seconds" ([string]$script:ScmSettleSeconds)) "awa_scm_settle_seconds")

    if ($script:Subscription -notmatch "^[A-Za-z0-9._ -]+$") { Fail "azure_subscription in oyako.env contains unsupported characters." }
    if ($script:Location -notmatch "^[a-z0-9]+$") { Fail "azure_location in oyako.env must be an Azure location name such as italynorth." }
    if ($script:Sku -notmatch "^[A-Za-z0-9_]+$") { Fail "awa_sku in oyako.env contains unsupported characters." }
    if ($script:Runtime -notmatch "^[A-Za-z0-9_.:-]+$") { Fail "awa_runtime in oyako.env contains unsupported characters." }
    if ($script:LinuxFxVersion -notmatch "^[A-Za-z0-9_.:-]+\|[A-Za-z0-9_.:-]+$") { Fail "awa_linux_fx_version in oyako.env must look like DOTNETCORE|10.0." }
}

function Resolve-DefaultTenantName {
    if (-not [string]::IsNullOrWhiteSpace($env:OYAKO_TENANT_NAME)) { return $env:OYAKO_TENANT_NAME.Trim() }
    if (-not [string]::IsNullOrWhiteSpace($env:tenant_name)) { return $env:tenant_name.Trim() }

    $defaultTenantId = EnvValue $GlobalEnv "default_tenant_id" ""
    if (-not [string]::IsNullOrWhiteSpace($defaultTenantId)) {
        return Resolve-TenantNameById $defaultTenantId
    }

    return EnvValue $GlobalEnv "default_tenant_name" $DefaultTenantName
}

function Resolve-TenantNameById([string]$TenantId) {
    if ($TenantId -notmatch "^[a-f0-9]{32}$") {
        Fail "default_tenant_id in oyako.env must be 32 lowercase hex characters."
    }

    $tenantsRoot = Join-Path $Root ".tenants"
    if (-not (Test-Path -LiteralPath $tenantsRoot -PathType Container)) {
        Fail "default_tenant_id is configured, but tenant directory was not found: $tenantsRoot"
    }

    $tenantFiles = @(Get-ChildItem -LiteralPath $tenantsRoot -Filter "*.env" -File | Sort-Object Name)
    if ($tenantFiles.Count -eq 0) {
        Fail "default_tenant_id is configured, but no tenant env files were discovered under $tenantsRoot."
    }

    $discoveredTenants = New-Object System.Collections.Generic.List[string]
    foreach ($file in $tenantFiles) {
        $candidateEnv = Expand-EnvReferences (Read-EnvFile $file.FullName)
        Require-EnvKeys $candidateEnv @("tenant_name", "tenant_enabled") $file.FullName
        $fileTenantName = [IO.Path]::GetFileNameWithoutExtension($file.Name)
        if ([string]$candidateEnv["tenant_name"] -ne $fileTenantName) {
            Fail "Tenant file '$($file.FullName)' declares tenant_name='$($candidateEnv["tenant_name"])', expected '$fileTenantName'."
        }

        [void]$discoveredTenants.Add([string]$candidateEnv["tenant_name"])
        if ((EnvValue $candidateEnv "tenant_id" "") -ne $TenantId) { continue }
        if ((Assert-BoolValue ([string]$candidateEnv["tenant_enabled"]) "tenant_enabled") -ne "true") {
            Fail "Tenant '$($candidateEnv["tenant_name"])' matches default_tenant_id but is disabled. Set tenant_enabled=true in '$($file.FullName)' to deploy it."
        }

        return [string]$candidateEnv["tenant_name"]
    }

    Fail "default_tenant_id '$TenantId' did not match an enabled tenant. Discovered tenants: $($discoveredTenants -join ', ')"
}

function Resolve-TenantName {
    $tenantName = ""
    for ($index = 0; $index -lt $ScriptArgs.Count; $index++) {
        $arg = [string]$ScriptArgs[$index]
        if ($arg -eq "--help" -or $arg -eq "-h") {
            Write-Host "Usage: deploy-awa.cmd [--tenant-name <name>|-t <name>] [--package-only]"
            exit 0
        }
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
        if ($arg -eq "--package-only") {
            $script:PackageOnly = $true
            continue
        }
        Fail "Unsupported argument '$arg'. Usage: deploy-awa.cmd [--tenant-name <name>|-t <name>] [--package-only]"
    }

    if ([string]::IsNullOrWhiteSpace($tenantName)) {
        $tenantName = Resolve-DefaultTenantName
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
    Assert-DnsLabel ([string]$tenantEnv["tenant_azure_domain_name"]) "tenant_azure_domain_name" 60
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

function Try-ConfigureCustomDomain([string]$CustomDomain, [string]$DefaultHostName) {
    if ([string]::IsNullOrWhiteSpace($CustomDomain)) {
        Warn "tenant_custom_domain_name is empty. Continuing with https://$DefaultHostName/."
        return
    }

    $cname = Resolve-DnsName -Name $CustomDomain -Type CNAME -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if (-not $cname -or ([string]$cname.NameHost).TrimEnd(".") -ne $DefaultHostName) {
        Warn "Custom domain '$CustomDomain' is not configured as a CNAME to '$DefaultHostName'. Skipping optional custom domain binding."
        return
    }

    $hostnameResult = TryAz @("webapp", "config", "hostname", "add", "--webapp-name", $WebAppName, "--resource-group", $ResourceGroup, "--hostname", $CustomDomain)
    if (-not $hostnameResult.Ok) {
        Warn "Could not add custom hostname '$CustomDomain'. $($hostnameResult.Text)"
        return
    }

    $certResult = TryAz @("webapp", "config", "ssl", "create", "--resource-group", $ResourceGroup, "--name", $WebAppName, "--hostname", $CustomDomain, "-o", "json")
    if (-not $certResult.Ok) {
        Warn "Could not create managed certificate for '$CustomDomain'. $($certResult.Text)"
        return
    }

    $cert = FromJson $certResult.Text
    $thumbprint = [string]$cert.thumbprint
    if ([string]::IsNullOrWhiteSpace($thumbprint)) {
        Warn "Managed certificate for '$CustomDomain' did not return a thumbprint yet. Continuing with default host."
        return
    }

    $bindResult = TryAz @("webapp", "config", "ssl", "bind", "--name", $WebAppName, "--resource-group", $ResourceGroup, "--hostname", $CustomDomain, "--certificate-thumbprint", $thumbprint, "--ssl-type", "SNI")
    if (-not $bindResult.Ok) {
        Warn "Could not bind managed certificate for '$CustomDomain'. $($bindResult.Text)"
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

function Wait-ScmStable([string]$WebAppHostName, [int]$InitialSettleSeconds) {
    if ($InitialSettleSeconds -gt 0) {
        Write-Host "Waiting $InitialSettleSeconds seconds for App Service SCM to settle after configuration changes..." -ForegroundColor DarkGray
        Start-Sleep -Seconds $InitialSettleSeconds
    }

    $url = "https://$WebAppHostName.scm.azurewebsites.net/api/deployments"
    $deadline = (Get-Date).AddMinutes(4)
    $consecutiveOk = 0
    $last = ""
    do {
        try {
            $response = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 15
            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 400) {
                $consecutiveOk++
                if ($consecutiveOk -ge 3) {
                    Ok "SCM endpoint is stable for ZIP deployment."
                    return
                }
            }
            else {
                $consecutiveOk = 0
                $last = "HTTP $($response.StatusCode)"
            }
        }
        catch {
            $consecutiveOk = 0
            $last = $_.Exception.Message
        }

        Start-Sleep -Seconds 5
    } while ((Get-Date) -lt $deadline)

    Fail "SCM endpoint did not become stable before ZIP deployment. Last status: $last"
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

function Set-RemoteAiProvider([string]$BaseUrl, [string]$Provider) {
    $settingsUrl = "$BaseUrl/api/ai-settings"
    try {
        $settings = Invoke-RestMethod -Method Get -Uri $settingsUrl -TimeoutSec 60
    }
    catch {
        Fail "Could not read remote AI settings from $settingsUrl. $($_.Exception.Message)"
    }

    $option = @($settings.providers | Where-Object { $_.id -eq $Provider } | Select-Object -First 1)[0]
    if (-not $option) {
        Fail "Remote AI provider '$Provider' is not exposed by $settingsUrl."
    }
    if ([string]::IsNullOrWhiteSpace([string]$option.selectedModel)) {
        Fail "Remote AI provider '$Provider' has no selected model."
    }

    $body = @{
        provider = $Provider
        model = [string]$option.selectedModel
    } | ConvertTo-Json -Compress

    try {
        Invoke-RestMethod -Method Put -Uri $settingsUrl -ContentType "application/json; charset=utf-8" -Body $body -TimeoutSec 60 | Out-Null
    }
    catch {
        Fail "Could not set remote AI provider '$Provider'. $($_.Exception.Message)"
    }

    Ok "Remote AI provider set to '$Provider' with model '$($option.selectedModel)'."
}

function Get-WebApp([string]$Name) {
    $result = TryAz @("resource", "list", "--name", $Name, "--resource-type", "Microsoft.Web/sites", "-o", "json")
    if (-not $result.Ok -or [string]::IsNullOrWhiteSpace($result.Text)) { return $null }
    return @(FromJson $result.Text | Select-Object -First 1)[0]
}

function Get-ArmAccessToken() {
    $token = AzText @("account", "get-access-token", "--resource", "https://management.azure.com/", "--query", "accessToken", "-o", "tsv") -Quiet
    if ([string]::IsNullOrWhiteSpace($token)) {
        Fail "Azure ARM access token could not be resolved for Web App name availability check."
    }
    return $token
}

function Assert-WebAppNameAvailableOrOwned([string]$Name, $ExistingSite) {
    if ($ExistingSite) {
        Assert-OwnedOrMissing $ExistingSite $Name "Web App"
        return
    }

    $token = Get-ArmAccessToken
    $uri = "https://management.azure.com/subscriptions/$subscriptionId/providers/Microsoft.Web/checknameavailability?api-version=2026-03-15"
    $body = @{ name = $Name; type = "Site" } | ConvertTo-Json -Compress
    try {
        $result = Invoke-RestMethod -Method Post -Uri $uri -Headers @{ Authorization = "Bearer $token" } -ContentType "application/json" -Body $body -TimeoutSec 60
    }
    catch {
        Fail "Web App name availability check failed for '$Name': $($_.Exception.Message)"
    }

    if (-not [bool]$result.nameAvailable) {
        $reason = [string]$result.reason
        $message = [string]$result.message
        Fail "Web App name '$Name' is not available. Reason='$reason'. Message='$message'."
    }

    Ok "Web App name '$Name' is globally available."
}

function Get-Plan() {
    $result = TryAz @("appservice", "plan", "show", "--name", $PlanName, "--resource-group", $ResourceGroup, "-o", "json")
    if (-not $result.Ok) { return $null }
    return FromJson $result.Text
}

function Remove-OwnedWebApp([string]$Name, [string]$Description = "non-compliant") {
    $site = Get-WebApp $Name
    Assert-OwnedOrMissing $site $Name "Web App"
    if ($site) {
        Step "Removing $Description Web App $Name"
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

    Step "Checking local prerequisites and strict tenant env files"
    Resolve-Exe "dotnet" | Out-Null
    Resolve-Exe "node" | Out-Null
    Resolve-Exe "npm" | Out-Null

    $GlobalEnv = Expand-EnvReferences (Read-OptionalEnvFile (Join-Path $Root "oyako.env"))
    Apply-GlobalConfig $GlobalEnv

    $tenantName = Resolve-TenantName
    $script:TargetTenantName = $tenantName
    $tenantInfo = Load-TenantEnv $tenantName
    $tenantEnv = $tenantInfo.Values
    $tenantSlug = [string]$tenantEnv["tenant_azure_domain_name"]
    $ResourceGroup = "rg-$($tenantEnv["tenant_id"])-$($tenantEnv["tenant_order_number"])"
    if ($ResourceGroup -eq "rg-oyako") {
        Fail "Application resources must not target rg-oyako. rg-oyako is reserved for Azure AI resources."
    }
    $WebAppName = $tenantSlug
    $PlanName = "$tenantSlug-awa-plan"
    $Tags = @(
        "app=oyako",
        "managed-by=$ManagedBy",
        "deployment-scope=$Scope",
        "tenant-name=$tenantName",
        "tenant-id=$($tenantEnv["tenant_id"])",
        "tenant-order-number=$($tenantEnv["tenant_order_number"])"
    )
    Assert-DnsLabel $tenantSlug "tenant_azure_domain_name" 60
    Assert-DnsLabel $WebAppName "Web App name" 60
    Assert-DnsLabel $PlanName "App Service Plan name" 40
    Ok "Required env files and keys are present."
    Ok "Tenant '$tenantName' loaded from $($tenantInfo.Path)."
    Ok "AWA naming: resource group '$ResourceGroup', web app '$WebAppName', plan '$PlanName'."

    $buildRoot = Join-Path $Root ".oyako-deploy\awa\$tenantName"
    $publishDir = Join-Path $buildRoot "publish"
    $zipPath = Join-Path $buildRoot "$tenantName-awa.zip"
    Remove-PathInsideRoot $buildRoot
    New-Item -ItemType Directory -Force -Path $publishDir | Out-Null
    Build-PublishPackage $publishDir $zipPath

    if ($script:PackageOnly) {
        Write-Host ""
        Write-Host "Oyako App Service package preflight completed." -ForegroundColor Green
        Write-Host "Tenant: $tenantName ($($tenantEnv["tenant_display_name"]))"
        Write-Host "Package: $zipPath"
        exit 0
    }

    Resolve-Exe "az" | Out-Null
    $azureEnv = Read-EnvFile (Join-Path $Root "azure-cloud.env")
    $ollamaEnv = Read-EnvFile (Join-Path $Root "ollama-cloud.env")
    Require-EnvKeys $azureEnv @("AzureAi__Endpoint", "AzureAi__DeploymentName", "AzureAi__Deployments__0", "AzureAi__ApiVersion", "AzureAi__ApiKey") "azure-cloud.env"
    Require-EnvKeys $ollamaEnv @("ollama_api_key") "ollama-cloud.env"

    Step "Selecting Azure subscription"
    $account = TryAz @("account", "show", "-o", "json")
    if (-not $account.Ok) {
        Fail "Azure CLI is not logged in. Run 'az login' manually, then retry deploy-awa.cmd."
    }
    Az @("account", "set", "--subscription", $Subscription)
    $subscriptionId = AzText @("account", "show", "--query", "id", "-o", "tsv") -Quiet
    if ([string]::IsNullOrWhiteSpace($subscriptionId)) { Fail "Azure subscription id could not be resolved after selecting '$Subscription'." }
    Ok "Using subscription $Subscription ($subscriptionId)."

    Step "Validating Azure capabilities"
    Ensure-Provider "Microsoft.Web"
    $locationName = AzText @("account", "list-locations", "--query", "[?name=='$Location'].name | [0]", "-o", "tsv") -Quiet
    if ($locationName -ne $Location) { Fail "Azure location '$Location' is not available for this subscription." }
    $appServiceLocations = AzText @("appservice", "list-locations", "--sku", $Sku, "--linux-workers-enabled", "-o", "tsv") -Quiet
    if (@($appServiceLocations -split "\r?\n" | Where-Object { $_ } | ForEach-Object { Normalize-Location ([string]$_) }) -notcontains $Location) {
        Fail "Linux App Service $Sku is not available in '$Location'."
    }
    $deployHelp = Run "az" @("webapp", "deployment", "source", "config-zip", "--help") -Quiet
    if ($deployHelp -notmatch "--src" -or $deployHelp -notmatch "--timeout") {
        Fail "Azure CLI webapp deployment source config-zip lacks required --src/--timeout arguments."
    }

    Step "Ensuring resource group"
    $rg = TryAz @("group", "show", "--name", $ResourceGroup, "--query", "id", "-o", "tsv")
    if (-not $rg.Ok -or [string]::IsNullOrWhiteSpace($rg.Text)) {
        Az @("group", "create", "--name", $ResourceGroup, "--location", $Location)
    }

    Step "Checking Web App name and managed resources"
    $site = Get-WebApp $WebAppName
    Assert-OwnedOrMissing $site $WebAppName "Web App"
    Assert-WebAppNameAvailableOrOwned $WebAppName $site
    $plan = Get-Plan
    Assert-OwnedOrMissing $plan $PlanName "App Service Plan"

    Step "Ensuring App Service resources"
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
    $appSettings = @(
        "ASPNETCORE_ENVIRONMENT=Production",
        "SCM_DO_BUILD_DURING_DEPLOYMENT=false",
        "WEBSITE_RUN_FROM_PACKAGE=0",
        "WEBSITES_CONTAINER_START_TIME_LIMIT=900",
        "Storage__DataRoot=/home/oyako-data",
        "Sqlite__ConnectionString=Data Source=/home/oyako-data/$tenantName/oyako.sqlite;Cache=Shared",
        "PLAYWRIGHT_BROWSERS_PATH=/home/oyako-playwright/ms-playwright",
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
        "AzureAi__ApiKey=$($azureEnv["AzureAi__ApiKey"])",
        "OllamaCloud__BaseUrl=$(EnvValue $ollamaEnv "OllamaCloud__BaseUrl" "https://ollama.com")",
        "OllamaCloud__Model=$($tenantEnv["ai_provider_ollama_cloud_model"])",
        "OllamaCloud__Models__0=$($tenantEnv["ai_provider_ollama_cloud_model"])",
        "OllamaCloud__TimeoutSeconds=$(EnvValue $ollamaEnv "OllamaCloud__TimeoutSeconds" "180")",
        "OllamaCloud__Temperature=$(EnvValue $ollamaEnv "OllamaCloud__Temperature" "0.2")",
        "OllamaCloud__ApiKey=$($ollamaEnv["ollama_api_key"])",
        "ollama_api_key=$($ollamaEnv["ollama_api_key"])"
    ) + $tenantAppSettings
    Az ((@("webapp", "config", "appsettings", "set", "--name", $WebAppName, "--resource-group", $ResourceGroup, "--settings") + $appSettings)) -Sensitive

    Step "Waiting for App Service SCM stability"
    Wait-ScmStable $WebAppName $ScmSettleSeconds

    Step "Deploying ZIP package"
    Az @("webapp", "deployment", "source", "config-zip", "--name", $WebAppName, "--resource-group", $ResourceGroup, "--src", $ZipPath, "--timeout", "600")
    Az @("webapp", "restart", "--name", $WebAppName, "--resource-group", $ResourceGroup)

    $baseUrl = "https://$WebAppName.azurewebsites.net"
    $apiBaseUrl = "$baseUrl/api"
    Try-ConfigureCustomDomain ([string]$tenantEnv["tenant_custom_domain_name"]) "$WebAppName.azurewebsites.net"
    Step "Waiting for Web App health before runtime configuration"
    $warmupSmoke = Wait-Smoke "AWA /health warmup" "$baseUrl/health" $SmokeTimeoutSeconds '"service":"oyako"'
    if (-not $warmupSmoke.Ok) {
        Fail "AWA warmup failed. HTTP $($warmupSmoke.StatusCode) $($warmupSmoke.Snippet)"
    }

    Step "Applying configured remote AI provider"
    Set-RemoteAiProvider $baseUrl $aiProvider

    Step "Running Web App smoke tests"
    $rootSmoke = Wait-Smoke "AWA frontend root" "$baseUrl/" $SmokeTimeoutSeconds "manifest.webmanifest"
    $tenantConfigSmoke = Wait-TenantConfigSmoke "AWA /api/tenant-config" "$apiBaseUrl/tenant-config" $tenantName ([string]$tenantEnv["tenant_display_name"]) $SmokeTimeoutSeconds
    $healthSmoke = Wait-Smoke "AWA /health" "$baseUrl/health" $SmokeTimeoutSeconds '"service":"oyako"'
    $apiHealthSmoke = Wait-Smoke "AWA /api/health" "$apiBaseUrl/health" $SmokeTimeoutSeconds "`"activeAiProvider`":`"$aiProvider`""
    $browserSmoke = Wait-Smoke "AWA /health/browser" "$baseUrl/health/browser" $SmokeTimeoutSeconds '"browser":"chromium"'
    $smokeResults = @($rootSmoke, $tenantConfigSmoke, $healthSmoke, $apiHealthSmoke, $browserSmoke)
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
    Write-Host "  deploy-awa.cmd --tenant-name $script:TargetTenantName"
    exit 1
}
