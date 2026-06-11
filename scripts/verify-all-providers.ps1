$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

function Get-EnvMap {
    $map = @{}
    if (Test-Path .env) {
        Get-Content .env |
            Where-Object { $_ -match '^[A-Za-z_][A-Za-z0-9_]*=' } |
            ForEach-Object {
                $k, $v = $_ -split '=', 2
                $map[$k.Trim()] = $v.Trim().Trim('"').Trim("'")
            }
    }

    # Process environment wins when set, without printing secrets.
    Get-ChildItem Env: | ForEach-Object { $map[$_.Name] = $_.Value }
    return $map
}

function Join-Url([string]$base, [string]$path) {
    return ($base.TrimEnd('/') + '/' + $path.TrimStart('/'))
}

function Get-ConfiguredModels([string]$provider) {
    $items = @()
    Get-ChildItem -Path 'config/model-selection' -Filter '*.json' -ErrorAction SilentlyContinue | ForEach-Object {
        try {
            $cfg = Get-Content $_.FullName -Raw | ConvertFrom-Json
            if ($cfg.provider -ne $provider) { return }
            foreach ($m in @($cfg.models)) {
                $enabled = $true
                if ($null -ne $m.enabled -and $m.enabled -eq $false) { $enabled = $false }
                if (-not $enabled) { continue }
                $match = if ($m.match) { [string]$m.match } elseif ($m.model) { [string]$m.model } elseif ($m.id) { [string]$m.id } else { $null }
                if ([string]::IsNullOrWhiteSpace($match)) { continue }
                $items += [pscustomobject]@{
                    provider = $provider
                    match = $match
                    priority = if ($m.priority) { [int]$m.priority } else { 9999 }
                    file = $_.Name
                }
            }
        }
        catch {
            Write-Warning "Could not parse $($_.FullName): $($_.Exception.Message)"
        }
    }

    return @($items | Sort-Object priority, match -Unique)
}

function Get-ModelsFromResponse($resp) {
    $models = @()

    if ($resp.data) {
        $models = @($resp.data | ForEach-Object {
            if ($_ -is [string]) { $_ }
            elseif ($_.id) { $_.id }
            elseif ($_.model) { $_.model }
            elseif ($_.name) { $_.name }
        })
    }
    elseif ($resp.models) {
        $models = @($resp.models | ForEach-Object {
            if ($_ -is [string]) { $_ }
            elseif ($_.model) { $_.model }
            elseif ($_.name) { $_.name }
            elseif ($_.id) { $_.id }
        })
    }

    return @($models | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)
}

function Get-ChatTextFromResponse($resp) {
    if ($resp.choices -and $resp.choices.Count -gt 0) {
        $msg = $resp.choices[0].message
        if ($msg) {
            if (-not [string]::IsNullOrWhiteSpace($msg.content)) { return [string]$msg.content }
            if (-not [string]::IsNullOrWhiteSpace($msg.reasoning_content)) { return [string]$msg.reasoning_content }
            if (-not [string]::IsNullOrWhiteSpace($msg.reasoning)) { return [string]$msg.reasoning }
        }
    }

    if ($resp.message) {
        if (-not [string]::IsNullOrWhiteSpace($resp.message.content)) { return [string]$resp.message.content }
        if (-not [string]::IsNullOrWhiteSpace($resp.message.thinking)) { return [string]$resp.message.thinking }
        if (-not [string]::IsNullOrWhiteSpace($resp.message.reasoning)) { return [string]$resp.message.reasoning }
    }

    return ''
}

function Get-HttpStatusFromError($err) {
    try {
        if ($err.Exception.Response.StatusCode) { return [int]$err.Exception.Response.StatusCode }
    }
    catch { }
    return $null
}

function Get-ErrorBody($err) {
    try {
        $stream = $err.Exception.Response.GetResponseStream()
        if ($stream) {
            $reader = New-Object IO.StreamReader($stream)
            $body = $reader.ReadToEnd()
            $reader.Close()
            if (-not [string]::IsNullOrWhiteSpace($body)) { return $body }
        }
    }
    catch { }
    return $err.Exception.Message
}

