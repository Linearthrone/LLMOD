$b = [IO.File]::ReadAllBytes('start.bat')
$crlf = 0
$lfonly = 0
for ($i = 0; $i -lt $b.Length; $i++) {
    if ($b[$i] -eq 10) {
        if ($i -gt 0 -and $b[$i-1] -eq 13) { $crlf++ } else { $lfonly++ }
    }
}
Write-Output "CRLF=$crlf  LF_only=$lfonly"
