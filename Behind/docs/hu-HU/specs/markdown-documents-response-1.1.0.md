# Markdown dokumentumlista-válasz 1.1.0

A `GET /markdownviewer/documents?locale=<locale>` nyilvános, és HTTP 200 állapottal JSON objektumot ad vissza. A választ a [JSON-séma](../../schemas/markdown-documents-response-1.1.0.schema.json) írja le.

A `configured` jelzi, hogy a beállított forráskönyvtár létezik-e. A `title` és a `defaultDocument` a munkaterületet írja le. A `locales` az elérhető nyelvi kódokat tartalmazza; a `locale` a kiválasztott nyelv vagy null. A `home` a kezdődokumentum útvonala vagy null. Az `entries` dokumentumútvonalakat és címeket tartalmaz; a `sections` legfelső szintű szakaszokba csoportosítja a bejegyzéseket, és a szakasz dokumentumának útvonala opcionális. Először a kért nyelv, majd a webhely alapértelmezett nyelve, végül az első elérhető nyelv az irányadó.

Az `errors` beállított munkaterületnél üres; egyébként `source-not-configured` hibát tartalmaz üres vagy meg nem adott forrásútvonalnál, vagy `source-missing` hibát, ha a feloldott könyvtár nem létezik. A hibák nem közölnek útvonalakat. Nem elérhető munkaterület esetén a nyelvek, bejegyzések és szakaszok listája üres, a `locale` és a `home` null, a cím és a dokumentumnév pedig az alapértelmezett.

## Kompatibilitás és migráció

Az 1.1.0 verzió a korábban dokumentált válaszhoz hozzáadja az `errors` mezőt, a meglévő mezők eltávolítása vagy módosítása nélkül. A további mezőket figyelmen kívül hagyó klienseknek nem kell migrálniuk. A szigorú klienseknek el kell fogadniuk az `errors` mezőt; a régebbi kiszolgálókat is támogató klienseknek kezelniük kell a hiányát, amikor nem különböztethető meg a be nem állított útvonal a hiányzó könyvtártól. Tárolt adatot nem kell migrálni.
