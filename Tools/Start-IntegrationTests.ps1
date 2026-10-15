param(
  [string]$RimWorldRoot = 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld',
  [string]$ModList = 'brrainz.harmony,cruesoe.researchqueue,cruesoe.researchqueue.tests'
)
$ErrorActionPreference = 'Stop'
if (Get-Process RimWorld,RimWorldWin64 -ErrorAction SilentlyContinue) { throw 'A RimWorld process is already running.' }
$fixture = Join-Path $RimWorldRoot 'Mods\Research Queue Test Fixture\About\About.xml'
if (-not (Test-Path -LiteralPath $fixture)) { throw 'Deploy Tests/Integration as a separate temporary mod first.' }
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('ResearchQueueTests-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
$log = Join-Path $testRoot 'Game.log'
$stderr = Join-Path $testRoot 'GameStderr.log'
$arguments = @('-savedatafolder="' + $testRoot + '"', ('-mod=' + $ModList), '-quicktest', '-researchQueueTests', '-NoFileLog')
$process = Start-Process -FilePath (Join-Path $RimWorldRoot 'RimWorld.exe') -WorkingDirectory $RimWorldRoot -ArgumentList $arguments -WindowStyle Hidden -RedirectStandardOutput $log -RedirectStandardError $stderr -PassThru
[pscustomobject]@{ ProcessId = $process.Id; Directory = $testRoot; Report = (Join-Path $testRoot 'Integration.txt'); Log = $log } | ConvertTo-Json
