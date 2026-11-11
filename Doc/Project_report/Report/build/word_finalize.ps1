param(
    [Parameter(Mandatory = $true)][string]$DocxPath,
    [string]$PdfPath = ""
)
# Opens a .docx in Word, updates every field and the tables of contents, saves it,
# and optionally exports a PDF copy for visual review.
$ErrorActionPreference = "Stop"
$word = New-Object -ComObject Word.Application
$word.Visible = $false
$word.DisplayAlerts = 0
try {
    $doc = $word.Documents.Open($DocxPath, $false, $false)
    foreach ($toc in $doc.TablesOfContents) { $toc.Update() }
    $doc.Fields.Update() | Out-Null
    foreach ($s in $doc.Sections) {
        foreach ($h in $s.Headers) { $h.Range.Fields.Update() | Out-Null }
        foreach ($f in $s.Footers) { $f.Range.Fields.Update() | Out-Null }
    }
    foreach ($toc in $doc.TablesOfContents) { $toc.Update() }
    $doc.Save()
    $pages = $doc.ComputeStatistics(2)
    if ($PdfPath -ne "") { $doc.ExportAsFixedFormat($PdfPath, 17) }
    $doc.Close($false)
    Write-Output ("pages=" + $pages)
}
finally {
    $word.Quit()
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($word) | Out-Null
}
