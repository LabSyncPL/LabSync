Write-Host '========================================'
Write-Host '   LabSync: Diagnostyka Systemu v1.0    '
Write-Host '========================================'
Write-Host ''

Write-Host '[*] Top 5 procesow obciazajacych CPU:'

Get-Process |
Sort-Object CPU -Descending |
Select-Object -First 5 Name, Id, CPU |
Format-Table -AutoSize

Write-Host ''

Write-Host '[*] Skanowanie folderu TEMP...'

$tempPath = $env:TEMP

$tempFiles = Get-ChildItem `
    -Path $tempPath `
    -Recurse `
    -File `
    -ErrorAction SilentlyContinue

$tempSize = ($tempFiles | Measure-Object Length -Sum).Sum / 1MB

Write-Host ('Znaleziono: ' + [math]::Round($tempSize,2) + ' MB plikow tymczasowych.')

Write-Host '[*] Rozpoczynam czyszczenie starych plikow...'

$limit = (Get-Date).AddDays(-7)

$tempFiles |
Where-Object { $_.LastWriteTime -lt $limit } |
Remove-Item -Force -ErrorAction SilentlyContinue

Write-Host ''
Write-Host '========================================'
Write-Host '[OK] Akcja zakonczona sukcesem.'
Write-Host '========================================'