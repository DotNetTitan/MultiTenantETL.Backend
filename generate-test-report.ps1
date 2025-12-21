param(
    [string]$OutputPath = "test-report.html"
)

# Function to parse TRX file and extract test results
function Parse-TrxFile {
    param([string]$TrxPath)

    $xml = [xml](Get-Content $TrxPath)
    $testRun = $xml.TestRun

    $results = @{
        TotalTests = 0
        Passed = 0
        Failed = 0
        Skipped = 0
        Duration = "00:00:00"
        TestResults = @()
    }

    if ($testRun.ResultSummary) {
        $summary = $testRun.ResultSummary
        $counters = $summary.Counters

        $results.TotalTests = [int]$counters.total
        $results.Passed = [int]$counters.passed
        $results.Failed = [int]$counters.failed
        $results.Skipped = [int]$counters.inconclusive + [int]$counters.notExecuted
    }

    if ($testRun.Times) {
        $results.Duration = $testRun.Times.duration
    }

    # Parse individual test results
    if ($testRun.Results) {
        foreach ($unitTestResult in $testRun.Results.UnitTestResult) {
            $result = @{
                TestName = $unitTestResult.testName
                Outcome = $unitTestResult.outcome
                Duration = $unitTestResult.duration
                ErrorMessage = ""
                StackTrace = ""
            }

            if ($unitTestResult.Output) {
                if ($unitTestResult.Output.ErrorInfo) {
                    $result.ErrorMessage = $unitTestResult.Output.ErrorInfo.Message
                    $result.StackTrace = $unitTestResult.Output.ErrorInfo.StackTrace
                }
            }

            $results.TestResults += $result
        }
    }

    return $results
}

# Get all TRX files
$trxFiles = Get-ChildItem -Path . -Filter "*.trx" -Recurse

$allResults = @{
    TotalTests = 0
    Passed = 0
    Failed = 0
    Skipped = 0
    Duration = "00:00:00"
    TestFiles = @()
}

foreach ($trxFile in $trxFiles) {
    Write-Host "Processing $($trxFile.FullName)"
    $fileResults = Parse-TrxFile -TrxPath $trxFile.FullName

    $allResults.TotalTests += $fileResults.TotalTests
    $allResults.Passed += $fileResults.Passed
    $allResults.Failed += $fileResults.Failed
    $allResults.Skipped += $fileResults.Skipped

    $allResults.TestFiles += @{
        FileName = $trxFile.Name
        Results = $fileResults
    }
}

# Generate HTML report
$html = @"
<!DOCTYPE html>
<html>
<head>
    <title>Test Results Report</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 20px; }
        .summary { background-color: #f5f5f5; padding: 15px; border-radius: 5px; margin-bottom: 20px; }
        .passed { color: #28a745; }
        .failed { color: #dc3545; }
        .skipped { color: #ffc107; }
        .total { color: #007bff; }
        table { border-collapse: collapse; width: 100%; margin-bottom: 20px; }
        th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }
        th { background-color: #f2f2f2; }
        .test-result { margin-bottom: 10px; }
        .error { background-color: #f8d7da; color: #721c24; padding: 10px; border-radius: 5px; }
        .stack-trace { font-family: monospace; font-size: 12px; background-color: #f8f9fa; padding: 10px; border-radius: 5px; margin-top: 5px; }
    </style>
</head>
<body>
    <h1>Test Results Report</h1>
    <div class="summary">
        <h2>Summary</h2>
        <p><strong>Total Tests:</strong> <span class="total">$($allResults.TotalTests)</span></p>
        <p><strong>Passed:</strong> <span class="passed">$($allResults.Passed)</span></p>
        <p><strong>Failed:</strong> <span class="failed">$($allResults.Failed)</span></p>
        <p><strong>Skipped:</strong> <span class="skipped">$($allResults.Skipped)</span></p>
        <p><strong>Success Rate:</strong> $(if ($allResults.TotalTests -gt 0) { [math]::Round(($allResults.Passed / $allResults.TotalTests) * 100, 2) } else { 0 })%</p>
    </div>
"@

foreach ($fileResult in $allResults.TestFiles) {
    $html += @"
    <h2>$($fileResult.FileName)</h2>
    <table>
        <tr>
            <th>Test Name</th>
            <th>Result</th>
            <th>Duration</th>
        </tr>
"@

    foreach ($testResult in $fileResult.Results.TestResults) {
        $resultClass = switch ($testResult.Outcome) {
            "Passed" { "passed" }
            "Failed" { "failed" }
            "Skipped" { "skipped" }
            default { "" }
        }

        $html += @"
        <tr>
            <td>$($testResult.TestName)</td>
            <td class="$resultClass">$($testResult.Outcome)</td>
            <td>$($testResult.Duration)</td>
        </tr>
"@

        if ($testResult.ErrorMessage) {
            $html += @"
        <tr>
            <td colspan="3">
                <div class="error">
                    <strong>Error:</strong> $($testResult.ErrorMessage)
                    $(if ($testResult.StackTrace) { "<br><strong>Stack Trace:</strong><div class='stack-trace'>$($testResult.StackTrace)</div>" })
                </div>
            </td>
        </tr>
"@
        }
    }

    $html += @"
    </table>
"@
}

$html += @"
</body>
</html>
"@

# Write HTML report
$html | Out-File -FilePath $OutputPath -Encoding UTF8
Write-Host "HTML report generated: $OutputPath"