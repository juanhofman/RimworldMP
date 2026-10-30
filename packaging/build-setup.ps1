# Build RimworldMP-Setup-<ver>.exe (single installer for Person 1 + Person 2).
# Runs on Windows PowerShell 5.1+ / PowerShell 7+.
param(
    [switch]$SkipHarmonyDownload,
    [string]$StorePath = "",
    [switch]$NoDesktopCopy,
    [string]$HarmonyReleaseTag = "v3.0.1.0"
)

$ErrorActionPreference = "Stop"
$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
$Stage = Join-Path $PSScriptRoot "stage"
$Dist = Join-Path $PSScriptRoot "dist"
$Version = (Get-Content (Join-Path $Root "VERSION") -Raw).Trim()
if ([string]::IsNullOrWhiteSpace($Version)) { throw "VERSION file missing or empty." }

function Find-ISCC {
    $candidates = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    )
    foreach ($c in $candidates) {
        if (Test-Path $c) { return $c }
    }
    $cmd = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    throw "ISCC.exe not found. Install Inno Setup 6 (winget install JRSoftware.InnoSetup)."
}

Write-Host "== RimworldMP Setup build v$Version ==" -ForegroundColor Cyan

if (Test-Path $Stage) {
    if ($SkipHarmonyDownload -and (Test-Path (Join-Path $Stage "Mods\Harmony"))) {
        Get-ChildItem $Stage -Force | Where-Object { $_.Name -ne "Mods" } | Remove-Item -Recurse -Force
        Get-ChildItem (Join-Path $Stage "Mods") -Force | Where-Object { $_.Name -ne "Harmony" } | Remove-Item -Recurse -Force
    }
    else {
        Remove-Item $Stage -Recurse -Force
    }
}
New-Item -ItemType Directory -Force -Path $Stage, $Dist, (Join-Path $Stage "Launcher"), (Join-Path $Stage "Mods") | Out-Null

Write-Host "Publishing launcher..." -ForegroundColor Yellow
$launcherProj = Join-Path $Root "tools\RimworldMP.Launcher\RimworldMP.Launcher.csproj"
dotnet publish $launcherProj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:TreatWarningsAsErrors=true `
    -o (Join-Path $Stage "Launcher")
if ($LASTEXITCODE -ne 0) { throw "dotnet publish launcher failed." }

Write-Host "Building / staging RimworldMP mod..." -ForegroundColor Yellow
$modProj = Join-Path $Root "src\RimworldMP\RimworldMP.csproj"
if (-not [string]::IsNullOrWhiteSpace($env:RIMWORLD_DIR) -and (Test-Path $env:RIMWORLD_DIR)) {
    Write-Host "  RIMWORLD_DIR set (live Verse refs)" -ForegroundColor DarkGray
}
else {
    Write-Host "  RIMWORLD_DIR unset - packaging stub assemblies (set RIMWORLD_DIR for live play builds)." -ForegroundColor DarkYellow
}
dotnet build $modProj -c Release -p:TreatWarningsAsErrors=true
if ($LASTEXITCODE -ne 0) { throw "dotnet build RimworldMP failed." }

$modSrc = Join-Path $Root "mod"
$modDst = Join-Path $Stage "Mods\RimworldMP"
Copy-Item -Recurse -Force $modSrc $modDst
Get-ChildItem $modDst -Recurse -Include "Assembly-CSharp.dll","Waylith.dll","UnityStubs.dll","UnityEngine*.dll" -ErrorAction SilentlyContinue |
    Remove-Item -Force -ErrorAction SilentlyContinue

$harmonyDst = Join-Path $Stage "Mods\Harmony"
if (-not (Test-Path (Join-Path $harmonyDst "About\About.xml")) -or -not $SkipHarmonyDownload) {
    Write-Host "Downloading Harmony $HarmonyReleaseTag..." -ForegroundColor Yellow
    $tmp = Join-Path $env:TEMP "RimworldMP-Harmony-$HarmonyReleaseTag"
    if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $tmp | Out-Null
    $zip = Join-Path $tmp "HarmonyMod.zip"
    $url = "https://github.com/pardeike/HarmonyRimWorld/releases/download/$HarmonyReleaseTag/HarmonyMod.zip"
    Invoke-WebRequest -Uri $url -OutFile $zip -UseBasicParsing
    Expand-Archive -Path $zip -DestinationPath (Join-Path $tmp "extract") -Force

    if (Test-Path $harmonyDst) { Remove-Item $harmonyDst -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $harmonyDst | Out-Null

    $aboutHit = Get-ChildItem (Join-Path $tmp "extract") -Recurse -Filter "About.xml" |
        Where-Object { $_.FullName -match '[\\/]About[\\/]About\.xml$' } |
        Select-Object -First 1
    if (-not $aboutHit) { throw "Harmony zip missing About/About.xml" }
    $harmonyRoot = $aboutHit.Directory.Parent.FullName
    Copy-Item -Recurse -Force (Join-Path $harmonyRoot "*") $harmonyDst
    Write-Host "  Harmony staged OK" -ForegroundColor DarkGray
}

if (-not (Test-Path (Join-Path $Stage "Launcher\RimworldMP.Launcher.exe"))) {
    throw "Launcher exe missing from stage."
}
if (-not (Test-Path (Join-Path $Stage "Mods\RimworldMP\About\About.xml"))) {
    throw "RimworldMP About.xml missing from stage."
}
if (-not (Test-Path (Join-Path $Stage "Mods\Harmony\About\About.xml"))) {
    throw "Harmony About.xml missing from stage."
}

$iscc = Find-ISCC
Write-Host "Compiling installer with ISCC..." -ForegroundColor Yellow
$iss = Join-Path $PSScriptRoot "RimworldMP.iss"
Push-Location $PSScriptRoot
try {
    & $iscc "/DMyAppVersion=$Version" "/DMyStageDir=stage" "/DMyDistDir=dist" $iss
    if ($LASTEXITCODE -ne 0) { throw "ISCC failed." }
}
finally {
    Pop-Location
}

$setupName = "RimworldMP-Setup-$Version.exe"
$setupPath = Join-Path $Dist $setupName
if (-not (Test-Path $setupPath)) { throw "Expected output missing: $setupPath" }

if (-not $NoDesktopCopy) {
    $desktop = [Environment]::GetFolderPath("Desktop")
    $deskOut = Join-Path $desktop $setupName
    Copy-Item -Force $setupPath $deskOut
    Write-Host "Desktop: $deskOut" -ForegroundColor Green
}

if (-not [string]::IsNullOrWhiteSpace($StorePath)) {
    $storeDir = Split-Path -Parent $StorePath
    if (-not [string]::IsNullOrWhiteSpace($storeDir)) {
        New-Item -ItemType Directory -Force -Path $storeDir | Out-Null
    }
    Copy-Item -Force $setupPath $StorePath
    Write-Host "Store:   $StorePath" -ForegroundColor Green
}

$sizeMb = [math]::Round((Get-Item $setupPath).Length / 1MB, 1)
Write-Host "Built:   $setupPath" -ForegroundColor Green
Write-Host "Size:    $sizeMb MB"
Write-Output $setupPath
