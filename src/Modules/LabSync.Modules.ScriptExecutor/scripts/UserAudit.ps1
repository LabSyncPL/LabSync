Write-Host "--- LabSync: Local User Security Audit ---" -ForegroundColor Cyan

$users = Get-LocalUser
$total = $users.Count
$current = 0

foreach ($user in $users) {
    $current++
    $passAge = if ($user.PasswordLastSet) { 
        (New-TimeSpan -Start $user.PasswordLastSet -End (Get-Date)).Days 
    } else { "Never" }

    Write-Host "[User] Name: $($user.Name) | Enabled: $($user.Enabled) | Password Age: $passAge days"
    
    $percent = [math]::Round(($current / $total) * 100)
    Write-Host "Progress: $percent%"
    Start-Sleep -Milliseconds 300
}

Write-Host "100% - Audit Finished." -ForegroundColor Green