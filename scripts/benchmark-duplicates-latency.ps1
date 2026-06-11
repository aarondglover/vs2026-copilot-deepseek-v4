$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$proxy = 'http://127.0.0.1:11434'
$runs = 3

# Duplicate model groups identified from /api/tags
# Format: modelName (provider):latest  - the proxy routes this natively
$dupes = @(
    @{ Group = 'deepseek-v4-pro';       Variants = @(@{Display='deepseek-v4-pro (deepseek)'; Prov='deepseek'}, @{Display='deepseek-v4-pro (ollama)'; Prov='ollama'}) }
    @{ Group = 'kimi-k2.6';             Variants = @(@{Display='kimi-k2.6 (moonshot)'; Prov='moonshot'}, @{Display='kimi-k2.6 (ollama)'; Prov='ollama'}) }
    @{ Group = 'moonshotai/kimi-k2.6';  Variants = @(@{Display='moonshotai/kimi-k2.6 (nvidia)'; Prov='nvidia'}, @{Display='moonshotai/kimi-k2.6 (openrouter)'; Prov='openrouter'}) }
    @{ Group = 'nvidia/nemotron-3-super-120b-a12b'; Variants = @(@{Display='nvidia/nemotron-3-super-120b-a12b (nvidia)'; Prov='nvidia'}, @{Display='nvidia/nemotron-3-super-120b-a12b (openrouter)'; Prov='openrouter'}) }
    @{ Group = 'openai/gpt-oss-120b';    Variants = @(@{Display='openai/gpt-oss-120b (groq)'; Prov='groq'}, @{Display='openai/gpt-oss-120b (nvidia)'; Prov='nvidia'}) }
)

$allResults = @()

foreach ($group in $dupes) {
    Write-Host ('>>> GROUP: ' + $group.Group + ' <<<')
    foreach ($v in $group.Variants) {
        $body = @{
            model = $v.Display
            stream = $false
            messages = @(@{ role = 'user'; content = 'Say OK' })
        } | ConvertTo-Json -Depth 4

        $timings = @()
        $errors = 0
        $errorMsg = ''

        for ($i = 1; $i -le $runs; $i++) {
            try {
                $sw = [System.Diagnostics.Stopwatch]::StartNew()
                $null = Invoke-RestMethod -Uri "$proxy/api/chat" -Method Post -Body $body -ContentType 'application/json' -TimeoutSec 120
                $sw.Stop()
                $timings += $sw.ElapsedMilliseconds
                Write-Host "  $($v.Prov): run $i = $($sw.ElapsedMilliseconds)ms"
            } catch {
                $errors++
                $errorMsg = $_.Exception.Message
                Write-Host "  $($v.Prov): run $i = ERROR: $errorMsg"
            }
        }

        $avg = if ($timings.Count -gt 0) { [math]::Round(($timings | Measure-Object -Average).Average, 0) } else { -1 }
        $med = if ($timings.Count -gt 0) { ($timings | Sort-Object)[[math]::Floor($timings.Count / 2)] } else { -1 }
        $min = if ($timings.Count -gt 0) { ($timings | Measure-Object -Minimum).Minimum } else { -1 }
        $max = if ($timings.Count -gt 0) { ($timings | Measure-Object -Maximum).Maximum } else { -1 }

        $allResults += [pscustomobject]@{
            Group = $group.Group
            Provider = $v.Prov
            Display = $v.Display
            Runs = ($timings -join ',')
            Success = ($runs - $errors)
            AvgMs = $avg
            MedMs = $med
            MinMs = $min
            MaxMs = $max
            Error = $errorMsg
        }
    }
    Write-Host ''
}

Write-Host '========== FINAL COMPARISON ==========' -ForegroundColor Cyan

foreach ($group in $dupes) {
    $groupResults = $allResults | Where-Object { $_.Group -eq $group.Group } | Sort-Object AvgMs
    Write-Host ('' + $group.Group + ':')
    $best = $groupResults | Select-Object -First 1
    foreach ($r in $groupResults) {
        $marker = if ($r.Provider -eq $best.Provider -and $r.AvgMs -gt 0) { ' <- FASTEST' } else { '' }
        $status = if ($r.AvgMs -gt 0) { $r.AvgMs.ToString() + 'ms' } else { 'FAIL' }
        Write-Host ('  ' + $r.Provider.PadRight(12) + ' avg=' + $status.PadRight(10) + ' med=' + $r.MedMs.ToString().PadRight(6) + ' [' + $r.Runs + ']' + $marker)
    }

    # Disable slower: set enabled=false for all non-winning providers
    foreach ($r in $groupResults | Select-Object -Skip 1) {
        if ($r.AvgMs -gt 0 -and $best.AvgMs -gt 0) {
            $configFile = ('config/model-selection/' + $r.Provider + '.json')
            Write-Host ('  -> DISABLE ' + $r.Provider + ' for ' + $group.Group + ' (slower by ' + ($r.AvgMs - $best.AvgMs) + 'ms)')
        }
    }
}

# Save results
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$reportPath = 'docs/testing/logs/duplicate-latency-benchmark-' + $stamp + '.json'
New-Item -ItemType Directory -Path 'docs/testing/logs' -Force | Out-Null
$allResults | Sort-Object Group, AvgMs | ConvertTo-Json -Depth 4 | Set-Content -Path $reportPath -Encoding UTF8
Write-Host ('Report: ' + $reportPath)