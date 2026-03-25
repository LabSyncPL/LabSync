Write-Host "--- LabSync Network Diagnostics ---" -ForegroundColor Cyan

$targets = @("8.8.8.8", "github.com", "microsoft.com")
$step = [math]::Round(100 / $targets.Count)
$progress = 0

foreach ($hostName in $targets) {
    Write-Host "[Checking] Latency to $hostName..." -ForegroundColor White
    $ping = Test-Connection -ComputerName $hostName -Count 4 | Select-Object Address, ResponseTime
    $ping | Out-String | Write-Host
    
    $progress += $step
    Write-Host "Progress: $progress%"
    Start-Sleep -Seconds 1
}

Write-Host "100% - Connectivity Audit Complete." -ForegroundColor Green