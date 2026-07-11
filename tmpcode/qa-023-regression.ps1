$ErrorActionPreference = 'Stop'
$repo = 'C:\Users\kurtw\LLMOD\LLMOD-max-master'
$appExe = Join-Path $repo 'HouseVictoria.App\bin\Release\net8.0-windows\HouseVictoria.App.exe'
$settings = Join-Path $env:LOCALAPPDATA 'HouseVictoria\user-settings.json'
$agentLog = Join-Path $env:LOCALAPPDATA 'hermes\logs\agent.log'
$token = 'REDACTED_TEST_TOKEN_2026'
$contact = '977d778f-2a33-4bca-aab9-4ff893463162'
$marker = "=== QA023-TEST-START $(Get-Date -Format o) ==="

# backup + enable control
Copy-Item $settings "$settings.bak-qa023-$(Get-Date -Format yyyyMMdd-HHmmss)" -Force
$json = Get-Content $settings -Raw | ConvertFrom-Json
$json.AllowComputerControl = $true
$json | ConvertTo-Json -Depth 10 | Set-Content $settings -Encoding UTF8
Write-Host "AllowComputerControl=true written"

# start app
$app = Start-Process -FilePath $appExe -WorkingDirectory (Split-Path $appExe) -PassThru
Write-Host "Started app pid $($app.Id)"
$deadline = (Get-Date).AddSeconds(45)
while ((Get-Date) -lt $deadline) {
    $p17890 = Get-NetTCPConnection -LocalPort 17890 -State Listen -ErrorAction SilentlyContinue
    $p8642 = Get-NetTCPConnection -LocalPort 8642 -State Listen -ErrorAction SilentlyContinue
    if ($p17890 -and $p8642) { break }
    Start-Sleep -Seconds 2
}
if (-not $p17890) { throw '17890 not listening' }
if (-not $p8642) { throw '8642 not listening' }
Write-Host "17890 + 8642 listening"

Add-Content $agentLog "`n$marker`n"

$body = @{ message = 'Take a screenshot of my desktop and tell me the active window title.'; contactId = $contact } | ConvertTo-Json
$headers = @{ Authorization = "Bearer $token" }
Write-Host "Sending chat request..."
$sw = [System.Diagnostics.Stopwatch]::StartNew()
try {
    $resp = Invoke-RestMethod -Uri 'http://127.0.0.1:17890/api/remote/v1/chat' -Method Post -Headers $headers -Body $body -ContentType 'application/json' -TimeoutSec 120
    $sw.Stop()
    Write-Host "HTTP 200 in $($sw.Elapsed.TotalSeconds)s"
    Write-Host "Reply snippet: $($resp.reply.Substring(0, [Math]::Min(200, $resp.reply.Length)))"
} catch {
    $sw.Stop()
    Write-Host "CHAT FAIL: $_"
}

Start-Sleep -Seconds 2
$logTail = Get-Content $agentLog -Tail 80
$sessionLines = $logTail | Where-Object { $_ -ge $marker -or $_ -match 'api-' }
Write-Host "`n--- LOG TAIL (post-marker) ---"
$logTail | ForEach-Object { Write-Host $_ }

$hasComputerUse = ($logTail -join "`n") -match 'computer_use|mcp_computer_use_computer'
$hasBrowserVision = ($logTail -join "`n") -match 'browser_vision'
Write-Host "`ncomputer_use in log tail: $hasComputerUse"
Write-Host "browser_vision in log tail: $hasBrowserVision"

# restore control off, stop app
$json.AllowComputerControl = $false
$json | ConvertTo-Json -Depth 10 | Set-Content $settings -Encoding UTF8
Stop-Process -Id $app.Id -Force -ErrorAction SilentlyContinue
Write-Host "Restored AllowComputerControl=false, stopped app"