$envMap = Get-EnvMap

$providers = @(
    @{ name = 'deepseek'; key = $envMap['PROVIDER_DEEPSEEK_API_KEY']; base = $(if ($envMap['PROVIDER_DEEPSEEK_BASE_URL']) { $envMap['PROVIDER_DEEPSEEK_BASE_URL'] } else { 'https://api.deepseek.com' }); list = 'v1/models'; chat = 'v1/chat/completions'; ollama = $false },
    @{ name = 'openai'; key = $envMap['PROVIDER_OPENAI_API_KEY']; base = $(if ($envMap['PROVIDER_OPENAI_BASE_URL']) { $envMap['PROVIDER_OPENAI_BASE_URL'] } else { 'https://api.openai.com' }); list = 'v1/models'; chat = 'v1/chat/completions'; ollama = $false },
    @{ name = 'nvidia'; key = $envMap['PROVIDER_NVIDIA_API_KEY']; base = $(if ($envMap['PROVIDER_NVIDIA_BASE_URL']) { $envMap['PROVIDER_NVIDIA_BASE_URL'] } else { 'https://integrate.api.nvidia.com' }); list = 'v1/models'; chat = 'v1/chat/completions'; ollama = $false },
    @{ name = 'openrouter'; key = $envMap['PROVIDER_OPENROUTER_API_KEY']; base = $(if ($envMap['PROVIDER_OPENROUTER_BASE_URL']) { $envMap['PROVIDER_OPENROUTER_BASE_URL'] } else { 'https://openrouter.ai/api' }); list = 'v1/models'; chat = 'v1/chat/completions'; ollama = $false },
    @{ name = 'groq'; key = $envMap['PROVIDER_GROQ_API_KEY']; base = $(if ($envMap['PROVIDER_GROQ_BASE_URL']) { $envMap['PROVIDER_GROQ_BASE_URL'] } else { 'https://api.groq.com/openai' }); list = 'v1/models'; chat = 'v1/chat/completions'; ollama = $false },
    @{ name = 'ollama'; key = $(if ($envMap['PROVIDER_OLLAMACLOUD_API_KEY']) { $envMap['PROVIDER_OLLAMACLOUD_API_KEY'] } else { $envMap['PROVIDER_OLLAMA_API_KEY'] }); base = $(if ($envMap['PROVIDER_OLLAMA_BASE_URL']) { $envMap['PROVIDER_OLLAMA_BASE_URL'] } else { 'https://ollama.com' }); list = 'api/tags'; chat = 'api/chat'; ollama = $true },
    @{ name = 'moonshot'; key = $envMap['PROVIDER_MOONSHOT_API_KEY']; base = $(if ($envMap['PROVIDER_MOONSHOT_BASE_URL']) { $envMap['PROVIDER_MOONSHOT_BASE_URL'] } else { 'https://api.moonshot.ai' }); list = 'v1/models'; chat = 'v1/chat/completions'; ollama = $false },
    @{ name = 'cerebras'; key = $envMap['PROVIDER_CEREBRAS_API_KEY']; base = $(if ($envMap['PROVIDER_CEREBRAS_BASE_URL']) { $envMap['PROVIDER_CEREBRAS_BASE_URL'] } else { 'https://api.cerebras.ai' }); list = 'v1/models'; chat = 'v1/chat/completions'; ollama = $false }
)

$providerRows = @()
$modelRows = @()

