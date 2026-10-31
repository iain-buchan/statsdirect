param([string]$Artifacts = $env:STATSDIRECT_OFFICE_ARTIFACTS)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.IO.Compression.FileSystem
# Opt-in probe called while WindowsShell owns the copy and safely preserves/restores
# every original clipboard format. This script reads, but never replaces, that copy.
# Constants: https://learn.microsoft.com/office/vba/api/powerpoint.ppsaveasfiletype
# and https://learn.microsoft.com/office/vba/api/word.wdpastedatatype.
# Word's synchronous COM paste is a separate opt-in: set
# STATSDIRECT_OFFICE_INCLUDE_WORD=1. Otherwise this probe does not certify Word.
if (@(Get-Process -Name WINWORD,POWERPNT -ErrorAction SilentlyContinue).Count) {
    Write-Output 'SKIP Office acceptance: close Word and PowerPoint first so only dedicated test instances are used.'
    exit 4
}
if (-not $Artifacts) { $Artifacts = Join-Path ([IO.Path]::GetTempPath()) 'StatsDirect-R-Office' }
$Artifacts = [IO.Path]::GetFullPath($Artifacts)
[IO.Directory]::CreateDirectory($Artifacts) | Out-Null
Add-Type -TypeDefinition 'using System.Runtime.InteropServices; public static class StatsDirectOfficeClipboardSequence { [DllImport("user32.dll")] public static extern uint GetClipboardSequenceNumber(); }'
$ownedSequence = [StatsDirectOfficeClipboardSequence]::GetClipboardSequenceNumber()
function RequireOwnedClipboard { if ([StatsDirectOfficeClipboardSequence]::GetClipboardSequenceNumber() -ne $ownedSequence) { throw 'The clipboard changed during Office acceptance; no further paste will be attempted.' } }
$current = [System.Windows.Forms.Clipboard]::GetDataObject()
$svgValue = $current.GetData('image/svg+xml', $false)
$svg = if ($svgValue -is [IO.MemoryStream]) { [Text.Encoding]::UTF8.GetString($svgValue.ToArray()).Trim([char]0) } elseif ($svgValue -is [string]) { $svgValue } else { $null }
$isVector = -not [string]::IsNullOrWhiteSpace($svg)
$kind = if ($isVector) { 'R-SVG' } else { 'R-PNG' }
$ownedFolder = Join-Path $Artifacts ($kind + '-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($ownedFolder) | Out-Null
if (-not $current.GetDataPresent('PNG', $false) -or -not [System.Windows.Forms.Clipboard]::ContainsImage()) { throw 'The test copy lacks its PNG/bitmap fallback.' }
function SvgFacts([string]$Text) {
    [xml]$xml = $Text
    $elements = @($xml.DocumentElement.SelectNodes('.//*'))
    $number = '[-+]?(?:\d*\.\d+|\d+\.?)(?:[eE][-+]?\d+)?'
    $coordinates = [Collections.Generic.List[double]]::new()
    $schema = [Collections.Generic.List[string]]::new()
    $markers = 0
    foreach ($node in $elements) {
        if ($node.LocalName -notin @('path','g','text','use','circle','ellipse','line','rect')) { continue }
        if ($node.LocalName -eq 'path' -and $node.GetAttribute('d') -match '[Cc]') {
            $fill=''; $parent=$node
            while ($parent -and $parent -is [Xml.XmlElement] -and -not $fill) {
                $fill=$parent.GetAttribute('fill')
                $style=[regex]::Match($parent.GetAttribute('style'),'(?:^|;)\s*fill\s*:\s*([^;]+)')
                if ($style.Success) { $fill=$style.Groups[1].Value.Trim() }
                $parent=$parent.ParentNode
            }
            if ($fill -eq 'none') { $markers++ }
        }
        $values=[Collections.Generic.List[double]]::new()
        foreach ($name in @('d','transform','x','y','cx','cy','r','rx','ry','x1','y1','x2','y2','width','height')) {
            $value=$node.GetAttribute($name)
            # Cairo appends a final empty move after closed contours. Office removes
            # it and compacts/rounds numbers while retaining the actual geometry.
            if ($name -eq 'd') { $value=[regex]::Replace($value,'[Mm]\s*'+$number+'[\s,]*'+$number+'\s*$','') }
            foreach ($match in [regex]::Matches($value,$number)) { $values.Add([double]::Parse($match.Value,[Globalization.CultureInfo]::InvariantCulture)) }
        }
        $schema.Add($node.LocalName+':'+$values.Count)
        $coordinates.AddRange($values)
    }
    return @{ markers=$markers; schema=($schema -join '|'); coordinates=$coordinates.ToArray() }
}
function SameGeometry($Actual, $Expected) {
    if (-not $Expected -or $Actual.schema -cne $Expected.schema -or $Actual.coordinates.Count -ne $Expected.coordinates.Count) { return $false }
    for ($i=0; $i -lt $Actual.coordinates.Count; $i++) {
        if ([Math]::Abs($Actual.coordinates[$i]-$Expected.coordinates[$i]) -gt 0.001) { return $false }
    }
    return $true
}
$reference = if ($isVector) { SvgFacts $svg } else { $null }
if ($isVector) {
    [IO.File]::WriteAllText((Join-Path $ownedFolder 'clipboard-copy.svg'), $svg, [Text.UTF8Encoding]::new($false))
    if ($reference.markers -ne 9) { throw "Expected nine circle markers in the copied SVG, found $($reference.markers)." }
}
function InspectPackage([string]$Path, [string]$Label) {
    $zip = [IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $vectors = @(); $rasters = @(); $svgBlip = $false; $matching = $false; $markers = @()
        foreach ($entry in $zip.Entries) {
            if ($entry.FullName -match '/media/.*\.png$') { $rasters += $entry.FullName }
            if ($entry.FullName -match '\.(?:xml|svg)$') {
                $reader = [IO.StreamReader]::new($entry.Open(), [Text.Encoding]::UTF8)
                try { $text = $reader.ReadToEnd() } finally { $reader.Dispose() }
                if ($entry.FullName.EndsWith('.xml') -and $text.Contains('svgBlip')) { $svgBlip = $true }
                if ($entry.FullName -match '/media/.*\.svg$') {
                    $vectors += $entry.FullName
                    $facts = SvgFacts $text; $markers += $facts.markers
                    if (SameGeometry $facts $reference) { $matching = $true }
                }
            }
        }
        return [pscustomobject]@{ label=$Label; file=$Path; svgBlip=$svgBlip; svgMedia=$vectors; pngMedia=$rasters; circleMarkers=$markers; matchesCopiedGeometry=$matching }
    } finally { $zip.Dispose() }
}
$word=$null; $ppt=$null; $document=$null; $presentation=$null
$results = [Collections.Generic.List[object]]::new()
function Stage([string]$Text) { [IO.File]::AppendAllText((Join-Path $ownedFolder 'progress.txt'), $Text + [Environment]::NewLine) }
try {
    Stage 'Creating dedicated Office COM applications'
    try { $ppt = New-Object -ComObject PowerPoint.Application; if ($env:STATSDIRECT_OFFICE_INCLUDE_WORD -eq '1') { $word = New-Object -ComObject Word.Application } }
    catch { Write-Output ('SKIP Office acceptance: Word or PowerPoint is unavailable: ' + $_.Exception.Message); exit 4 }
    Stage 'PowerPoint: create blank presentation'
    $presentation=$ppt.Presentations.Add(0); $slide=$presentation.Slides.Add(1,12)
    Stage 'PowerPoint: paste default clipboard graphic'
    RequireOwnedClipboard
    $shapes=$slide.Shapes.Paste()
    if ($shapes.Count -lt 1) { throw 'PowerPoint default Paste inserted no chart.' }
    $pptPath=Join-Path $ownedFolder 'powerpoint-default-paste.pptx'
    Stage 'PowerPoint: save default paste package'
    $presentation.SaveAs($pptPath,24); $presentation.Close(); $presentation=$null
    $defaultPpt=InspectPackage $pptPath 'PowerPoint Shapes.Paste'; $results.Add($defaultPpt)
    Stage 'PowerPoint: create blank presentation'
    $presentation=$ppt.Presentations.Add(0); $slide=$presentation.Slides.Add(1,12)
    Stage 'PowerPoint: paste PNG fallback'
    RequireOwnedClipboard
    $fallback=$slide.Shapes.PasteSpecial(6)
    if ($fallback.Count -lt 1) { throw 'PowerPoint PNG Paste Special inserted no picture.' }
    $fallbackPath=Join-Path $ownedFolder 'powerpoint-png-fallback.pptx'
    $presentation.SaveAs($fallbackPath,24); $presentation.Close(); $presentation=$null
    $pngResult=InspectPackage $fallbackPath 'PowerPoint PasteSpecial PNG'; $results.Add($pngResult)
    if (-not $pngResult.pngMedia.Count -or $pngResult.svgMedia.Count) { throw 'PNG fallback package did not retain a raster picture.' }
    $defaultWord=[pscustomobject]@{label='Word';svgBlip=$false;svgMedia=@();matchesCopiedGeometry=$false;notCompleted=$true}
    if ($word) {
    Stage 'Word: configure hidden application'
    $word.Visible=$false; $word.DisplayAlerts=0
    Stage 'Word: create blank document'
    $document=$word.Documents.Add()
    Stage 'Word: default Range.Paste'
    RequireOwnedClipboard
    $document.Content.Paste()
    $wordPath=Join-Path $ownedFolder 'word-default-paste.docx'
    $document.SaveAs2($wordPath,12); $document.Close(0); $document=$null
    $defaultWord=InspectPackage $wordPath 'Word Range.Paste'; $results.Add($defaultWord)
    if ($isVector) {
        $document=$word.Documents.Add()
        try {
            $missing=[Type]::Missing
            Stage 'Word: PasteSpecial shape'
            RequireOwnedClipboard
            $document.Content.PasteSpecial($missing,$false,$missing,$false,8)
            $special=Join-Path $ownedFolder 'word-shape-paste.docx'; $document.SaveAs2($special,12)
            $results.Add((InspectPackage $special 'Word PasteSpecial shape'))
        } catch { $results.Add([pscustomobject]@{ label='Word PasteSpecial shape'; error=$_.Exception.Message }) }
        finally { $document.Close(0); $document=$null }
    }
    } else { $results.Add($defaultWord) }
    $report=[pscustomobject]@{ input=$kind; wordVersion=$word.Version; powerpointVersion=$ppt.Version; copiedCircleMarkers=if($reference){$reference.markers}else{0}; results=$results }
    $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $ownedFolder 'results.json') -Encoding UTF8
    Write-Output ('Office artifacts: ' + $ownedFolder)
    if ($defaultPpt.svgBlip -and $defaultPpt.svgMedia.Count -and $defaultPpt.matchesCopiedGeometry) { Write-Output 'PowerPoint: vector' } else { Write-Output 'PowerPoint: raster only' }
    if ($defaultWord.notCompleted) { Write-Output 'Word: automation not completed' } elseif ($defaultWord.svgBlip -and $defaultWord.svgMedia.Count -and $defaultWord.matchesCopiedGeometry) { Write-Output 'Word: vector' } else { Write-Output 'Word: raster only' }
    foreach ($result in $results) { Write-Output ($result | ConvertTo-Json -Compress -Depth 6) }
    if ($isVector -and (-not $defaultPpt.svgBlip -or -not $defaultPpt.matchesCopiedGeometry)) { throw 'PowerPoint lost the copied vector geometry or labels.' }
    if ($isVector -and $defaultWord.svgBlip -and $defaultWord.matchesCopiedGeometry) { exit 0 }
    exit 2 # Preserve evidence when an Office COM path selected the supplied raster.
} finally {
    if ($document) { try { $document.Close(0) } catch {} }
    if ($presentation) { try { $presentation.Close() } catch {} }
    if ($word) { try { $word.Quit() } catch {} }
    if ($ppt) { try { $ppt.Quit() } catch {} }
    [GC]::Collect(); [GC]::WaitForPendingFinalizers()
}
