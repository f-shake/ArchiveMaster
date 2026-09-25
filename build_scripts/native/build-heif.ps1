<#
.SYNOPSIS
构建照片瘦身输出 HEIC 所需的原生库：libheif + x265（编码器）+ libde265（解码器）。

.DESCRIPTION
产物放到 ArchiveMaster.UI.Desktop/native/<RID>/，由 ArchiveMaster.UI.Desktop.csproj 随包复制到输出目录，
运行时由 ArchiveMaster.Helpers.HeifEncoder 加载（见该类的注释，尤其是 EXIF 前缀那一段）。

为什么需要自己构建：Magick.NET 自带的 libheif 没有编入 HEVC 编码器（实测 Heic Read=True Write=False），
而 NuGet 上可自由再分发的 .NET 包都刻意不含 x265（GPL）。因此本项目自行构建并在 GPLv3 下随包分发。

前置条件（本机 2026-09 验证通过的组合）：
  - Visual Studio 的 MSVC 工具链（本机为 VS18 / MSVC 14.51）
  - CMake 3.x        ← 注意：VS 自带的 CMake 4.x 无法配置 x265 4.1（其 CMakeLists 设了已废弃的 OLD 策略）
                        本机用 `python -m pip install "cmake<4"` 得到的 3.31.10
  - Ninja            ← VS 自带（Common7\IDE\CommonExtensions\Microsoft\CMake\Ninja\ninja.exe）
  - nasm             ← 可选，装了就用 -EnableAssembly 显著加快编码速度。
                        本机的装法（无需管理员，解压即用）：
                          curl.exe -sL https://www.nasm.us/pub/nasm/releasebuilds/3.02/win64/nasm-3.02-win64.zip -o $env:TEMP\nasm.zip
                          Expand-Archive $env:TEMP\nasm.zip "$env:LOCALAPPDATA\Programs\nasm-3.02"
                        然后把 `%LOCALAPPDATA%\Programs\nasm-3.02\nasm-3.02` 加进 PATH
                        （注意：winget 的 NASM.NASM 安装包在本机会报"已成功安装"但实际不落盘，故用上面的 zip 方式）

.PARAMETER WorkDir
源码与中间构建目录，默认 $env:TEMP\archiveMaster-heif-build。

.PARAMETER OutDir
原生库输出目录，默认仓库内 ArchiveMaster.UI.Desktop\native\win-x64。

.PARAMETER EnableAssembly
是否启用汇编优化（需 nasm）。默认关闭：未安装 nasm 时保持关闭即可（编码较慢但结果一致），
装了 nasm 后加上本开关可显著提速。

.EXAMPLE
pwsh build-heif.ps1
pwsh build-heif.ps1 -EnableAssembly -WorkDir D:\build\heif
#>
[CmdletBinding()]
param(
    [string]$WorkDir = (Join-Path $env:TEMP 'archiveMaster-heif-build'),
    [string]$OutDir = (Join-Path $PSScriptRoot '..\..\ArchiveMaster.UI.Desktop\native\win-x64'),
    [switch]$EnableAssembly,
    # x265 按 tag 检出：Bitbucket 不允许按裸 commit 抓取，且 x265 的版本识别要读 git tag；
    # 检出后用 X265Commit 断言实际 commit，兼顾可复现（与 THIRD-PARTY-NOTICES.txt 一致）
    [string]$X265Tag = '4.1',
    [string]$X265Commit = '1d117bed4747758b51bd2c124d738527e30392cb',
    [string]$LibheifRef = '5c7b41f3cc097447dd3c700cc9ec7d94fbb59eec',   # 1.23.5（master 上的 commit）
    [string]$Libde265Ref = '78bd19905b90a95c2ddbe109b2554ea01f65acd7'    # 1.1.3（master 上的 commit）
)

$ErrorActionPreference = 'Stop'
$OutDir = [System.IO.Path]::GetFullPath($OutDir)
New-Item -ItemType Directory -Force $WorkDir, $OutDir | Out-Null

function Find-CMake {
    # 优先用 pip 装的 CMake 3.x（VS 自带的 4.x 无法配置 x265）
    $candidates = @(
        (Join-Path $env:LOCALAPPDATA 'Programs\Python\Python310\Scripts\cmake.exe'),
        'cmake'
    )
    foreach ($c in $candidates) {
        $exe = if (Test-Path $c) { $c } else { (Get-Command $c -ErrorAction SilentlyContinue)?.Source }
        if ($exe) {
            $version = (& $exe --version | Select-Object -First 1)
            if ($version -match 'version (\d+)\.' -and [int]$Matches[1] -lt 4) {
                Write-Host "使用 $version -> $exe"
                return $exe
            }
            Write-Warning "$version 版本过高（x265 4.1 需要 CMake 3.x）：$exe"
        }
    }
    throw '找不到 CMake 3.x。安装方式：python -m pip install "cmake<4"'
}

