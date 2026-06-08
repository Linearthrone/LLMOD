$lines = Get-Content 'start.bat' -TotalCount 141
Set-Content -Path '_verify.bat' -Value $lines
Add-Content '_verify.bat' 'echo PARSED_THROUGH_KOKORO_OK'
Write-Output 'made _verify.bat'
