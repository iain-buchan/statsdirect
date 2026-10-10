param([Parameter(Mandatory=$true)][string]$Artifacts, [switch]$SavePasteArtifacts)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
$Artifacts = (Resolve-Path -LiteralPath $Artifacts).Path
$script:checks = 0
function Check([bool]$Value, [string]$Description) {
    if (-not $Value) { throw $Description }
    $script:checks++
    Write-Output "PASS $Description"
}
# Use new, hidden Office instances and new documents only. Restore the clipboard
# in memory; never log or save its previous contents with the test artifacts.
function PasteFixture([scriptblock]$Paste) {
    $previous = [System.Windows.Forms.DataObject]::new()
    $current = [System.Windows.Forms.Clipboard]::GetDataObject()
    if ($current) {
        foreach ($format in $current.GetFormats($false)) {
            try { $previous.SetData($format, $false, $current.GetData($format, $false)) } catch { }
        }
    }
    try {
        [System.Windows.Forms.Clipboard]::SetDataObject($payload, $true)
        & $Paste
    } finally {
        # Restore immediately, before an Office file save or close could block.
        [System.Windows.Forms.Clipboard]::SetDataObject($previous, $true)
    }
}
$excel = $null; $book = $null; $word = $null; $document = $null
try {
    $payload = [System.Windows.Forms.DataObject]::new()
    $payload.SetData([System.Windows.Forms.DataFormats]::Html, [IO.File]::ReadAllText((Join-Path $Artifacts 'office-clipboard.cfhtml')))
    $excel = New-Object -ComObject Excel.Application
    $excel.Visible = $false; $excel.DisplayAlerts = $false
    $book = $excel.Workbooks.Add(); $sheet = $book.Worksheets.Item(1)
    PasteFixture { $sheet.Paste() }
    $rows = @{}
    $used = $sheet.UsedRange
    for ($r = 1; $r -le $used.Rows.Count; $r++) {
        $label = [string]$sheet.Cells.Item($r, 1).Value2
        if ($label) { $rows[$label] = $r }
    }
    Check ($rows.ContainsKey('Border sample')) 'Excel receives a real table from the report HTML clipboard'
    function ValueCell([string]$Label) { $sheet.Cells.Item($rows[$Label], 2) }
    Check ((ValueCell 'Border sample').Value2 -eq 12.5 -and (ValueCell 'Negative').Value2 -eq -2.5) 'Excel receives decimal and negative values as numbers'
    Check ([Math]::Abs((ValueCell 'Exponent').Value2 - 1.2e-7) -lt 1e-20) 'Excel receives exponential values as numbers'
    Check ((ValueCell 'Percentage').Value2 -eq 0.95 -and (ValueCell 'Percentage').NumberFormat.Contains('%')) 'Excel retains the percentage value and format'
    Check ((ValueCell 'Identifier').Value2 -ceq '00123' -and (ValueCell 'Long identifier').Value2 -ceq '1234567890123456') 'Excel retains leading zeroes and long text identifiers'
    Check ((ValueCell 'Formula-like label').Value2 -ceq '=1+1' -and -not (ValueCell 'Formula-like label').HasFormula) 'Excel does not execute a formula-like report label'
    Check ($sheet.Cells.Item($rows['Merged heading'], 1).MergeArea.Columns.Count -eq 2) 'Excel retains the merged heading'
    $borderCell = $sheet.Cells.Item($rows['Border sample'], 1)
    Check ($borderCell.Font.Name -eq 'Georgia' -and $borderCell.Font.Size -eq 12) 'Excel retains the custom cell font and size'
    Check ($borderCell.Borders.Item(8).Color -eq 255 -and $borderCell.Borders.Item(9).Color -eq 16711680) 'Excel retains red top and blue bottom borders'
    Check ((ValueCell 'Border sample').Font.Size -eq 10) 'Excel retains the standard report text at 10pt on this monitor'
    Check ($sheet.Shapes.Count -gt 0 -and [Math]::Abs($sheet.Shapes.Item(1).Width - 288) -lt 1 -and [Math]::Abs($sheet.Shapes.Item(1).Height - 144) -lt 1) 'Excel pastes the chart at its four-by-two-inch report size'
    # Nothing reserved after the picture: the cells it floats over are empty, and the next text starts below it
    $shape = $sheet.Shapes.Item(1); $covered = 0
    for ($r = $shape.TopLeftCell.Row; $r -le $shape.BottomRightCell.Row; $r++) { for ($c = $shape.TopLeftCell.Column; $c -le $shape.BottomRightCell.Column; $c++) { if ([string]$sheet.Cells.Item($r, $c).Value2) { $covered++ } } }
    Check ($covered -eq 0) 'Excel places the chart picture over empty cells, with no line breaks reserved after it'
    if ($SavePasteArtifacts) { $book.SaveAs((Join-Path $Artifacts 'excel-paste.xlsx'), 51) }

    $word = New-Object -ComObject Word.Application
    $word.Visible = $false; $word.DisplayAlerts = 0
    $document = $word.Documents.Add()
    PasteFixture { $document.Content.Paste() }
    Check ($document.Tables.Count -eq 1) 'Word receives an editable table from the report HTML clipboard'
    $table = $document.Tables.Item(1)
    Check ($table.Cell(2,1).Range.Text.Contains('Border sample') -and $table.Cell(2,1).Range.Font.Name -eq 'Georgia') 'Word retains the custom table cell and font'
    Check ($document.InlineShapes.Count -gt 0) 'Word receives the chart picture from the clipboard'
    Check ([Math]::Abs($document.InlineShapes.Item(1).Width - 288) -lt 1 -and [Math]::Abs($document.InlineShapes.Item(1).Height - 144) -lt 1) 'Word pastes the chart at its four-by-two-inch report size'
    Check ($table.Cell(2,1).Range.Font.Size -eq 12 -and $table.Cell(2,2).Range.Font.Size -eq 10) 'Word pastes custom and standard text at their actual point sizes'
    # No line breaks after the chart, hidden or shown: character 11 is not in the picture's paragraph at all
    $pictureParagraph = $document.InlineShapes.Item(1).Range.Paragraphs.Item(1).Range
    $breaks = ($pictureParagraph.Text.ToCharArray() | Where-Object { [int]$_ -eq 11 }).Count
    Check ($breaks -eq 0) 'Word gets no line breaks after the chart'
    if ($SavePasteArtifacts) { $document.SaveAs2((Join-Path $Artifacts 'word-paste.docx'), 12) }
    $document.Close(0); $document = $null
    $document = $word.Documents.Open((Join-Path $Artifacts 'office-export.docx'), $false, $true)
    Check ($document.Tables.Count -eq 1 -and $document.InlineShapes.Count -gt 0) 'Word opens the exported DOCX with table and chart intact'
    Check ($document.Tables.Item(1).Cell(2,1).Range.Font.Name -eq 'Georgia') 'Word reads the exported cell font correctly'
    Write-Output "PASS $script:checks installed Office integration checks"
} finally {
    if ($document) { try { $document.Close(0) } catch { Write-Warning $_ } }
    if ($book) { try { $book.Close($false) } catch { Write-Warning $_ } }
    if ($word) { try { $word.Quit() } catch { Write-Warning $_ } }
    if ($excel) { try { $excel.Quit() } catch { Write-Warning $_ } }
}
