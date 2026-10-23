param(
    [string]$AmPath = 'C:\CodeTemp\bin\ArchiveMaster.Cli\net10.0\am.exe'
)

$ErrorActionPreference = 'Stop'

$root = Join-Path $env:TEMP ("am-bug-cli-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root | Out-Null
$am = $AmPath
$results = Join-Path $root 'results.txt'
$jsonLinePattern = '^\s*\{'

function Write-Log([string]$text) {
    $text | Tee-Object -FilePath $results -Append | Out-Null
}

function Invoke-Am {
    param([string[]]$Arguments)
    $output = & $am @Arguments 2>&1
    $code = $LASTEXITCODE
    $lines = @($output | ForEach-Object { "$_" })
    $jsonText = $lines | Where-Object { $_ -match $jsonLinePattern } | Select-Object -Last 1
    $json = $null
    if ($jsonText) {
        try { $json = $jsonText | ConvertFrom-Json } catch { }
    }
    return [pscustomobject]@{
        Code = $code
        Text = ($lines -join "`n")
        Json = $json
    }
}

function Write-Config([string]$name, [hashtable]$config) {
    $path = Join-Path $root $name
    $config | ConvertTo-Json -Depth 10 | Set-Content -Path $path -Encoding utf8
    return $path
}

function Get-Filter {
    return @{
        IsEnabled = $true
        ExcludeFiles = "Thumbs.db`r`nThumb.db`r`ndesktop.ini"
        ExcludeFolders = '$*'
        ExcludePaths = ''
        IncludeFiles = '*'
        IncludeFolders = '*'
        IncludePaths = '*'
        UseRegex = $false
    }
}

try {
    Write-Log "ROOT=$root"
    Write-Log "AM=$am"

    # BUG-01：offlineSync.step2 时间容差为 0 时，相同文件是否仍被判为 Modify。
    $remote = Join-Path $root 'a\remote'
    $localRoot = Join-Path $root 'b'
    $local = Join-Path $localRoot 'remote'
    New-Item -ItemType Directory -Path $remote -Force | Out-Null
    New-Item -ItemType Directory -Path $local -Force | Out-Null
    $t0 = [DateTime]'2020-01-01 10:00:00'
    $sameRemote = Join-Path $remote 'same.txt'
    $sameLocal = Join-Path $local 'same.txt'
    $changedRemote = Join-Path $remote 'changed.txt'
    $changedLocal = Join-Path $local 'changed.txt'
    [IO.File]::WriteAllText($sameRemote, 'same')
    [IO.File]::WriteAllText($sameLocal, 'same')
    [IO.File]::WriteAllText($changedRemote, 'old')
    [IO.File]::WriteAllText($changedLocal, 'changed')
    [IO.File]::SetLastWriteTime($sameRemote, $t0)
    [IO.File]::SetLastWriteTime($sameLocal, $t0)
    [IO.File]::SetLastWriteTime($changedRemote, $t0)
    [IO.File]::SetLastWriteTime($changedLocal, $t0.AddMinutes(1))

    $snapshot = Join-Path $root 'snapshot.os1'
    $step1Config = Write-Config 'step1.json' @{ SyncDirs = @($remote); OutputFile = $snapshot }
    $step1 = Invoke-Am @('run', 'offlineSync.step1', '--config', $step1Config, '--yes', '--json', '--quiet')
    Write-Log "BUG-01 step1 exit=$($step1.Code) snapshot=$([IO.File]::Exists($snapshot))"

    $patch = Join-Path $root 'patch'
    New-Item -ItemType Directory -Path $patch -Force | Out-Null
    $step2Config = Write-Config 'step2.json' @{
        CheckMoveIgnoreFileName = $true
        EnableEncryption = $false
        EncryptionPassword = ''
        ExportMode = 0
        Filter = (Get-Filter)
        LocalDir = $localRoot
        MaxTimeToleranceSecond = 0
        OffsiteSnapshot = $snapshot
        PatchDir = $patch
    }
    $step2 = Invoke-Am @('plan', 'offlineSync.step2', '--config', $step2Config, '--json', '--quiet')
    $sameItem = $step2.Json.Items | Where-Object { $_.Name -eq 'same.txt' } | Select-Object -First 1
    $sameUpdate = if ($sameItem) { $sameItem.Extra.UpdateType } else { '(none)' }
    Write-Log "BUG-01 step2 exit=$($step2.Code) same.txt UpdateType=$sameUpdate"
    Write-Log "BUG-01 items=$($step2.Json.Items.Count)"

    # BUG-05：dirStructureSync 的 TemplateDir 缺失时，CLI 是否在 Check 阶段给出模板目录错误。
    $source = Join-Path $root 'sync-source'
    New-Item -ItemType Directory -Path (Join-Path $source 'sub') -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $source 'sub\a.txt'), 'x')
    $missingTemplate = Join-Path $root 'missing-template'
    $dirSyncConfig = Write-Config 'dirsync.json' @{
        TemplateDir = $missingTemplate
        SourceDir = $source
        TargetDir = (Join-Path $root 'sync-target')
        CompareName = $true
        CompareTime = $false
        CompareLength = $false
        Copy = $true
        MaxTimeToleranceSecond = 0
    }
    $dirSync = Invoke-Am @('plan', 'fileTools.dirStructureSync', '--config', $dirSyncConfig, '--json', '--quiet')
    Write-Log "BUG-05 exit=$($dirSync.Code) error=$($dirSync.Json.Error)"

    # BUG-06：linkDeduplication 在 AllowDifferentTime=false 时是否仍处理时间不同的文件。
    $linkDir = Join-Path $root 'link'
    New-Item -ItemType Directory -Path $linkDir -Force | Out-Null
    $linkA = Join-Path $linkDir 'a.txt'
    $linkB = Join-Path $linkDir 'b.txt'
    [IO.File]::WriteAllText($linkA, 'same content')
    [IO.File]::WriteAllText($linkB, 'same content')
    [IO.File]::SetLastWriteTime($linkA, $t0)
    [IO.File]::SetLastWriteTime($linkB, $t0.AddHours(1))
    $linkConfig = Write-Config 'link.json' @{
        Dir = $linkDir
        Filter = (Get-Filter)
        AllowDifferentTime = $false
        HashType = 2
    }
    $linkPlan = Invoke-Am @('plan', 'fileTools.linkDeduplication', '--config', $linkConfig, '--json', '--quiet')
    $linkRun = Invoke-Am @('run', 'fileTools.linkDeduplication', '--config', $linkConfig, '--yes', '--json', '--quiet')
    $hardLinkLines = @(& fsutil hardlink list $linkA 2>$null)
    Write-Log "BUG-06 planExit=$($linkPlan.Code) runExit=$($linkRun.Code) hardLinkEntries=$($hardLinkLines.Count)"
    Write-Log "BUG-06 planItems=$($linkPlan.Json.Items.Count) runItems=$($linkRun.Json.Items.Count)"

    # BUG-08：readabilityScanner 空文件最终提示是什么。
    $readDir = Join-Path $root 'read'
    New-Item -ItemType Directory -Path $readDir -Force | Out-Null
    [IO.File]::WriteAllBytes((Join-Path $readDir 'empty.bin'), [byte[]]::new(0))
    $readConfig = Write-Config 'read.json' @{ Dir = $readDir; Filter = (Get-Filter) }
    $read = Invoke-Am @('run', 'fileTools.fileReadabilityScanner', '--config', $readConfig, '--yes', '--json', '--quiet')
    $readItem = $read.Json.Items | Select-Object -First 1
    Write-Log "BUG-08 exit=$($read.Code) message=$($readItem.Message) status=$($readItem.Status)"
}
catch {
    Write-Log "EXCEPTION: $($_.Exception.ToString())"
}
finally {
    Write-Log "DONE root=$root"
}
