$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$buildRoot = Join-Path $projectRoot 'Builds\Tester'
$guide = Join-Path $projectRoot 'docs\TESTER_GUIDE.md'
if (-not (Test-Path -LiteralPath (Join-Path $buildRoot 'ChainboundRaid.exe'))) {
    throw 'Build the tester client in Unity before packaging.'
}
$packageDir = Join-Path $projectRoot 'Builds\Packages'
New-Item -ItemType Directory -Path $packageDir -Force | Out-Null
$archivePath = Join-Path $packageDir ('ChainboundRaid-Tester-' + (Get-Date -Format 'yyyyMMdd-HHmm') + '.zip')
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::Open($archivePath, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    Get-ChildItem -LiteralPath $buildRoot -Recurse -File | Where-Object {
        $_.FullName -notmatch 'bindraid_BurstDebugInformation_DoNotShip'
    } | ForEach-Object {
        $entryName = 'ChainboundRaid/' + $_.FullName.Substring($buildRoot.Length + 1).Replace('\', '/')
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $_.FullName, $entryName) | Out-Null
    }
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $guide, 'ChainboundRaid/TESTER_GUIDE.md') | Out-Null
} finally {
    $archive.Dispose()
}
Get-FileHash -LiteralPath $archivePath -Algorithm SHA256 | Select-Object Path, Hash
