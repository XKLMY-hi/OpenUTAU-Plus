# OpenUTAU Plus 安装包构建流水线
# 用法: powershell -ExecutionPolicy Bypass -File packaging\build-installer.ps1
# 前置: Inno Setup 6 (winget install JRSoftware.InnoSetup) + gh CLI（可选，仅发布用）
# 每步检查 $LASTEXITCODE，失败即抛。打包前清空输出目录并重新编译主程序（不用旧产物）。

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $PSScriptRoot "publish\win-x64"
$distDir = Join-Path $PSScriptRoot "dist"
$vcRedist = Join-Path $PSScriptRoot "vc_redist.x64.exe"
$islFile = Join-Path $PSScriptRoot "ChineseSimplified.isl"
$issFile = Join-Path $PSScriptRoot "OpenUtauPlus.iss"

function Assert-LastExit {
    if ($LASTEXITCODE -ne 0) { throw "上一步失败 (exit $LASTEXITCODE)" }
}

Write-Host "=== OpenUTAU Plus 安装包构建 ===" -ForegroundColor Cyan

# ── 1. 版本提取（单一事实源） ─────────────────────────────
$csproj = Get-Content (Join-Path $root "OpenUtau\OpenUtau.csproj") -Raw
$baseVer = [regex]::Match($csproj, '<Version>([0-9.]+)</Version>').Groups[1].Value
$plusInfo = Get-Content (Join-Path $root "OpenUtau.Core\PlusInfo.cs") -Raw
$plusVer = [regex]::Match($plusInfo, 'PlusVersion\s*=\s*"([0-9.]+)"').Groups[1].Value
$tag = "v$baseVer-plus.$plusVer"
Write-Host "版本: $baseVer (Plus $plusVer)  tag: $tag"

# ── 2. 清空输出目录（保证干净构建，不用旧产物） ───────────
Write-Host "清空输出目录..." -ForegroundColor Yellow
foreach ($dir in @($publishDir, $distDir)) {
    if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
}
New-Item -ItemType Directory -Force -Path $publishDir, $distDir | Out-Null

# ── 3. 补 vst_probe 缺口（先构建 VstProbe Release） ───────
Write-Host "构建 VstProbe (Release)..." -ForegroundColor Yellow
Push-Location $root
dotnet build VstProbe -c Release | Out-Null
Assert-LastExit
Pop-Location

# ── 4. 重新编译主程序并发布（干净编译，不用旧产物） ───────
Write-Host "publish OpenUtau (Release win-x64, 重新编译)..." -ForegroundColor Yellow
Push-Location $root
dotnet publish OpenUtau -c Release -r win-x64 --self-contained true -o $publishDir | Out-Null
Assert-LastExit
Pop-Location

# ── 5. 补 vst_probe.* 三件套到发布目录（CopyVstProbe target 只写构建目录） ──
Write-Host "复制 vst_probe.* ..."
Copy-Item (Join-Path $root "VstProbe\bin\Release\net8.0\vst_probe.*") $publishDir

# ── 6. 补 DirectML.dll（GPU 推理；缺失仅 warning 降级 CPU） ──
$directMl = Get-ChildItem "$env:USERPROFILE\.nuget\packages\microsoft.ai.directml\*\bin\x64-win\DirectML.dll" -ErrorAction SilentlyContinue |
    Sort-Object { [version]$_.Directory.Parent.Parent.Name } | Select-Object -Last 1
if ($directMl) {
    Copy-Item $directMl.FullName $publishDir
    Write-Host "DirectML.dll: $($directMl.FullName)"
} else {
    Write-Warning "DirectML.dll 未找到——GPU 推理不可用，运行时回退 CPU"
}

# ── 7. 裁剪（PDB 101MB 等） ───────────────────────────────
Write-Host "裁剪产物..."
Remove-Item -ErrorAction SilentlyContinue `
    "$publishDir\*.pdb", "$publishDir\createdump.exe",
    "$publishDir\Microsoft.DiaSymReader.Native.amd64.dll",
    "$publishDir\onnxruntime.lib", "$publishDir\*.dylib"

# ── 8. 写 installed.txt（已安装模式标记——数据落 Documents\OpenUtau Plus） ──
Set-Content -Path "$publishDir\installed.txt" -Value "yes" -NoNewline -Encoding ascii
Write-Host "installed.txt 已写入"

# ── 9. vc_redist.x64.exe（缺失则下载 + 签名校验） ─────────
if (-not (Test-Path $vcRedist)) {
    Write-Host "下载 vc_redist.x64.exe ..."
    Invoke-WebRequest "https://aka.ms/vs/17/release/vc_redist.x64.exe" -OutFile $vcRedist
}
$sig = Get-AuthenticodeSignature $vcRedist
if ($sig.Status -ne "Valid" -or $sig.SignerCertificate.Subject -notmatch "Microsoft") {
    throw "vc_redist 签名校验失败: $($sig.Status)"
}
Write-Host "vc_redist SHA-256: $((Get-FileHash $vcRedist).Hash)"

# ── 10. 确保中文语言文件 ───────────────────────────────────
if (-not (Test-Path $islFile)) {
    throw "缺少 packaging\ChineseSimplified.isl——请先从 jrsoftware/issrc 仓库获取"
}

# ── 11. ISCC 编译安装包 ────────────────────────────────────
# winget 用户级安装可能在 AppData\Local\Programs，也兼容 Program Files 位置
$isccCandidates = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
)
$iscc = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw "未找到 ISCC.exe——请安装 Inno Setup 6" }
Write-Host "ISCC 编译安装包..." -ForegroundColor Yellow
& $iscc $issFile "/DMyAppVersion=$baseVer" /Qp
Assert-LastExit

$setup = Get-ChildItem $distDir -Filter "*.exe" | Select-Object -First 1
Write-Host ""
Write-Host "=== 完成 ===" -ForegroundColor Green
Write-Host "安装包: $($setup.FullName)  ($([math]::Round($setup.Length / 1MB, 1)) MB)"
Write-Host "发布 tag: $tag"
