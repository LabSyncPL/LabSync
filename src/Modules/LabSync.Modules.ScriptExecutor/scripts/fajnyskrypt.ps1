Start-Sleep -Seconds 7

$wshell = New-Object -ComObject wscript.shell
1..50 | ForEach-Object { $wshell.SendKeys([char]175) }

Write-Host "[!] UWAGA: Wykryto krytyczna luke w zabezpieczeniach stacji (CVE-2026-LABSYNC)." -ForegroundColor Red
Start-Sleep -Milliseconds 800

Write-Host "[*] Wymuszanie polaczenia systemowego... " -NoNewline -ForegroundColor Cyan
Start-Sleep -Milliseconds 600
Write-Host "ZAAKCEPTOWANO" -ForegroundColor Green

Write-Host "[*] Inicjalizacja modulu LabSync-Patch-1337..." -ForegroundColor Cyan
Start-Sleep -Milliseconds 800

Write-Host "[*] Wstrzykiwanie ladunku do jadra systemu: [" -NoNewline -ForegroundColor White
for ($i = 1; $i -le 15; $i++) {
    Write-Host "#" -NoNewline -ForegroundColor Yellow
    Start-Sleep -Milliseconds 100
}
Write-Host "] 100%" -ForegroundColor Green

Start-Sleep -Milliseconds 800

Write-Host "`n[SYSTEM] A tak naprawde... mamy absolutna wladze nad ta maszyna! :)" -ForegroundColor Green

Start-Process "https://www.youtube.com/watch?v=dQw4w9WgXcQ"