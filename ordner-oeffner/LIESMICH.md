# Ordner-Öffner

Damit öffnen lokale Ordner aus der Favoritenseite im Windows-Explorer.
Der Browser darf das aus Sicherheitsgründen nicht selbst; die Seite macht
aus `C:\…` deshalb einen Link `ordner:…`, und dieses kleine Programm nimmt
ihn entgegen.

**Installieren:** `installieren.cmd` doppelklicken. Keine Adminrechte nötig,
alles landet beim aktuellen Benutzer. Zum Aktualisieren einfach erneut ausführen.
**Entfernen:** `entfernen.cmd`.

Verhalten: Ist kein Explorer offen, kommt ein neues Fenster. Ist einer offen,
wird er nach vorn geholt und der Ordner in einem neuen Tab geöffnet. Klappt das
nicht, gibt es ersatzweise ein neues Fenster.

Geöffnet werden nur vorhandene Ordner auf Laufwerken — keine Dateien, keine
Programme, keine Netzwerkpfade. In den Favoriten gehen auch Pfade mit
Umgebungsvariablen, z. B. `%LOCALAPPDATA%\Comfy-Desktop`.
