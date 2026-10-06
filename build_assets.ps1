# HS_Escalator — rebuild balustrade FBX from Blender, then the Unity asset bundle.
# Requires Unity 2022.3.62f2 (the game's version). Do not use Unity 6.

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$BlenderScript = Join-Path $Root "_blender\hsescalator_models.py"
$Models = Join-Path $Root "_unity\Assets\HSEscalator\Models"
$IconOut = Join-Path $Root "UIAtlases\ItemIconAtlas"
$UnityProj = Join-Path $Root "_unity"
$UnityLog = Join-Path $Root "_unity_build.log"
$UnityExe = "C:\Program Files\Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe"

function Find-OfficialBlender {
    $candidates = @()
    $pf = "C:\Program Files\Blender Foundation"
    if (Test-Path $pf) {
        $candidates += Get-ChildItem $pf -Recurse -Filter blender.exe -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -notmatch '\\WindowsApps\\' } |
            Sort-Object FullName -Descending |
            Select-Object -ExpandProperty FullName
    }
    $local = Join-Path $env:LOCALAPPDATA "Programs\Blender"
    if (Test-Path $local) {
        $candidates += Get-ChildItem $local -Recurse -Filter blender.exe -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -notmatch '\\WindowsApps\\' } |
            Sort-Object FullName -Descending |
            Select-Object -ExpandProperty FullName
    }
    $cmd = Get-Command blender -ErrorAction SilentlyContinue
    if ($cmd -and $cmd.Source -and $cmd.Source -notmatch '\\WindowsApps\\') {
        $candidates += $cmd.Source
    }
    foreach ($p in $candidates) {
        if ($p -and (Test-Path $p)) { return $p }
    }
    return $null
}

$blender = Find-OfficialBlender
if (-not $blender) { throw "Official blender.exe not found. Install from blender.org (not the Microsoft Store)." }
if (-not (Test-Path $UnityExe)) { throw "Unity 2022.3.62f2 not found at $UnityExe" }

Write-Host "Blender: $blender"
& $blender --background --factory-startup --python $BlenderScript -- $Models $IconOut
if ($LASTEXITCODE -ne 0) { throw "Blender export failed ($LASTEXITCODE)" }

Write-Host "Unity: $UnityExe"
Remove-Item (Join-Path $UnityProj "build_report.txt") -ErrorAction SilentlyContinue
Remove-Item $UnityLog -ErrorAction SilentlyContinue
$before = @(Get-Process Unity -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id)
& $UnityExe -batchmode -nographics -projectPath $UnityProj -executeMethod HSEscalatorBuild.Build -quit -logFile $UnityLog
# Unity's launcher process returns before the editor does. Wait for the editor that actually started.
$deadline = (Get-Date).AddMinutes(20)
$report = Join-Path $UnityProj "build_report.txt"
do {
    Start-Sleep -Seconds 3
    $alive = @(Get-Process Unity -ErrorAction SilentlyContinue | Where-Object { $before -notcontains $_.Id })
    $tail = if (Test-Path $report) { @(Get-Content $report -Tail 1)[0] } else { "" }
    if ($tail -eq "DONE" -and -not $alive) { break }
    if ($tail -match "^FAILED") { throw "Unity bundle build failed. See $UnityLog" }
} while ((Get-Date) -lt $deadline)
if ($alive) { throw "Unity bundle build timed out. See $UnityLog" }
$report = Join-Path $UnityProj "build_report.txt"
if (-not (Test-Path $report) -or (Get-Content $report -Tail 1) -ne "DONE") { throw "Unity bundle build failed. See $UnityLog" }
Write-Host "Done. Bundle: $(Join-Path $Root 'Resources\HSEscalator.unity3d')"
