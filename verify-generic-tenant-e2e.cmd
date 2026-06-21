@echo off
chcp 65001 >nul
setlocal EnableExtensions EnableDelayedExpansion

set "OYAKO_SCRIPT_SELF=%~f0"
set "OYAKO_SCRIPT_PS1=%TEMP%\oyako-generic-tenant-e2e-%RANDOM%-%RANDOM%.ps1"

powershell -NoProfile -ExecutionPolicy Bypass -Command "$ErrorActionPreference='Stop'; $src=Get-Content -Raw -Encoding UTF8 -LiteralPath $env:OYAKO_SCRIPT_SELF; $parts=$src -split '(?m)^:POWERSHELL_BEGIN\r?$',2; if($parts.Count -lt 2){ throw 'PowerShell payload marker was not found.' }; Set-Content -LiteralPath $env:OYAKO_SCRIPT_PS1 -Value $parts[1] -Encoding UTF8"
if errorlevel 1 exit /b %ERRORLEVEL%

powershell -NoProfile -ExecutionPolicy Bypass -File "%OYAKO_SCRIPT_PS1%"
set "OYAKO_SCRIPT_EXIT=%ERRORLEVEL%"

if exist "%~dp0.verification\generic-tenant-e2e.log" (
  type "%~dp0.verification\generic-tenant-e2e.log"
)

del "%OYAKO_SCRIPT_PS1%" >nul 2>nul
exit /b %OYAKO_SCRIPT_EXIT%

:POWERSHELL_BEGIN
param()

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
Set-Variable -Name PSNativeCommandUseErrorActionPreference -Value $false -Scope Script -ErrorAction SilentlyContinue
[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)

$Root = Split-Path -Parent $env:OYAKO_SCRIPT_SELF
$ApiBaseUrl = "http://localhost:5000"
$TenantName = "generictenant"
$SourceName = "generic-tenant"
$SourceAddress = "https://www.generic-tenant.org.tr"
$Question = "generic-tenant nasıl bağış yaparım?"
$VerificationDir = Join-Path $Root ".verification"
$LogPath = Join-Path $VerificationDir "generic-tenant-e2e.log"
New-Item -ItemType Directory -Force -Path $VerificationDir | Out-Null
[IO.File]::WriteAllText($LogPath, "Oyako generic-tenant E2E log`r`n", [System.Text.UTF8Encoding]::new($true))

function Emit([string]$Message = "") {
    [IO.File]::AppendAllText($LogPath, "$Message`r`n", [System.Text.UTF8Encoding]::new($true))
    Write-Output $Message
}

function Step([string]$Message) { Emit ""; Emit "==> $Message" }
function Ok([string]$Message) { Emit "OK: $Message" }
function Fail([string]$Message) { Emit "ERROR: $Message"; throw $Message }

function Write-LocalEnvValue([string]$Path, [string]$Key, [string]$Value) {
    $lines = if (Test-Path -LiteralPath $Path) { @(Get-Content -LiteralPath $Path) } else { @() }
    $pattern = "^\s*$([Regex]::Escape($Key))\s*="
    $updated = $false
    $next = New-Object System.Collections.Generic.List[string]
    foreach ($line in $lines) {
        if ($line -match $pattern) {
            $next.Add("$Key=$Value")
            $updated = $true
        }
        else {
            $next.Add($line)
        }
    }

    if (-not $updated) {
        $next.Add("$Key=$Value")
    }

    [IO.File]::WriteAllLines($Path, [string[]]$next.ToArray(), [System.Text.UTF8Encoding]::new($false))
}

function Invoke-ApiJson([string]$Method, [string]$Path, [object]$Body = $null, [int]$TimeoutSeconds = 300) {
    $parameters = @{
        Method = $Method
        Uri = "$ApiBaseUrl$Path"
        TimeoutSec = $TimeoutSeconds
        Headers = @{ Accept = "application/json" }
    }
    if ($null -ne $Body) {
        $parameters["ContentType"] = "application/json; charset=utf-8"
        $parameters["Body"] = ($Body | ConvertTo-Json -Depth 20 -Compress)
    }

    return Invoke-RestMethod @parameters
}

function Wait-ApiReady {
    $deadline = (Get-Date).AddSeconds(180)
    do {
        try {
            $health = Invoke-ApiJson "GET" "/api/api-health" $null 5
            if ($health.status) {
                return $health
            }
        }
        catch {
            Start-Sleep -Milliseconds 750
        }
    } while ((Get-Date) -lt $deadline)

    Fail "Local API did not become ready at $ApiBaseUrl within 180 seconds."
}