foreach ($p in $providers) {
    $configured = @(Get-ConfiguredModels $p.name)
    if ([string]::IsNullOrWhiteSpace($p.key)) {
        $providerRows += [pscustomobject]@{ provider = $p.name; status = 'missing_key'; models = 0; configured = $configured.Count; chat_ok = 0; chat_total = $configured.Count; latency_avg_ms = 0; error = 'missing API key' }
        foreach ($c in $configured) {
            $modelRows += [pscustomobject]@{ provider = $p.name; configured_model = $c.match; catalog_model = ''; list_status = 'missing_key'; chat_status = 'not_tested'; latency_ms = 0; sample = ''; error = 'missing API key' }
        }
        continue
    }

    $headers = @{ Authorization = "Bearer $($p.key)" }
    if ($p.name -eq 'openrouter') {
        if ($envMap['PROVIDER_OPENROUTER_REFERER']) { $headers['HTTP-Referer'] = $envMap['PROVIDER_OPENROUTER_REFERER'] }
        if ($envMap['PROVIDER_OPENROUTER_TITLE']) { $headers['X-Title'] = $envMap['PROVIDER_OPENROUTER_TITLE'] }
    }

    $models = @()
    $listStatus = 'error'
    $listError = ''
    try {
        $listResp = Invoke-RestMethod -Uri (Join-Url $p.base $p.list) -Method Get -Headers $headers -TimeoutSec 45
        $models = @(Get-ModelsFromResponse $listResp)
        $listStatus = 'ok'
    }
    catch {
        $status = Get-HttpStatusFromError $_
        $listStatus = if ($status) { "http_$status" } else { 'error' }
        $listError = Get-ErrorBody $_
    }

    $chatOk = 0
    $latencies = @()
    foreach ($c in $configured) {
        $catalogModel = $models | Where-Object { $_ -ieq $c.match } | Select-Object -First 1
        if (-not $catalogModel) { $catalogModel = $models | Where-Object { $_ -match [regex]::Escape($c.match) } | Select-Object -First 1 }
        if (-not $catalogModel) { $catalogModel = $c.match }

        $chatStatus = 'not_tested'
        $latency = 0
        $sample = ''
        $err = $listError

        if ($listStatus -eq 'ok') {
            try {
                if ($p.ollama) {
                    $bodyObj = @{ model = $catalogModel; stream = $false; messages = @(@{ role = 'user'; content = 'Reply exactly OK' }) }
                }
                else {
                    $bodyObj = @{ model = $catalogModel; stream = $false; max_tokens = 32; temperature = 0.2; messages = @(@{ role = 'user'; content = 'Reply exactly OK' }) }
                }

                $body = $bodyObj | ConvertTo-Json -Depth 8
                $sw = [System.Diagnostics.Stopwatch]::StartNew()
                $chatResp = Invoke-RestMethod -Uri (Join-Url $p.base $p.chat) -Method Post -Headers ($headers + @{ 'Content-Type' = 'application/json' }) -Body $body -TimeoutSec 75
                $sw.Stop()
                $latency = [int]$sw.Elapsed.TotalMilliseconds
                $sample = Get-ChatTextFromResponse $chatResp
                if ($sample.Length -gt 160) { $sample = $sample.Substring(0, 160) }
                if ([string]::IsNullOrWhiteSpace($sample)) { $chatStatus = 'empty_response' } else { $chatStatus = 'ok'; $chatOk++; $latencies += $latency }
                $err = ''
            }
            catch {
                $status = Get-HttpStatusFromError $_
                $chatStatus = if ($status) { "http_$status" } else { 'error' }
                $err = Get-ErrorBody $_
                if ($err.Length -gt 300) { $err = $err.Substring(0, 300) }
            }
        }

        $modelRows += [pscustomobject]@{ provider = $p.name; configured_model = $c.match; catalog_model = $catalogModel; list_status = $listStatus; chat_status = $chatStatus; latency_ms = $latency; sample = $sample; error = $err }
    }

    $avg = if ($latencies.Count -gt 0) { [int](($latencies | Measure-Object -Average).Average) } else { 0 }
    $providerRows += [pscustomobject]@{ provider = $p.name; status = $listStatus; models = $models.Count; configured = $configured.Count; chat_ok = $chatOk; chat_total = $configured.Count; latency_avg_ms = $avg; error = $listError }
}

