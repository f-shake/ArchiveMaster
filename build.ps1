<#
.SYNOPSIS
Build script for ArchiveMaster desktop and CLI with flexible options.

.PARAMETER w
Build for Windows (win-x64)

.PARAMETER l
Build for Linux (linux-x64)

.PARAMETER m
Build for macOS (osx-x64)

.PARAMETER all
Build for all platforms

.PARAMETER s
Enable self-contained build

.PARAMETER c
Enable compression (EnableCompressionInSingleFile)

.PARAMETER noSingleFile
Disable single-file publish (default is enabled)
#>

param (
    [switch]$w,
    [switch]$l,
    [switch]$m,
    [switch]$all,
    [switch]$s,
    [switch]$c,
    [switch]$noSingleFile
)

# Determine target platforms
$targets = @()

if ($all) {
    $targets = @("win-x64", "linux-x64", "osx-x64")
} else {
    if ($w) { $targets += "win-x64" }
    if ($l) { $targets += "linux-x64" }
    if ($m) { $targets += "osx-x64" }
}

if ($targets.Count -eq 0) {
    Write-Host "Please specify at least one platform: -w, -l, -m or -all."
    Write-Host "can specify -s to enable self-contained build."
    Write-Host "can specify -c to enable compression."
    Write-Host "can specify -noSingleFile to disable single-file publish."
    exit 1
}

# Determine publish parameters
$publishFile = if ($noSingleFile) { "" } else { "-p:PublishSingleFile=true" }
$selfContained = if ($s) { "--self-contained" } else { "--no-self-contained" }

# Auto-disable compression if framework-dependent (to avoid NETSDK1176)
if (-not $s -and $c) {
    Write-Host "Framework-dependent single-file cannot use compression. Compression will be disabled."
    $compression = "-p:EnableCompressionInSingleFile=false"
} else {
    $compression = if ($c) { "-p:EnableCompressionInSingleFile=true" } else { "-p:EnableCompressionInSingleFile=false" }
}

# Print current configuration
Write-Host "========== Build Configuration =========="
Write-Host "Targets: $($targets -join ', ')"
Write-Host "Outputs: GUI + CLI"
Write-Host "Self-contained: $($s.IsPresent)"
Write-Host "Single-file: $(! $noSingleFile.IsPresent)"
Write-Host "Compression: $($c.IsPresent -and $s.IsPresent)"
Write-Host "========================================`n"

# 任一发布步骤失败都要立即中止：否则脚本仍会打印成功并以 0 退出，
# 调用方（CI、打包脚本）会把上一次留在输出目录里的旧产物当成新产物。
function Invoke-Publish {
    param (
        [string]$Title,
        [string]$Project,
        [string]$Target,
        [string]$OutDir
    )

    Write-Host "Publishing $Title to $OutDir ..."
    dotnet publish $Project -c Release $selfContained -r $Target $publishFile $compression -o $OutDir
    # dotnet 不存在时上面的命令根本没有执行，$LASTEXITCODE 仍是 $null；按失败处理，
    # 否则 `exit $null` 会得到退出码 0，调用方会把失败当成功。
    $exitCode = if ($null -eq $LASTEXITCODE) { 1 } else { $LASTEXITCODE }
    if ($exitCode -ne 0) {
        Write-Host "Publishing $Title to $OutDir failed (exit code $exitCode)." -ForegroundColor Red
        exit $exitCode
    }
}

foreach ($target in $targets) {
    # Append _sc suffix if self-contained
    $dirSuffix = if ($s) { "_sc" } else { "" }
    $outputDir = "./Publish/$($target)$dirSuffix"

    # GUI 与 CLI 发布到同一个目录：am（am.exe）与桌面程序放在一起，
    # 各自需要的依赖与原生库由各自的项目复制进该目录。
    Invoke-Publish -Title "GUI" -Project "ArchiveMaster.UI.Desktop" -Target $target -OutDir $outputDir
    Invoke-Publish -Title "CLI" -Project "ArchiveMaster.Cli" -Target $target -OutDir $outputDir

    # Windows: rename executable if single-file is enabled
    if ($target -eq "win-x64" -and -not $noSingleFile) {
        $exePath = Join-Path $outputDir "ArchiveMaster.UI.Desktop.exe"
        if (Test-Path $exePath) {
            $exeName = if ($s) { "ArchiveMaster.exe" } else { "ArchiveMaster.exe" }
            Move-Item -Force $exePath (Join-Path $outputDir $exeName)
        }
    }

    Write-Host "Publishing to $outputDir completed.`n"
}

Write-Host "All builds completed successfully!"
