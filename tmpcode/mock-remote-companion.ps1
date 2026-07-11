<#
  Temporary mock of the Remote Companion HTTP contract for TASK-016 verification.
  Mirrors HouseVictoria.App/RemoteCompanion/RemoteCompanionWebHost.cs:
    - 401 {"error":"unauthorized"} when Bearer/X-Api-Key token mismatch
    - 400 {"error":"message_required"} when body.message is empty
    - 400 {"error":"No AI contact ..."} downstream business error when no contact configured
    - 200 {"reply":...} otherwise
  Not product code. Lives in tmpcode/ (gitignored) per DEV-01 temp-script rule.
#>
param(
    [int]$Port = 17890,
    [string]$Token = 'REDACTED_TEST_TOKEN_2026',
    [string]$ContactId = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$listener = New-Object System.Net.HttpListener
$listener.Prefixes.Add("http://127.0.0.1:$Port/")
$listener.Start()
Write-Host "[mock] listening on http://127.0.0.1:$Port/ (Ctrl+C to stop)"

function Write-Json {
    param($Context, [int]$Status, [hashtable]$Obj)
    $json = ($Obj | ConvertTo-Json -Compress)
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($json)
    $Context.Response.StatusCode = $Status
    $Context.Response.ContentType = 'application/json; charset=utf-8'
    $Context.Response.OutputStream.Write($bytes, 0, $bytes.Length)
    $Context.Response.OutputStream.Close()
}

try {
    while ($listener.IsListening) {
        $ctx = $listener.GetContext()
        $path = $ctx.Request.Url.AbsolutePath
        $method = $ctx.Request.HttpMethod

        if ($path -eq '/api/remote/v1/health') {
            Write-Json $ctx 200 @{ ok = $true }
            continue
        }

        if ($path -eq '/api/remote/v1/chat' -and $method -eq 'POST') {
            $auth = $ctx.Request.Headers['Authorization']
            $apiKey = $ctx.Request.Headers['X-Api-Key']
            $authed = $false
            if ($auth -and $auth -match '^Bearer\s+(.+)$') {
                if ($Matches[1].Trim() -ceq $Token) { $authed = $true }
            }
            if (-not $authed -and $apiKey -and ($apiKey -ceq $Token)) { $authed = $true }

            if (-not $authed) {
                Write-Json $ctx 401 @{ error = 'unauthorized' }
                continue
            }

            $reader = New-Object System.IO.StreamReader($ctx.Request.InputStream, $ctx.Request.ContentEncoding)
            $raw = $reader.ReadToEnd()
            $reader.Close()

            $msg = $null
            try { $msg = ($raw | ConvertFrom-Json).message } catch { $msg = $null }

            if ([string]::IsNullOrWhiteSpace($msg)) {
                Write-Json $ctx 400 @{ error = 'message_required' }
                continue
            }

            if ([string]::IsNullOrWhiteSpace($ContactId)) {
                Write-Json $ctx 400 @{ error = 'No AI contact found. Configure RemoteCompanionAiContactId.' }
                continue
            }

            Write-Json $ctx 200 @{ reply = "pong: $msg"; conversationId = 'mock-1' }
            continue
        }

        Write-Json $ctx 404 @{ error = 'not_found' }
    }
}
finally {
    $listener.Stop()
    $listener.Close()
}
