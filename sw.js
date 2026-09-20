/* ============================================================
   Service Worker der Favoriten-Startseite.

   Zweck: die Seite startet auch ohne Netz. Gespeichert wird nur
   das Gerüst (HTML, Manifest, Symbol) - die Favoriten selbst
   liegen im Browserspeicher der Seite und werden hier nicht
   angefasst. Die Adresse des Apps Script wird bewusst NIE
   zwischengespeichert, sonst käme veralteter Stand zurück.
   ============================================================ */

const VERSION = "favoriten-v4";
const GERUEST = ["./", "./index.html", "./manifest.webmanifest", "./icon.svg"];

self.addEventListener("install", e => {
  e.waitUntil(
    caches.open(VERSION)
      .then(c => c.addAll(GERUEST).catch(()=>{}))
      .then(() => self.skipWaiting())
  );
});

self.addEventListener("activate", e => {
  e.waitUntil(
    caches.keys()
      .then(namen => Promise.all(namen.filter(n => n !== VERSION).map(n => caches.delete(n))))
      .then(() => self.clients.claim())
  );
});

self.addEventListener("fetch", e => {
  const anfrage = e.request;
  if (anfrage.method !== "GET") return;                   // Sync-POSTs durchlassen
  const url = new URL(anfrage.url);
  if (url.origin !== self.location.origin) return;        // Apps Script & Co. nie cachen

  // Seitenaufrufe: erst Netz (damit Änderungen ankommen), sonst aus dem Speicher
  if (anfrage.mode === "navigate") {
    e.respondWith(
      fetch(anfrage)
        .then(a => { const k = a.clone(); caches.open(VERSION).then(c => c.put("./index.html", k)); return a; })
        .catch(() => caches.match("./index.html").then(a => a || caches.match("./")))
    );
    return;
  }

  // Übriges Gerüst: sofort aus dem Speicher, im Hintergrund auffrischen
  e.respondWith(
    caches.match(anfrage).then(treffer => {
      const netz = fetch(anfrage).then(a => {
        if (a && a.ok) { const k = a.clone(); caches.open(VERSION).then(c => c.put(anfrage, k)); }
        return a;
      }).catch(() => treffer);
      return treffer || netz;
    })
  );
});
