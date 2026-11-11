# Rebuilds every report document and workbook, then lets Word update the tables of contents and export PDFs.
param([string]$PdfDir = "")
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot
$rep = Split-Path $PSScriptRoot -Parent
$env:PKV_EDITMODE_RESULTS = Join-Path $rep "evidence\editmode_results_20261111.json"
$env:PKV_UNIT_TPL = Join-Path $PSScriptRoot "unit_test_template.xlsx"
python gen_r1.py; python gen_r2.py; python gen_r3.py; python gen_r4.py; python gen_r5.py; python gen_r5_xlsx.py; python gen_tracking.py
$docs = @("Report1_Project Introduction", "Report2_Project Management Plan", "Report3_Software Requirement Specification", "Report4_Software Design Document", "Report5_Test Documentation")
foreach ($d in $docs) {
    $pdf = ""
    if ($PdfDir -ne "") { $pdf = Join-Path $PdfDir ($d + ".pdf") }
    & (Join-Path $PSScriptRoot "word_finalize.ps1") -DocxPath (Join-Path $rep ($d + ".docx")) -PdfPath $pdf
}
