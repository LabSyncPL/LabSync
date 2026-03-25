Write-Host "--- Checking Windows Update Status ---" -ForegroundColor Yellow

$service = Get-Service -Name wuauserv
Write-Host "Service Status: $($service.Status)"

Write-Host "`nFetching last 5 installed updates..."
Get-HotFix | Sort-Object InstalledOn -Descending | Select-Object -First 5 | 
    Format-Table -AutoSize | Out-String | Write-Host

Write-Host "100% - Update check complete."