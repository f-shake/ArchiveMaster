<#
.SYNOPSIS
Build script for ArchiveMaster.UI.Desktop with flexible options.

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
    Write-Host "Please specify at least one platform: -w, -l, -m or --all"
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
Write-Host "Self-contained: $($s.IsPresent)"
Write-Host "Single-file: $(! $noSingleFile.IsPresent)"
Write-Host "Compression: $($c.IsPresent -and $s.IsPresent)"
Write-Host "========================================`n"

foreach ($target in $targets) {
    # Append _sc suffix if self-contained
    $dirSuffix = if ($s) { "_sc" } else { "" }
    $outputDir = "./Publish/$($target)$dirSuffix"
    Write-Host "Publishing to $outputDir ..."

    dotnet publish ArchiveMaster.UI.Desktop -c Release $selfContained -r $target $publishFile $compression -o $outputDir

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
