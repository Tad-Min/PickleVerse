# Rebuilds every report document and workbook, then lets Word update the tables of contents and export PDFs.
param([string]$PdfDir = "")
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot
$rep = Split-Path $PSScriptRoot -Parent
$env:PKV_EDITMODE_RESULTS = Join-Path $rep "evidence\editmode_results_20261111_5963501.json"
$env:PKV_UNIT_TPL = Join-Path $PSScriptRoot "unit_test_template.xlsx"
python gen_r1.py; python gen_r2.py; python gen_r3.py; python gen_r4.py; python gen_r5.py; python gen_r6.py; python gen_r7.py; python gen_r5_xlsx.py; python gen_tracking.py
$docs = @("Report1_Project Introduction", "Report2_Project Management Plan", "Report3_Software Requirement Specification", "Report4_Software Design Document", "Report5_Test Documentation", "Report6_Software User Guides", "Report7_Final Project Report")
foreach ($d in $docs) {
    $pdf = ""
    if ($PdfDir -ne "") { $pdf = Join-Path $PdfDir ($d + ".pdf") }
    & (Join-Path $PSScriptRoot "word_finalize.ps1") -DocxPath (Join-Path $rep ($d + ".docx")) -PdfPath $pdf
}

# Store the calculated values of the workbook formulas.
$xl = New-Object -ComObject Excel.Application
$xl.Visible = $false
$xl.DisplayAlerts = $false
try {
    foreach ($n in @("Report5_Test Report.xlsx", "Report5_Unit Test.xlsx", "PickleVerse_Project_Tracking.xlsx")) {
        $wb = $xl.Workbooks.Open((Join-Path $rep $n)); $xl.CalculateFull(); $wb.Save(); $wb.Close($false)
    }
} finally { $xl.Quit(); [System.Runtime.InteropServices.Marshal]::ReleaseComObject($xl) | Out-Null }