function Get-generic-tenant($Bank) {
    return @($Bank.sources | Where-Object {
        $_.name -eq $SourceName -or $_.address -eq $SourceAddress
    } | Select-Object -First 1)[0]
}

function Invoke-SourceRedownloadOnce([int]$SourceId) {
    $maxSeconds = 900
    $job = Start-Job -ArgumentList $ApiBaseUrl, $SourceId -ScriptBlock {
        param([string]$BaseUrl, [int]$Id)
        $ErrorActionPreference = "Stop"
        Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/knowledge-source-redownload/$Id" -TimeoutSec 900
    }

    $deadline = (Get-Date).AddSeconds($maxSeconds)
    $lastStatusLine = ""
    try {
        while ($job.State -eq "Running") {
            if ((Get-Date) -gt $deadline) {
                Stop-Job -Job $job -Force
                Fail "generic-tenant redownload exceeded the ${maxSeconds}s hard limit."
            }

            try {
                $runtime = Invoke-ApiJson "GET" "/api/runtime/status" $null 5
                $line = "  $($runtime.operation): step $($runtime.stepIndex)/$($runtime.stepCount), phase=$($runtime.phase), severity=$($runtime.severity), pages=$($runtime.pageCount), message=$($runtime.message)"
                if ($line -ne $lastStatusLine) {
                    Emit $line
                    $lastStatusLine = $line
                }
            }
            catch {
                Emit "  Runtime status poll failed: $($_.Exception.Message)"
            }

            Start-Sleep -Seconds 5
        }

        $result = Receive-Job -Job $job -ErrorAction Stop
        if ($result.status -ne "succeeded") {
            Fail "generic-tenant redownload did not succeed. Status=$($result.status). Message=$($result.message)"
        }

        return $result
    }
    finally {
        Remove-Job -Job $job -Force -ErrorAction SilentlyContinue
    }
}

function Invoke-SourceRedownload([int]$SourceId) {
    for ($attempt = 1; $attempt -le 12; $attempt++) {
        try {
            if ($attempt -gt 1) {
                Emit "  Redownload retry attempt $attempt/12."
            }

            return Invoke-SourceRedownloadOnce $SourceId
        }
        catch {
            $message = $_.Exception.Message
            if ($message -match "409|Conflict" -and $attempt -lt 12) {
                Emit "  Redownload is waiting for the current knowledge operation to finish: $message"
                Start-Sleep -Seconds 10
                continue
            }

            throw
        }
    }

    Fail "generic-tenant redownload could not start after bounded conflict retries."
}

function Test-generic-tenant([string]$Url) {
    try {
        $hostName = ([Uri]$Url).Host.ToLowerInvariant()
        if ($hostName.StartsWith("www.")) {
            $hostName = $hostName.Substring(4)
        }
        return $hostName -eq "generic-tenant.org.tr" -or $hostName.EndsWith(".generic-tenant.org.tr")
    }
    catch {
        return $false
    }
}

function ConvertFrom-SseResponse([string]$Content) {
    $items = New-Object System.Collections.Generic.List[object]
    foreach ($line in ($Content -split "\r?\n")) {
        $trimmed = $line.Trim()
        if (-not $trimmed.StartsWith("data:", [StringComparison]::OrdinalIgnoreCase)) {
            continue
        }

        $data = $trimmed.Substring(5).Trim()
        if ($data.Length -eq 0 -or $data -eq "[DONE]") {
            continue
        }

        try {
            $items.Add(($data | ConvertFrom-Json))
        }
        catch {
        }
    }

    return [object[]]$items.ToArray()
}

function Invoke-Chat([string]$Message) {
    $body = @{ message = $Message } | ConvertTo-Json -Compress
    $response = Invoke-WebRequest `
        -Method Post `
        -Uri "$ApiBaseUrl/api/chat/stream" `
        -ContentType "application/json; charset=utf-8" `
        -Body $body `
        -TimeoutSec 900 `
        -UseBasicParsing

    if ($response.StatusCode -lt 200 -or $response.StatusCode -ge 300) {
        Fail "Chat stream failed with HTTP $($response.StatusCode)."
    }

    $events = ConvertFrom-SseResponse $response.Content
    $answers = @($events | Where-Object { $_.type -eq "answer" -and -not [string]::IsNullOrWhiteSpace([string]$_.answer_content) })
    if ($answers.Count -eq 0) {
        $errors = @($events | Where-Object { $_.type -eq "error" })
        if ($errors.Count -gt 0) {
            Fail "Chat stream returned an error event: $($errors[$errors.Count - 1].content)"
        }

        $snippet = (($response.Content -replace "\s+", " ").Trim())
        if ($snippet.Length -gt 500) {
            $snippet = $snippet.Substring(0, 500)
        }
        Fail "Chat stream did not return an answer payload. SSE snippet: $snippet"
    }

    return $answers[$answers.Count - 1]
}

