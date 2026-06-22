param(
    [Parameter(Mandatory = $true)]
    [string]$Root,

    [string]$ExplicitTenantName = ""
)

$ErrorActionPreference = "Stop"
$OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

function Fail([string]$Message) { throw $Message }

function Read-EnvFile([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        return @{}
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

function EnvValue([hashtable]$Map, [string]$Key, [string]$DefaultValue) {
    if ($Map.ContainsKey($Key) -and -not [string]::IsNullOrWhiteSpace([string]$Map[$Key])) {
        return [string]$Map[$Key]
    }

    return $DefaultValue
}

function Assert-TenantName([string]$TenantName) {
    if ($TenantName -notmatch "^[a-z0-9][a-z0-9-]*[a-z0-9]$|^[a-z0-9]$") {
        Fail "Tenant name '$TenantName' must be lowercase letters, numbers, or hyphen."
    }
}

function Assert-BoolValue([string]$Value, [string]$Name) {
    if ($Value -notin @("true", "false", "True", "False", "TRUE", "FALSE")) {
        Fail "$Name must be true or false."
    }
    return $Value.ToLowerInvariant()
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
        foreach ($key in @("tenant_name", "tenant_enabled")) {
            if (-not $candidateEnv.ContainsKey($key) -or [string]::IsNullOrWhiteSpace([string]$candidateEnv[$key])) {
                Fail "Missing required tenant env key '$key' in $($file.FullName)."
            }
        }

        $fileTenantName = [IO.Path]::GetFileNameWithoutExtension($file.Name)
        if ([string]$candidateEnv["tenant_name"] -ne $fileTenantName) {
            Fail "Tenant file '$($file.FullName)' declares tenant_name='$($candidateEnv["tenant_name"])', expected '$fileTenantName'."
        }

        [void]$discoveredTenants.Add([string]$candidateEnv["tenant_name"])
        if ((EnvValue $candidateEnv "tenant_id" "") -ne $TenantId) { continue }
        if ((Assert-BoolValue ([string]$candidateEnv["tenant_enabled"]) "tenant_enabled") -ne "true") {
            Fail "Tenant '$($candidateEnv["tenant_name"])' matches default_tenant_id but is disabled. Set tenant_enabled=true in '$($file.FullName)' to run or deploy it."
        }

        return [string]$candidateEnv["tenant_name"]
    }

    Fail "default_tenant_id '$TenantId' did not match an enabled tenant. Discovered tenants: $($discoveredTenants -join ', ')"
}

$resolvedRoot = (Resolve-Path -LiteralPath $Root).Path
$Root = $resolvedRoot

if (-not [string]::IsNullOrWhiteSpace($ExplicitTenantName)) {
    Assert-TenantName $ExplicitTenantName
    Write-Output $ExplicitTenantName.Trim()
    exit 0
}

if (-not [string]::IsNullOrWhiteSpace($env:OYAKO_TENANT_NAME)) {
    Assert-TenantName $env:OYAKO_TENANT_NAME
    Write-Output $env:OYAKO_TENANT_NAME.Trim()
    exit 0
}

if (-not [string]::IsNullOrWhiteSpace($env:tenant_name)) {
    Assert-TenantName $env:tenant_name
    Write-Output $env:tenant_name.Trim()
    exit 0
}

$globalEnv = Expand-EnvReferences (Read-EnvFile (Join-Path $Root "oyako.env"))
$defaultTenantId = EnvValue $globalEnv "default_tenant_id" ""
if (-not [string]::IsNullOrWhiteSpace($defaultTenantId)) {
    $tenantName = Resolve-TenantNameById $defaultTenantId
    Assert-TenantName $tenantName
    Write-Output $tenantName
    exit 0
}

$fallbackTenantName = EnvValue $globalEnv "default_tenant_name" "oyakdijital"
Assert-TenantName $fallbackTenantName
Write-Output $fallbackTenantName.Trim()
