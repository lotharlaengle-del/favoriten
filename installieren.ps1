# Ordner-Öffner installieren (nur für den aktuellen Benutzer, keine Adminrechte nötig)
#
#  1. übersetzt OrdnerOeffner.cs zu einer kleinen .exe (ohne Konsolenfenster)
#  2. legt sie in %LOCALAPPDATA%\Programs\OrdnerOeffner ab
#  3. meldet das Linkschema  ordner:  bei Windows an (HKCU\Software\Classes\ordner)
#
# Erneut ausführen = aktualisieren.

$ErrorActionPreference = "Stop"
$quelle = Join-Path $PSScriptRoot "OrdnerOeffner.cs"
$ziel   = Join-Path $env:LOCALAPPDATA "Programs\OrdnerOeffner"
$exe    = Join-Path $ziel "OrdnerOeffner.exe"

Write-Host ""
Write-Host "Ordner-Öffner wird installiert ..." -ForegroundColor Cyan

if (-not (Test-Path $quelle)) { throw "OrdnerOeffner.cs fehlt neben diesem Skript." }
New-Item -ItemType Directory -Path $ziel -Force | Out-Null

# Laufende Instanz würde das Überschreiben blockieren
Get-Process OrdnerOeffner -ErrorAction SilentlyContinue | Stop-Process -Force
if (Test-Path $exe) { Remove-Item $exe -Force }

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName Microsoft.CSharp
$refs = @(
  [System.Windows.Forms.Form].Assembly.Location,
  [Microsoft.CSharp.RuntimeBinder.Binder].Assembly.Location,
  [System.Runtime.CompilerServices.CallSite].Assembly.Location
)
Add-Type -TypeDefinition (Get-Content -Raw -Encoding UTF8 $quelle) -Language CSharp `
         -ReferencedAssemblies $refs -OutputAssembly $exe -OutputType WindowsApplication
Write-Host "  Programm:  $exe"

$k = "HKCU:\Software\Classes\ordner"
New-Item -Path $k -Value "URL:Ordner-Öffner" -Force | Out-Null
New-ItemProperty -Path $k -Name "URL Protocol" -Value "" -PropertyType String -Force | Out-Null
New-Item -Path "$k\DefaultIcon" -Value "explorer.exe,0" -Force | Out-Null
New-Item -Path "$k\shell\open\command" -Value ('"' + $exe + '" "%1"') -Force | Out-Null
Write-Host "  Linkschema ordner: angemeldet"

Write-Host ""
Write-Host "Fertig. Zum Test öffnet sich jetzt dein Benutzerordner." -ForegroundColor Green
Start-Process ("ordner:" + [uri]::EscapeDataString($env:USERPROFILE))
Write-Host ""
Write-Host "Beim ersten Klick in der Favoritenseite fragt Chrome, ob der Link geöffnet"
Write-Host "werden darf. Häkchen bei 'Immer erlauben' setzen, dann fragt er nicht mehr."