$proxyPort = if ($envMap['PROXY_PORT']) { $envMap['PROXY_PORT'] } else { '11434' }
$proxyHeaders = @{}
if ($envMap['PROXY_API_KEY']) { $proxyHeaders['Authorization'] = "Bearer $($envMap['PROXY_API_KEY'])" }
$proxyTags = @()
$proxyStatus = 'not_tested'
try {
    $tagsResp = Invoke-RestMethod -Uri "http://127.0.0.1:$proxyPort/api/tags" -Method Get -Headers $proxyHeaders -TimeoutSec 20
    $proxyTags = @($tagsResp.models | ForEach-Object { if ($_.name) { $_.name } elseif ($_.model) { $_.model } })
    $proxyStatus = 'ok'
}
catch {
    $status = Get-HttpStatusFromError $_
    $proxyStatus = if ($status) { "http_$status" } else { 'error' }
}

$generated = (Get-Date).ToUniversalTime().ToString('o')
$statusIcon = @{ ok = '✅'; missing_key = '⚪'; not_tested = '⚪'; empty_response = '⚠️'; error = '❌' }

$md = @()
$md += '# Provider connectivity status'
$md += ''
$md += ('Generated UTC: ' + $generated)
$md += ''
$md += '## Provider summary'
$md += ''
$md += '| Provider | Catalog | Catalog models | Configured models | Chat OK | Avg latency |'
$md += '|---|---:|---:|---:|---:|---:|'
foreach ($r in $providerRows | Sort-Object provider) {
    $ico = $statusIcon[$r.status]
    if (-not $ico) { $ico = $statusIcon['error'] }
    $md += ('| ' + $r.provider + ' | ' + $ico + ' ' + $r.status + ' | ' + $r.models + ' | ' + $r.configured + ' | ' + $r.chat_ok + '/' + $r.chat_total + ' | ' + $r.latency_avg_ms + 'ms |')
}

$md += ''
$md += '## Configured model checks'
$md += ''
$md += '| Provider | Configured model | Catalog/chat model | Catalog | Chat | Latency | Sample/Error |'
$md += '|---|---|---|---:|---:|---:|---|'
foreach ($r in $modelRows | Sort-Object provider, configured_model) {
    $chatIco = $statusIcon[$r.chat_status]
    if (-not $chatIco) { $chatIco = $statusIcon['error'] }
    $msg = if ($r.chat_status -eq 'ok') { $r.sample } else { $r.error }
    $msg = $msg.ToString().Replace('|', '\|').Replace([char]13, ' ').Replace([char]10, ' ')
    $md += ('| ' + $r.provider + ' | `' + $r.configured_model + '` | `' + $r.catalog_model + '` | ' + $r.list_status + ' | ' + $chatIco + ' ' + $r.chat_status + ' | ' + $r.latency_ms + 'ms | ' + $msg + ' |')
}

$md += ''
$md += '## Copilot BYOM list from proxy `/api/tags`'
$md += ''
$md += ('Proxy status: ' + $proxyStatus)
$md += ''
foreach ($tag in $proxyTags | Sort-Object) { $md += ('- `' + $tag + '`') }

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$jsonPath = ('docs/testing/provider-status-' + $stamp + '.json')
$mdPath = 'docs/testing/provider-status.md'

[pscustomobject]@{
    generated_at_utc = $generated
    providers = $providerRows
    models = $modelRows
    proxy = [pscustomobject]@{ status = $proxyStatus; tags = $proxyTags }
} | ConvertTo-Json -Depth 10 | Set-Content -Path $jsonPath -Encoding UTF8

$md | Set-Content -Path $mdPath -Encoding UTF8

Write-Host ('Markdown report: ' + $mdPath)
Write-Host ('JSON report: ' + $jsonPath)
$providerRows | Sort-Object provider | Format-Table provider, status, models, configured, chat_ok, chat_total, latency_avg_ms -AutoSize
$tagCount = $proxyTags.Count
Write-Host ('Proxy /api/tags status: ' + $proxyStatus + ' (' + $tagCount + ' entries)')
