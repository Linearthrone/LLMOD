$t = [IO.File]::ReadAllText('_start_trunc.bat')
$lf = $t.Replace("`r`n", "`n")
[IO.File]::WriteAllText('_start_lf.bat', $lf)
Write-Output "wrote _start_lf.bat"
