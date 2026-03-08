param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$PublishDir = ".\publish\HouseVictoria",
    [string]$InnoSetupPath = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
)

Write-Host "=== Building HouseVictoria app ($Configuration, $Runtime) ==="

$solutionRoot = Split-Path -Parent $PSScriptRoot
Set-Location $solutionRoot

if (-not (Test-Path $InnoSetupPath)) {
    Write-Warning "Inno Setup compiler not found at '$InnoSetupPath'."
    Write-Warning "Please install Inno Setup 6+ and/or update the -InnoSetupPath parameter."
    exit 1
}

dotnet publish ".\HouseVictoria.App\HouseVictoria.App.csproj" `
    -c $Configuration `
    -r $Runtime `
    -o $PublishDir `
    --self-contained false

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed. Aborting."
    exit $LASTEXITCODE
}

Write-Host "=== Building installer with Inno Setup ==="

& $InnoSetupPath ".\Installer\HouseVictoria.iss"

if ($LASTEXITCODE -ne 0) {
    Write-Error "Inno Setup compilation failed."
    exit $LASTEXITCODE
}

Write-Host "=== Installer build complete. Check the 'Installer' folder for 'HouseVictoriaSetup.exe'. ==="

