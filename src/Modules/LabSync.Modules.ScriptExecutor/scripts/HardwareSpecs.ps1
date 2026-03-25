Write-Host "--- Gathering Hardware Inventory ---" -ForegroundColor Cyan

$info = @{
    CPU = (Get-CimInstance Win32_Processor).Name
    RAM = "$([math]::Round((Get-CimInstance Win32_PhysicalMemory | Measure-Object Capacity -Sum).Sum / 1GB, 0)) GB"
    GPU = (Get-CimInstance Win32_VideoController).Caption
    Motherboard = (Get-CimInstance Win32_BaseBoard).Product
}

foreach ($key in $info.Keys) {
    Write-Host "$key : $($info[$key])"
    Start-Sleep -Milliseconds 500
}

Write-Host "100% - Inventory data pushed to console."