function Invoke-ChatWithRetry([string]$Message) {
    for ($attempt = 1; $attempt -le 3; $attempt++) {
        try {
            if ($attempt -gt 1) {
                Emit "  Chat retry attempt $attempt/3."
            }

            return Invoke-Chat $Message
        }
        catch {
            $messageText = $_.Exception.Message
            if ($messageText -match "429|Too Many Requests|usage limit" -and $attempt -lt 3) {
                Emit "  Chat provider is rate-limited, waiting before retry: $messageText"
                Start-Sleep -Seconds 30
                continue
            }

            throw
        }
    }
}

try {
    Set-Location -LiteralPath $Root

    Step "Writing local non-secret tenant env settings"
    $tenantEnvPath = Join-Path $Root ".tenants\$TenantName.env"
    if (-not (Test-Path -LiteralPath $tenantEnvPath)) {
        Fail "Tenant env file was not found: $tenantEnvPath"
    }
    Write-LocalEnvValue $tenantEnvPath "domain_only_crawling" "true"
    Write-LocalEnvValue $tenantEnvPath "web_document_max_count" "1000"
    Write-LocalEnvValue $tenantEnvPath "web_document_max_depth" "10"
    Write-LocalEnvValue $tenantEnvPath "primary_ai_provider" "ollama-cloud"
    Write-LocalEnvValue $tenantEnvPath "secondary_ai_provider" "azure-cloud"
    Write-LocalEnvValue $tenantEnvPath "Crawler__MinimumRequestDelayMilliseconds" "50"
    Write-LocalEnvValue $tenantEnvPath "Crawler__MaximumRequestDelayMilliseconds" "150"
    Write-LocalEnvValue $tenantEnvPath "Crawler__RequestTimeoutSeconds" "4"
    Write-LocalEnvValue $tenantEnvPath "Crawler__SourceRefreshEnabled" "false"
    Write-LocalEnvValue $tenantEnvPath "Crawler__LocalKnowledgeRebuildOnStartupEnabled" "false"
    Ok "$TenantName tenant env contains the requested non-secret defaults."

    Step "Starting local Oyako app through run-app.cmd"
    & (Join-Path $Root "run-app.cmd") "--tenant-name" $TenantName "--no-browser"
    if ($LASTEXITCODE -ne 0) {
        Fail "run-app.cmd failed with exit code $LASTEXITCODE."
    }

    Step "Waiting for local API"
    $health = Wait-ApiReady
    Ok "API is ready. Active AI provider: $($health.activeAiProvider)."

    Step "Selecting ollama-cloud as the default AI provider"
    $aiSettings = Invoke-ApiJson "GET" "/api/ai-settings"
    $ollamaCloud = @($aiSettings.providers | Where-Object { $_.id -eq "ollama-cloud" } | Select-Object -First 1)[0]
    if (-not $ollamaCloud) {
        Fail "ollama-cloud provider is not exposed by /api/ai-settings."
    }
    if ([string]::IsNullOrWhiteSpace([string]$ollamaCloud.selectedModel)) {
        Fail "ollama-cloud selected model is empty."
    }
    Invoke-ApiJson "PUT" "/api/ai-settings" @{ provider = "ollama-cloud"; model = $ollamaCloud.selectedModel } | Out-Null
    Ok "ollama-cloud selected model: $($ollamaCloud.selectedModel)."

    Step "Preparing the generic-tenant website source"
    $bank = Invoke-ApiJson "GET" "/api/knowledge-bank"
    $source = Get-generic-tenant $bank
    if ($source) {
        $bank = Invoke-ApiJson "PUT" "/api/knowledge-sources/$($source.id)" @{
            sourceType = "web_site"
            name = $SourceName
            description = "generic-tenant web sitesi"
            address = $SourceAddress
            isEnabled = $true
            redownload = $false
        } 3600
    }
    else {
        $bank = Invoke-ApiJson "POST" "/api/knowledge-sources" @{
            sourceType = "web_site"
            name = $SourceName
            description = "generic-tenant web sitesi"
            address = $SourceAddress
            isEnabled = $true
            redownload = $false
        } 3600
    }

    $source = Get-generic-tenant $bank
    if (-not $source) {
        Fail "generic-tenant source was not found after upsert."
    }

    foreach ($item in @($bank.sources)) {
        $isgeneric-tenant = [int]$item.id -eq [int]$source.id
        if ($item.isArchived -and $isgeneric-tenant) {
            $bank = Invoke-ApiJson "PATCH" "/api/knowledge-sources/$($item.id)/archive" @{ isArchived = $false }
        }
        if ([bool]$item.isEnabled -ne $isgeneric-tenant) {
            $bank = Invoke-ApiJson "PATCH" "/api/knowledge-sources/$($item.id)/enabled" @{ isEnabled = $isgeneric-tenant }
        }
    }
    Ok "Only the generic-tenant source is enabled."

    Step "Redownloading generic-tenant website content"
    $redownload = Invoke-SourceRedownload ([int]$source.id)
    Ok "Redownload completed. Pages=$($redownload.pageCount), warnings=$($redownload.warningCount), errors=$($redownload.errorCount)."

    Step "Validating crawled generic-tenant documents"
    $bank = Invoke-ApiJson "GET" "/api/knowledge-bank"
    $source = Get-generic-tenant $bank
    $activeSources = @($bank.sources | Where-Object { $_.isEnabled -and -not $_.isArchived })
    if ($activeSources.Count -ne 1 -or [int]$activeSources[0].id -ne [int]$source.id) {
        Fail "Expected exactly one enabled, non-archived source: generic-tenant."
    }

    $documents = @($bank.documents | Where-Object { [int]$_.sourceId -eq [int]$source.id })
    $okDocuments = @($documents | Where-Object {
        $_.statusCode -eq "ok" -and -not [string]::IsNullOrWhiteSpace([string]$_.content)
    })
    if ($okDocuments.Count -eq 0) {
        Fail "generic-tenant source has no usable crawled documents."
    }

    $externalDocuments = @($okDocuments | Where-Object { -not (Test-generic-tenant ([string]$_.url)) })
    if ($externalDocuments.Count -gt 0) {
        Fail "Crawler stored documents outside generic-tenant.org.tr: $($externalDocuments[0].url)"
    }

    $uncleanDocuments = @($okDocuments | Where-Object { [string]$_.content -match "<\s*(html|body|script|style)\b" })
    if ($uncleanDocuments.Count -gt 0) {
        Fail "Crawler stored unclean HTML content for document id $($uncleanDocuments[0].id)."
    }

    $donationPattern = "bağış|bagis|sms|168|banka|online|ptt|şube|sube|kripto|mobil"
    $donationDocuments = @($okDocuments | Where-Object {
        "$($_.title) $($_.url) $($_.content)" -match $donationPattern
    })
    if ($donationDocuments.Count -eq 0) {
        Fail "generic-tenant crawl did not store any donation-related document."
    }
    Ok "Stored $($okDocuments.Count) usable generic-tenant documents; donation-related documents: $($donationDocuments.Count)."

    Step "Asking the generic-tenant donation question"
    $answer = Invoke-ChatWithRetry $Question
    $plainAnswer = ([string]$answer.answer_content -replace "<[^>]+>", " " -replace "\s+", " ").Trim()
    $normalizedAnswer = $plainAnswer.ToLowerInvariant()
    if ($normalizedAnswer -notmatch "generic-tenant|generic-tenant" -or $normalizedAnswer -notmatch "bağış|bagis") {
        Fail "Answer does not clearly address generic-tenant donation. Answer: $plainAnswer"
    }

    $methodHits = 0
    foreach ($pattern in @("online", "sms", "banka", "168", "ptt", "şube|sube", "mobil", "kripto", "yurt dışı|yurt disi")) {
        if ($normalizedAnswer -match $pattern) {
            $methodHits++
        }
    }
    if ($methodHits -lt 2) {
        Fail "Answer did not include enough donation methods. Answer: $plainAnswer"
    }

    $attributions = @($answer.source_attributions)
    if ($attributions.Count -gt 0) {
        $outsideAttributions = @($attributions | Where-Object { $_.source_name -ne $SourceName })
        if ($outsideAttributions.Count -gt 0) {
            Fail "Answer used a source outside generic-tenant: $($outsideAttributions[0].source_name)"
        }
    }

    if ($plainAnswer.Length -gt 500) {
        $plainAnswer = $plainAnswer.Substring(0, 500)
    }

    Emit ""
    Emit "generic-tenant local E2E completed."
    Emit "Source: $SourceName <$SourceAddress>"
    Emit "Documents: $($okDocuments.Count) usable, $($documents.Count) total"
    Emit "Question: $Question"
    Emit "Answer snippet: $plainAnswer"
    Emit "Log: $LogPath"
    exit 0
}
catch {
    Emit ""
    Emit "generic-tenant local E2E failed."
    Emit $_.Exception.Message
    Emit ""
    Emit "Retry command:"
    Emit "  verify-generic-tenant-e2e.cmd"
    Emit "Log: $LogPath"
    exit 1
}
