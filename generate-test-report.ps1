[CmdletBinding()]
param(
    [string]$SearchPath = "."
)

$ns = @{ x = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010" }

$trxFiles = @(Get-ChildItem -Path $SearchPath -Recurse -Filter "*.trx" -File -ErrorAction SilentlyContinue)

if ($trxFiles.Count -eq 0) {
    Write-Warning "No TRX files found under '$SearchPath'."
    exit 0
}

$rows = [System.Collections.Generic.List[string]]::new()
$grandTotal = 0
$grandPassed = 0
$grandFailed = 0
$grandSkipped = 0

foreach ($file in $trxFiles) {
    try {
        [xml]$xml = Get-Content -LiteralPath $file.FullName -Raw
    }
    catch {
        Write-Warning "Failed to parse $($file.FullName): $_"
        continue
    }

    $counters = $xml.TestRun.ResultSummary.Counters
    $total = [int]$counters.total
    $passed = [int]$counters.passed
    $failed = [int]$counters.failed
    $skipped = [int]$counters.notExecuted + [int]$counters.inconclusive

    $grandTotal += $total
    $grandPassed += $passed
    $grandFailed += $failed
    $grandSkipped += $skipped

    $failures = @()
    if ($null -ne $xml.TestRun.Results) {
        $failures = @(
            $xml.TestRun.Results.UnitTestResult |
                Where-Object { $_.outcome -and $_.outcome -notin @('Passed', 'NotExecuted', 'Inconclusive', 'Pending', 'Blocked') } |
                ForEach-Object { $_.testName }
        )
    }

    $outcomeClass = if ($failed -gt 0) { 'failed' } elseif ($total -gt 0 -and $passed -eq $total) { 'passed' } else { 'warning' }

    $failureHtml = ''
    if ($failures.Count -gt 0) {
        $failureHtml = '<ul>' + (($failures | ForEach-Object { '<li><code>' + [System.Net.WebUtility]::HtmlEncode($_) + '</code></li>' }) -join '') + '</ul>'
    }

    $rows.Add(@"
<tr class="$outcomeClass">
  <td><code>$($file.Name)</code></td>
  <td>$total</td>
  <td>$passed</td>
  <td>$failed</td>
  <td>$skipped</td>
  <td>$failureHtml</td>
</tr>
"@)
}

$grandOutcome = if ($grandFailed -gt 0) { 'failed' } else { 'passed' }
$generatedAt = (Get-Date).ToString('u')

$html = @"
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>Test Report</title>
<style>
  body { font-family: -apple-system, 'Segoe UI', Roboto, sans-serif; margin: 2rem; color: #1f2328; }
  h1 { margin-bottom: 0.5rem; }
  .summary { display: inline-block; padding: 0.75rem 1.25rem; border-radius: 6px; font-weight: 600; margin-bottom: 1.5rem; }
  .summary.passed { background: #dafbe1; color: #116329; }
  .summary.failed { background: #ffebe9; color: #82071e; }
  table { border-collapse: collapse; width: 100%; margin-bottom: 1rem; }
  th, td { border: 1px solid #d0d7de; padding: 0.5rem 0.75rem; text-align: left; vertical-align: top; }
  th { background: #f6f8fa; }
  tr.failed td:first-child { background: #ffebe9; }
  tr.passed td:first-child { background: #dafbe1; }
  ul { margin: 0; padding-left: 1.25rem; }
  .generated { color: #656d76; font-size: 0.85rem; }
</style>
</head>
<body>
<h1>Test Report</h1>
<div class="summary $grandOutcome">
  Total: $grandTotal &middot; Passed: $grandPassed &middot; Failed: $grandFailed &middot; Skipped: $grandSkipped
</div>
<table>
<thead>
  <tr><th>Test Run</th><th>Total</th><th>Passed</th><th>Failed</th><th>Skipped</th><th>Failures</th></tr>
</thead>
<tbody>
$($rows -join "`n")
</tbody>
</table>
<p class="generated">Generated $generatedAt</p>
</body>
</html>
"@

Set-Content -LiteralPath (Join-Path $SearchPath 'test-report.html') -Value $html -Encoding UTF8
Write-Host "HTML test report written to $SearchPath/test-report.html"
exit 0
