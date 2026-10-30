# Ordner-Öffner entfernen
Get-Process OrdnerOeffner -ErrorAction SilentlyContinue | Stop-Process -Force
Remove-Item "HKCU:\Software\Classes\ordner" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $env:LOCALAPPDATA "Programs\OrdnerOeffner") -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "Ordner-Öffner entfernt." -ForegroundColor Green
