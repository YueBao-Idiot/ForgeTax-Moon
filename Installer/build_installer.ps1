# build_installer.ps1 - 编译 wins 安装器（单 exe，内嵌修改器 DLL，只用 Windows 自带的 .NET Framework）
param([string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'

$sourceDir = $PSScriptRoot
$gameRoot = (Resolve-Path (Join-Path $sourceDir '..\..')).Path
$modDll = Join-Path $gameRoot 'Mods\MoonCheatMod.dll'
$distDir = Join-Path $sourceDir '..\dist'
$outExe = Join-Path $distDir 'MoonInstaller.exe'

if (-not (Test-Path $modDll)) { throw "没有找到修改器 DLL：$modDll（先运行 Source\build.ps1）" }

$fw = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
if (-not (Test-Path $fw)) { throw "找不到 .NET Framework 4.x：$fw" }
$dotnet = (Get-Command dotnet -ErrorAction Stop).Source
$dotnetHome = Split-Path $dotnet
$sdkVersion = (& $dotnet --version).Trim()
$cscDll = Join-Path $dotnetHome ("sdk\$sdkVersion\Roslyn\bincore\csc.dll")
if (-not (Test-Path $cscDll)) { throw "找不到 csc.dll：$cscDll" }

$refs = @(
    (Join-Path $fw 'mscorlib.dll'),
    (Join-Path $fw 'System.dll'),
    (Join-Path $fw 'System.Core.dll'),
    (Join-Path $fw 'System.Windows.Forms.dll'),
    (Join-Path $fw 'System.Drawing.dll'),
    (Join-Path $fw 'System.IO.Compression.dll'),
    (Join-Path $fw 'System.IO.Compression.FileSystem.dll')
) | Where-Object { Test-Path $_ }

if (-not (Test-Path $distDir)) { New-Item -ItemType Directory -Path $distDir | Out-Null }

$args = @(
    '/nologo', '/target:winexe', '/nostdlib+', '/noconfig', '/codepage:65001',
    '/langversion:7.3', '/optimize+', '/deterministic', '/platform:anycpu',
    ('/out:' + $outExe),
    ('/resource:' + $modDll + ',MoonCheatMod.dll')
) + ($refs | ForEach-Object { '/reference:' + $_ }) + @(Join-Path $sourceDir 'Installer.cs')

& $dotnet $cscDll @args
if ($LASTEXITCODE -ne 0) { throw "安装器编译失败（exit $LASTEXITCODE）" }

# --- 代码签名（发布者：YueBao_）---
try {
    $pfx = Join-Path $sourceDir 'cert\YueBao_.pfx'
    $cert = Get-ChildItem Cert:\CurrentUser\My -ErrorAction SilentlyContinue |
        Where-Object { $_.Subject -like '*CN=YueBao_*' } | Select-Object -First 1
    if (-not $cert -and (Test-Path $pfx)) {
        $pwd = ConvertTo-SecureString -String 'yuebao-moon-installer' -Force -AsPlainText
        Import-PfxCertificate -FilePath $pfx -Password $pwd -CertStoreLocation Cert:\CurrentUser\My | Out-Null
        $cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -like '*CN=YueBao_*' } | Select-Object -First 1
    }
    if ($cert) {
        $sig = Set-AuthenticodeSignature -FilePath $outExe -Certificate $cert -HashAlgorithm SHA256
        Write-Host ("已签名：{0}（状态 {1}）" -f $sig.SignerCertificate.Subject, $sig.Status)
    } else {
        Write-Host "未找到签名证书，跳过签名（Source\installer\cert\YueBao_.pfx 可重新导入）"
    }
} catch {
    Write-Host ("签名失败（不影响使用）：" + $_.Exception.Message)
}

Write-Host ("Build OK: {0} ({1:N0} bytes)" -f $outExe, (Get-Item $outExe).Length)
