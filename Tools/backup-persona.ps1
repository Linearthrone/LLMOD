<#
.SYNOPSIS
  Reference for House Victoria persona backups.

.DESCRIPTION
  Persona backup/restore is built into the app:
    AI Models window -> Contact Book
      - Per persona: "Backup" button (exports a .zip)
      - Top right: "Restore Backup" button (imports a .zip)

  Each .zip contains:
    - persona.json        (definition, system prompt, LLM params, knowledge sharing)
    - memories.json       (all SQLite memory rows for this persona)
    - databanks.json      (personal databank entries + attachments)
    - conversations.json    (chat thread metadata)
    - messages/*.json     (full message history)
    - files/              (media, portraits, databank attachments, persona folder)

  After restoring on a new machine:
    1. Open AI Models -> edit the persona -> set the new Model name
    2. Load the model in Ollama/LM Studio
    3. Chat history and memories carry over automatically

  Manual locations (if you need to inspect raw data):
    - Database:  Data\Memory\HouseVictoria.db
    - Persona folder: Data\Databanks\{persona-guid}\
    - Chat media: Data\Media\conv-{persona-guid}\
#>

param(
    [string]$PersonaId,
    [string]$AppDir
)

$RepoRoot = Split-Path -Parent $PSScriptRoot

if (-not $AppDir) {
    foreach ($c in @(
        (Join-Path $RepoRoot 'HouseVictoria.App\bin\Release\net8.0-windows'),
        (Join-Path $RepoRoot 'HouseVictoria.App\bin\Debug\net8.0-windows')
    )) {
        if (Test-Path (Join-Path $c 'HouseVictoria.App.exe')) { $AppDir = $c; break }
    }
}

if (-not $AppDir) {
    Write-Host 'Run the app from Visual Studio or build first, then re-run this script.'
    exit 1
}

$db = Join-Path $AppDir 'Data\Memory\HouseVictoria.db'
Write-Host "App directory: $AppDir"
Write-Host "Database:      $db"
Write-Host ''
Write-Host 'Use the in-app Backup / Restore Backup buttons in AI Models > Contact Book.'
Write-Host ''

if ($PersonaId) {
    $folder = Join-Path $AppDir "Data\Databanks\$PersonaId"
    $media  = Join-Path $AppDir "Data\Media\conv-$PersonaId"
    Write-Host "Persona folder: $(if (Test-Path $folder) { $folder } else { '(not found)' })"
    Write-Host "Chat media:     $(if (Test-Path $media) { $media } else { '(not found)' })"
}
