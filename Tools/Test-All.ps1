# Checks run by Deploy-Mod before deploying: offline queue checks, translation keys, keyed XML whitespace, no tiny fonts, metadata.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$failures = New-Object System.Collections.Generic.List[string]

& dotnet run --project (Join-Path $root 'Tests\ResearchQueue.Checks.csproj') -c Release --no-launch-profile
if ($LASTEXITCODE -ne 0) { $failures.Add('Offline queue checks failed') }

$sourceText = (Get-ChildItem (Join-Path $root 'Source') -Recurse -Filter *.cs |
    Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' } |
    ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -join "`n"

$used = New-Object System.Collections.Generic.HashSet[string]
foreach ($m in [regex]::Matches($sourceText, '"(ResearchQueue_[A-Za-z0-9_]+)"')) { [void]$used.Add($m.Groups[1].Value) }

$keyed = New-Object System.Collections.Generic.HashSet[string]
foreach ($file in Get-ChildItem (Join-Path $root 'Languages\English\Keyed') -Filter *.xml) {
    $text = [IO.File]::ReadAllText($file.FullName)
    if ($text -match '[ \t]+\r?\n') { $failures.Add("Trailing whitespace in $($file.Name)") }
    [xml]$xml = $text
    foreach ($node in $xml.LanguageData.ChildNodes) {
        if ($node.NodeType -eq 'Element' -and -not $keyed.Add($node.Name)) { $failures.Add("Duplicate key $($node.Name)") }
    }
}
foreach ($key in $used) { if (-not $keyed.Contains($key)) { $failures.Add("Missing translation: $key") } }
foreach ($key in $keyed) { if (-not $used.Contains($key)) { $failures.Add("Unused translation: $key") } }

if ($sourceText -match 'GameFont\.Tiny') { $failures.Add('GameFont.Tiny is used') }
if ($sourceText -match 'GUI\.enabled') { $failures.Add('GUI.enabled is used; grey out with the active argument instead') }

[xml]$about = [IO.File]::ReadAllText((Join-Path $root 'About\About.xml'))
if ($about.ModMetaData.packageId -ne 'cruesoe.researchqueue') { $failures.Add('Wrong package ID') }
foreach ($id in 'cruesoe.semirandompfork.continued', 'cruesoe.research.auto') {
    if (@($about.ModMetaData.incompatibleWith.li) -notcontains $id) { $failures.Add("Not marked incompatible with $id") }
}
$preview = Join-Path $root 'About\Preview.png'
if ((Test-Path $preview) -and (Get-Item $preview).Length -ge 1000000) { $failures.Add('About\Preview.png is 1,000,000 bytes or more') }

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Host "FAIL $_" }
    exit 1
}
Write-Host "Test-All passed: $($keyed.Count) keys checked"
