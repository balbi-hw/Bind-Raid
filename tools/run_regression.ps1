param([int]$TimeoutSeconds = 120, [string]$BuildRoot)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$artifactRoot = if ($BuildRoot) { (Resolve-Path -LiteralPath $BuildRoot).Path } else { Join-Path $projectRoot 'Builds' }
$serverExe = Join-Path $artifactRoot 'Server\ChainboundRaidServer.exe'
$clientExe = Join-Path $artifactRoot 'Windows\ChainboundRaid.exe'
if (-not (Test-Path -LiteralPath $serverExe) -or -not (Test-Path -LiteralPath $clientExe)) {
    throw 'Build the development client and dedicated server before running regression.'
}

$runDirectory = Join-Path $projectRoot ('Builds\Regression\' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null
$cases = @(
    @{ Name = 'tutorial'; ServerFlag = '-raidTestTutorial'; ClientFlag = '-raidAutoReady'; Expected = 'TUTORIAL_SMOKE: completed all five steps'; ClientExpected = 'RAID_SESSION: Tutorial -> Combat' },
    @{ Name = 'boss'; ServerFlag = '-raidTestBoss'; ClientFlag = '-raidAutoFlow'; Expected = 'BOSS_SMOKE: facing=True.*phaseTwo=True'; ClientExpected = 'RAID_TEST: combat entered' },
    @{ Name = 'revive'; ServerFlag = '-raidTestRevive'; ClientFlag = '-raidAutoFlow'; Expected = 'RAID_TEST: revived=True count=2'; ClientExpected = 'RAID_TEST: combat entered'; FailurePattern = 'RAID_TEST: revived=False' },
    @{ Name = 'victory'; ServerFlag = '-raidTestWin'; ClientFlag = '-raidAutoFlow'; Expected = 'RAID_SESSION: Combat -> Result reason=BossDefeated'; ClientExpected = 'RAID_SESSION: Combat -> Result reason=BossDefeated' }
)

$results = @()
foreach ($case in $cases) {
    $serverLog = Join-Path $runDirectory ($case.Name + '-server.log')
    $clientOneLog = Join-Path $runDirectory ($case.Name + '-client1.log')
    $clientTwoLog = Join-Path $runDirectory ($case.Name + '-client2.log')
    $processes = @()
    $passed = $false
    $reason = ''
    try {
        $processes += Start-Process -FilePath $serverExe -ArgumentList @('-batchmode', '-nographics', $case.ServerFlag, '-logFile', $serverLog) -WindowStyle Hidden -PassThru
        Start-Sleep -Seconds 2
        $processes += Start-Process -FilePath $clientExe -ArgumentList @('-batchmode', '-nographics', '-raidClient', $case.ClientFlag, '-logFile', $clientOneLog) -WindowStyle Hidden -PassThru
        Start-Sleep -Seconds 2
        $processes += Start-Process -FilePath $clientExe -ArgumentList @('-batchmode', '-nographics', '-raidClient', $case.ClientFlag, '-logFile', $clientTwoLog) -WindowStyle Hidden -PassThru
        $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
        while ((Get-Date) -lt $deadline) {
            Start-Sleep -Seconds 1
            $serverText = if (Test-Path -LiteralPath $serverLog) { Get-Content -LiteralPath $serverLog -Raw } else { '' }
            $oneText = if (Test-Path -LiteralPath $clientOneLog) { Get-Content -LiteralPath $clientOneLog -Raw } else { '' }
            $twoText = if (Test-Path -LiteralPath $clientTwoLog) { Get-Content -LiteralPath $clientTwoLog -Raw } else { '' }
            if ($serverText -match 'started=False|BOSS_SMOKE: failed|Exception:' -or
                $oneText -match 'client started=False|Exception:' -or
                $twoText -match 'client started=False|Exception:') {
                $reason = 'Runtime error in a log file.'
                break
            }
            if ($case.FailurePattern -and $serverText -match $case.FailurePattern) {
                $reason = 'Scenario reported a failed assertion.'
                break
            }
            if ($serverText -match $case.Expected -and $oneText -match $case.ClientExpected -and
                $twoText -match $case.ClientExpected) {
                $passed = $true
                break
            }
            if ($processes | Where-Object { $_.HasExited }) {
                $reason = 'A game process exited before the expected result.'
                break
            }
        }
        if (-not $passed -and -not $reason) { $reason = 'Timed out waiting for server and both clients.' }
    } finally {
        foreach ($process in $processes) {
            if ($process -and -not $process.HasExited) { Stop-Process -Id $process.Id -Force }
        }
    }
    $results += [pscustomobject]@{ Case = $case.Name; Passed = $passed; Reason = $reason }
    Write-Host ($case.Name + ': ' + $(if ($passed) { 'PASS' } else { 'FAIL - ' + $reason }))
}
$results | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $runDirectory 'summary.json')
Write-Host ('Logs: ' + $runDirectory)
if ($results.Passed -contains $false) { exit 1 }
