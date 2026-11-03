param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('master', 'develop')]
    [string]$Branch
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$project = $root
if ($Branch -eq 'develop') {
    if ((& git -C $root branch --show-current).Trim() -ne 'develop') {
        throw 'The main project must be on develop before building develop.'
    }
    if (& git -C $root status --porcelain) {
        throw 'Commit or set aside project changes before making a labeled develop build.'
    }
} else {
    $project = Join-Path $root 'Builds/Worktrees/master'
    $marker = Join-Path $project 'Builds/.branch-build-managed'
    if (-not (Test-Path -LiteralPath $project)) {
        & git -C $root worktree add --detach $project master
        if ($LASTEXITCODE -ne 0) { throw 'Could not create master worktree.' }
        New-Item -ItemType Directory -Path (Join-Path $project 'Builds') -Force | Out-Null
        'Managed by tools/build_branch.ps1' | Set-Content -LiteralPath $marker
    }
    if (-not (Test-Path -LiteralPath $marker)) {
        throw 'Existing master worktree was not created by this script.'
    }
    $unityGenerated = @(
        'Assets/Settings/PrototypeURP.asset',
        'Assets/UniversalRenderPipelineGlobalSettings.asset',
        'ProjectSettings/GraphicsSettings.asset',
        'ProjectSettings/Packages/com.unity.testtools.codecoverage/Settings.json',
        'ProjectSettings/ProjectSettings.asset',
        'ProjectSettings/ShaderGraphSettings.asset'
    )
    $dirty = @(& git -C $project status --porcelain=v1 -uno | ForEach-Object { $_.Substring(3) })
    $unexpected = @($dirty | Where-Object { $_ -notin $unityGenerated })
    $untracked = @(& git -C $project ls-files --others --exclude-standard)
    if ($unexpected.Count -gt 0 -or $untracked.Count -gt 0) {
        throw 'Master worktree has non-generated changes; inspect it before building.'
    }
    if ($dirty.Count -gt 0) {
        # Unity writes LF; this repository's Windows autocrlf checkout otherwise
        # leaves the restored files appearing modified even when contents match.
        & git -C $project -c core.autocrlf=false restore -- $dirty
        if ($LASTEXITCODE -ne 0) { throw 'Could not restore Unity-generated master settings.' }
    }
    & git -C $project switch --detach master | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not update master worktree.' }
}

$commit = (& git -C $project rev-parse --short=7 HEAD).Trim()
$versionLine = Get-Content -LiteralPath (Join-Path $project 'ProjectSettings/ProjectVersion.txt') |
    Where-Object { $_ -match '^m_EditorVersion:' } | Select-Object -First 1
if ($versionLine -notmatch '^m_EditorVersion:\s*(\S+)') { throw 'Unity editor version was not found.' }
$version = $Matches[1]
$unity = Join-Path 'C:/Program Files/Unity/Hub/Editor' "$version/Editor/Unity.exe"
if (-not (Test-Path -LiteralPath $unity)) { throw "Unity $version is not installed at $unity" }

$branchRoot = Join-Path $root "Builds/$Branch"
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$destination = Join-Path $branchRoot "$stamp-$commit"
if (Test-Path -LiteralPath $destination) { throw "Build destination already exists: $destination" }
New-Item -ItemType Directory -Path $destination -Force | Out-Null
$log = Join-Path $destination 'unity-build.log'
$process = Start-Process -FilePath $unity -ArgumentList @(
    '-batchmode', '-nographics', '-quit', '-projectPath', $project,
    '-executeMethod', 'ChainboundRaid.Editor.PrototypeBuild.BuildClientAndServer',
    '-logFile', $log
) -WindowStyle Hidden -Wait -PassThru
if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $log) -or
    -not (((Get-Content -LiteralPath $log -Tail 12) -join "`n") -match 'Exiting batchmode successfully')) {
    throw "Unity build failed. See $log"
}

$clientSource = Join-Path $project 'Builds/Windows'
$serverSource = Join-Path $project 'Builds/Server'
if (-not (Test-Path -LiteralPath (Join-Path $clientSource 'ChainboundRaid.exe')) -or
    -not (Test-Path -LiteralPath (Join-Path $serverSource 'ChainboundRaidServer.exe'))) {
    throw 'Build completed without the expected client and server executables.'
}
Copy-Item -LiteralPath $clientSource -Destination (Join-Path $destination 'Windows') -Recurse
Copy-Item -LiteralPath $serverSource -Destination (Join-Path $destination 'Server') -Recurse
@(
    "Branch: $Branch"
    "Commit: $(& git -C $project rev-parse HEAD)"
    "Unity: $version"
    "Built: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz')"
    "Client: Windows/ChainboundRaid.exe"
    "Server: Server/ChainboundRaidServer.exe"
    "Default mode: $(if ($Branch -eq 'develop') { 'solo development' } else { 'two-player multiplayer' })"
) | Set-Content -LiteralPath (Join-Path $destination 'BUILD_INFO.txt') -Encoding UTF8
$destination | Set-Content -LiteralPath (Join-Path $branchRoot 'LATEST.txt') -Encoding UTF8
Write-Output "$Branch build: $destination"
