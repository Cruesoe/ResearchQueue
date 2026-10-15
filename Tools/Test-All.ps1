param([string]$RimWorldRoot)
$ErrorActionPreference = 'Stop'
$researchQueueRepo = Split-Path $PSScriptRoot -Parent
& dotnet run --project (Join-Path $researchQueueRepo 'Tests\ResearchQueue.Checks.csproj') -c Release --no-launch-profile
if ($LASTEXITCODE -ne 0) { throw 'Research Queue offline checks failed.' }
$keys = @{}
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $researchQueueRepo 'Languages\English\Keyed') -Filter *.xml -File) {
  $document = [xml]([IO.File]::ReadAllText($file.FullName))
  foreach ($element in $document.LanguageData.ChildNodes) {
    if ($element.NodeType -ne [Xml.XmlNodeType]::Element) { continue }
    if ($keys.ContainsKey($element.Name)) { throw "Duplicate translation key: $($element.Name)" }
    $keys[$element.Name] = $true
  }
}
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $researchQueueRepo 'Source') -Filter *.cs -Recurse -File) {
  foreach ($match in [regex]::Matches([IO.File]::ReadAllText($file.FullName), '"(ResearchQueue_[A-Za-z0-9]+)"')) {
    if (-not $keys.ContainsKey($match.Groups[1].Value)) { throw "Missing translation key: $($match.Groups[1].Value)" }
  }
}
'Translation keys verified.'
