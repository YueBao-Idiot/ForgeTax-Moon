# build.ps1 - compile TianGong cheat menu straight to Mods\ (no NuGet, no .NET Framework targeting pack needed)
#
#   powershell -ExecutionPolicy Bypass -File build.ps1
#
# It uses the Roslyn compiler that ships with the .NET SDK and references the
# assemblies already present in the game folder plus MelonLoader\net35.
param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$sourceDir = $PSScriptRoot
$gameRoot = (Resolve-Path (Join-Path $sourceDir '..')).Path
$managed = Join-Path $gameRoot 'ForgeTax_Data\Managed'
$melon = Join-Path $gameRoot 'MelonLoader\net35'
$outDir = Join-Path $gameRoot 'Mods'
$outDll = Join-Path $outDir 'MoonCheatMod.dll'

if (-not (Test-Path $managed)) { throw "Game assemblies not found: $managed" }
if (-not (Test-Path $melon)) { throw "MelonLoader not installed (missing MelonLoader\net35). Run MelonLoader.Installer first." }

$dotnetExe = (Get-Command dotnet -ErrorAction Stop).Source
$dotnetHome = Split-Path $dotnetExe
$sdkVersion = (& $dotnetExe --version).Trim()
$cscDll = Join-Path $dotnetHome ("sdk\$sdkVersion\Roslyn\bincore\csc.dll")
if (-not (Test-Path $cscDll)) { throw "Roslyn csc.dll not found: $cscDll" }

$refs = @(
    (Join-Path $managed 'mscorlib.dll'),
    (Join-Path $managed 'netstandard.dll'),
    (Join-Path $managed 'System.dll'),
    (Join-Path $managed 'System.Core.dll'),
    (Join-Path $managed 'System.Xml.dll'),
    (Join-Path $managed 'UnityEngine.dll'),
    (Join-Path $managed 'UnityEngine.CoreModule.dll'),
    (Join-Path $managed 'UnityEngine.IMGUIModule.dll'),
    (Join-Path $managed 'UnityEngine.InputLegacyModule.dll'),
    (Join-Path $managed 'UnityEngine.InputModule.dll'),
    (Join-Path $managed 'UnityEngine.TextRenderingModule.dll'),
    (Join-Path $managed 'UnityEngine.ImageConversionModule.dll'),
    (Join-Path $managed 'UnityEngine.JSONSerializeModule.dll'),
    (Join-Path $managed 'UnityEngine.PhysicsModule.dll'),
    (Join-Path $managed 'UnityEngine.UI.dll'),
    (Join-Path $managed 'Unity.InputSystem.dll'),
    (Join-Path $managed 'Unity.TextMeshPro.dll'),
    (Join-Path $managed 'Assembly-CSharp.dll'),
    (Join-Path $melon 'MelonLoader.dll'),
    (Join-Path $melon '0Harmony.dll')
) | Where-Object { Test-Path $_ }

$sources = Get-ChildItem -Path (Join-Path $sourceDir 'ForgeTaxTianGong') -Recurse -Filter *.cs |
    Sort-Object FullName | ForEach-Object { $_.FullName }
if ($sources.Count -eq 0) { throw "No .cs files found under ForgeTaxTianGong" }

$optimize = if ($Configuration -eq 'Debug') { '/optimize-' } else { '/optimize+' }

$cscArgs = @(
    '/nologo', '/target:library', '/nostdlib+', '/noconfig', '/codepage:65001',
    '/langversion:7.3', $optimize, '/deterministic', '/warn:2',
    ('/out:' + $outDll)
)
foreach ($r in $refs) { $cscArgs += ('/reference:' + $r) }
foreach ($s in $sources) { $cscArgs += ('"' + $s + '"') }

if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }

Write-Host "Compiling $($sources.Count) files -> $outDll"
& $dotnetExe $cscDll @cscArgs
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build FAILED (exit $LASTEXITCODE)" -ForegroundColor Red
    exit $LASTEXITCODE
}
Write-Host ("Build OK: {0} ({1:N0} bytes)" -f $outDll, (Get-Item $outDll).Length) -ForegroundColor Green
exit 0
