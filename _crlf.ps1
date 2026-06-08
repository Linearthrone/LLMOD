$path = 'start.bat'
$t = [IO.File]::ReadAllText($path)
$t = $t.Replace("`r`n", "`n").Replace("`n", "`r`n")
$enc = New-Object System.Text.UTF8Encoding($false)
[IO.File]::WriteAllText($path, $t, $enc)
$b = [IO.File]::ReadAllBytes($path)
$crlf = 0; $lfonly = 0
for ($i = 0; $i -lt $b.Length; $i++) {
    if ($b[$i] -eq 10) {
        if ($i -gt 0 -and $b[$i-1] -eq 13) { $crlf++ } else { $lfonly++ }
    }
}
Write-Output "converted: CRLF=$crlf LF_only=$lfonly"