function Find-Ninja {
    $ninja = Get-ChildItem 'C:\Program Files\Microsoft Visual Studio\*\*\Common7\IDE\CommonExtensions\Microsoft\CMake\Ninja\ninja.exe' -ErrorAction SilentlyContinue |
        Select-Object -First 1 -ExpandProperty FullName
    if (-not $ninja) { $ninja = (Get-Command ninja -ErrorAction SilentlyContinue)?.Source }
    if (-not $ninja) { throw '找不到 ninja.exe' }
    return $ninja
}

function Import-VcVars {
    $vcvars = Get-ChildItem 'C:\Program Files\Microsoft Visual Studio\*\*\VC\Auxiliary\Build\vcvars64.bat' -ErrorAction SilentlyContinue |
        Select-Object -First 1 -ExpandProperty FullName
    if (-not $vcvars) { throw '找不到 vcvars64.bat，请确认已安装 VS 的 C++ 生成工具' }
    cmd /c "`"$vcvars`" && set" 2>$null | ForEach-Object {
        if ($_ -match '^([^=]+)=(.*)$') { Set-Item -Path "env:$($Matches[1])" -Value $Matches[2] -ErrorAction SilentlyContinue }
    }
}

function Get-Source([string]$name, [string]$url, [string]$ref, [string]$expectCommit) {
    $dir = Join-Path $WorkDir $name
    if (Test-Path (Join-Path $dir '.git')) {
        Write-Host "[$name] 已存在，跳过克隆"
    }
    else {
        # 用完整克隆而非 --depth 1：x265 的版本识别要读 git tag，
        # 而浅克隆既没有 tag，也无法按任意 commit 检出（部分服务器还不允许按裸 SHA 抓取）
        Write-Host "[$name] 克隆 $url"
        git clone $url $dir
        if ($LASTEXITCODE -ne 0) { throw "[$name] 克隆失败" }
    }
    Push-Location $dir
    # 兼容旧版本脚本留下的浅克隆：x265 的版本识别依赖 tag，浅克隆会让它算出 "unknown" 而使配置失败
    if ((git rev-parse --is-shallow-repository).Trim() -eq 'true') {
        Write-Host "[$name] 检测到浅克隆，补齐历史与 tag"
        git fetch --unshallow --tags
        if ($LASTEXITCODE -ne 0) { throw "[$name] 补齐浅克隆失败，请删除 $dir 后重试" }
    }
    git checkout --detach $ref
    if ($LASTEXITCODE -ne 0) { throw "[$name] 检出 $ref 失败（需是仓库中存在的 tag 或 commit）" }
    $head = (git rev-parse HEAD).Trim()
    if ($expectCommit -and $head -ne $expectCommit) {
        throw "[$name] commit 不符：期望 $expectCommit，实际 $head（上游 tag 可能被移动，请核对后更新脚本）"
    }
    Write-Host "[$name] 版本: $head"
    Pop-Location
    return $dir
}

$cmake = Find-CMake
$ninja = Find-Ninja
Import-VcVars

$crt = '-DCMAKE_MSVC_RUNTIME_LIBRARY=MultiThreaded'   # 静态 CRT：用户机器无需 VC++ 运行库
# 注意：x265 不认 CMAKE_MSVC_RUNTIME_LIBRARY（它自己写死 /MD），要用它自带的 STATIC_LINK_CRT 开关；
# 否则 libx265.dll 会依赖 VCRUNTIME140/UCRT，用户机器没装 VC++ 运行库时 HEIC 就用不了
$crtX265 = '-DSTATIC_LINK_CRT=ON'

# 构建目录与安装前缀都带上"变体"后缀：改了开关（汇编/静态 CRT）就自动换一套目录，
# 避免旧 CMake 缓存/旧安装产物混进来造成难查的配置失败
$variant = if ($EnableAssembly) { 'asm' } else { 'noasm' }
$prefix = Join-Path $WorkDir "install-$variant"
$bX265 = Join-Path $WorkDir "b-x265-$variant"
$bDe265 = Join-Path $WorkDir "b-libde265-$variant"
$bHeif = Join-Path $WorkDir "b-libheif-$variant"
$heifOut = Join-Path $WorkDir "out-libheif-$variant"
$asmOpt = if ($EnableAssembly) { '-DENABLE_ASSEMBLY=ON' } else { '-DENABLE_ASSEMBLY=OFF' }
if (-not $EnableAssembly) { Write-Warning '未启用汇编优化：x265 编码会明显变慢（未安装 nasm 时的默认选择）' }

# ---------------- 1. x265（HEVC 编码器，GPL-2.0-or-later）----------------
$x265Src = Get-Source 'x265' 'https://bitbucket.org/multicoreware/x265_git.git' $X265Tag $X265Commit
Write-Host '[x265] 配置与构建'
& $cmake -S (Join-Path $x265Src 'source') -B $bX265 -G Ninja `
    "-DCMAKE_MAKE_PROGRAM=$ninja" -DCMAKE_BUILD_TYPE=Release "-DCMAKE_INSTALL_PREFIX=$prefix" `
    $asmOpt -DENABLE_SHARED=ON -DENABLE_CLI=OFF -DHIGH_BIT_DEPTH=OFF $crt $crtX265
if ($LASTEXITCODE -ne 0) { throw 'x265 配置失败' }
& $cmake --build $bX265 --target install
if ($LASTEXITCODE -ne 0) { throw 'x265 构建失败' }

# ---------------- 2. libde265（HEVC 解码器，LGPL-3.0）----------------
$de265Src = Get-Source 'libde265' 'https://github.com/strukturag/libde265.git' $Libde265Ref $Libde265Ref
Write-Host '[libde265] 配置与构建'
& $cmake -S $de265Src -B $bDe265 -G Ninja `
    "-DCMAKE_MAKE_PROGRAM=$ninja" -DCMAKE_BUILD_TYPE=Release "-DCMAKE_INSTALL_PREFIX=$prefix" `
    -DBUILD_SHARED_LIBS=ON -DENABLE_SDL=OFF $crt
if ($LASTEXITCODE -ne 0) { throw 'libde265 配置失败' }
& $cmake --build $bDe265 --target install
if ($LASTEXITCODE -ne 0) { throw 'libde265 构建失败' }

# ---------------- 3. libheif（HEIF 容器，LGPL-3.0）----------------
$heifSrc = Get-Source 'libheif' 'https://github.com/strukturag/libheif.git' $LibheifRef $LibheifRef
Write-Host '[libheif] 配置与构建'
# WITH_EXAMPLES=OFF：libheif 的示例程序（heif-enc）在 MSVC 下有上游链接错误，本项目用不到
& $cmake -S $heifSrc -B $bHeif -G Ninja `
    "-DCMAKE_MAKE_PROGRAM=$ninja" -DCMAKE_BUILD_TYPE=Release "-DCMAKE_INSTALL_PREFIX=$heifOut" `
    "-DCMAKE_PREFIX_PATH=$prefix" -DBUILD_SHARED_LIBS=ON `
    -DWITH_X265=ON -DWITH_LIBDE265=ON -DWITH_KVAZAAR=OFF `
    -DWITH_AOM_ENCODER=OFF -DWITH_AOM_DECODER=OFF -DWITH_EXAMPLES=OFF -DBUILD_TESTING=OFF $crt
if ($LASTEXITCODE -ne 0) { throw 'libheif 配置失败（配置日志里应能看到 "x265 HEVC encoder : + built-in"）' }
& $cmake --build $bHeif --target install
if ($LASTEXITCODE -ne 0) { throw 'libheif 构建失败' }

# ---------------- 4. 收集产物 ----------------
# 注意 libheif 在 Windows 上的产物名是 heif.dll，而 HeifEncoder 按 libheif.dll 查找，故重命名
$artifacts = @(
    @{ From = (Join-Path $heifOut 'bin\heif.dll'); To = 'libheif.dll' },
    @{ From = (Join-Path $prefix 'bin\libx265.dll'); To = 'libx265.dll' },
    @{ From = (Join-Path $prefix 'bin\libde265.dll'); To = 'libde265.dll' }
)
foreach ($a in $artifacts) {
    if (-not (Test-Path $a.From)) { throw "缺少产物 $($a.From)" }
    Copy-Item $a.From (Join-Path $OutDir $a.To) -Force
}

Write-Host ''
Write-Host "完成，产物已放入 $OutDir" -ForegroundColor Green
Get-ChildItem $OutDir -Filter *.dll | ForEach-Object { "  $($_.Name)  ($([math]::Round($_.Length / 1KB, 1)) KB)" }
Write-Host '提醒：libheif/libde265 为 LGPL-3.0、x265 为 GPL-2.0-or-later，分发时需随包提供 THIRD-PARTY-NOTICES.txt'
