# Konfigurációs JSON és beállítások 1.7.0

Állapot: aktuális. Ez a bővítő változat a futásidejű ellenőrzéshez felváltja az
[1.6.0 konfigurációs szerződést](./configuration-json-1.6.0.md).
A teljes [JSON Schema](../../schemas/configuration-1.7.0.schema.json) megőrzi
az előző definíciókat, és modulkapcsolókat, valamint dokumentált modulalapértékeket
ad hozzá. A korábbi sémák és specifikációk változatlanok.

## Lefedett konfiguráció

A séma a Backend (fiókok, modulok, hozzászólások, letöltések, ütemező, továbbított
fejlécek, korhatárok, auditkeretek), Frontend (webhely, oldalak, galériák,
képforgók, statikus fájlok), Security (fiókok, értesítések, JWT, varázshivatkozások,
jogosultságemelés, adatvédelem, OIDC), Database, Caching, ConnectionStrings, Email,
Serilog naplószintek, OpenTelemetry, Forum és MarkdownViewer beállításait fedi le.
A konvertált értékek JSON-alakja megmarad, a tartományi ellenőrzések továbbra is
irányadók. Egyedi további tulajdonságok támogatottak.

## Modulkapcsolók

A `backend.json` opcionális `Backend.Modules` objektumot fogad, amelyben a leírók
kisbetűs kulcsai JSON-logikai értékekhez tartoznak:

```json
{
  "Backend": {
    "Modules": {
      "contact": true,
      "quotes": true,
      "sport": true,
      "forum": false,
      "markdownviewer": true
    }
  }
}
```

A hiányzó kapcsoló, hiányzó vagy null szakasz engedélyezettet jelent. A Core
kötelező, kikapcsolási kísérlete figyelmen kívül marad. A Beállítások csak a
telepített opcionális modulokat kínálja, ismeretlen vagy Core-kulcs módosítását
elutasítja.

A kapcsolók kiszolgáló-újraindítás nélkül frissülnek. Minden modul-API
`ModuleEndpoint` metaadatot kap; a kikapcsolt kérések és frontend-fájlok a
kezelő meghívása előtt 404 és no-store választ kapnak. A false-ra újratöltött
kapcsoló megszakítja az aktív modulkérések tokenjeit. A már befejezett műveletek
nem vonódnak vissza; a megszakítás együttműködésen alapul.

A `GET /modules` válaszalakja megmarad, de kihagyja a kikapcsolt leírókat.
Útvonalaik, adminisztrációs oldalaik, stílusaik és widgetjeik nem fedezhetők fel.
A nyilvános menühivatkozások, a nyelves és beágyazott hivatkozásokkal együtt,
és az elrendezéswidgetek szűrtek. A közös jelentésbizonyíték-, tartalomnyelv-,
korhatár- és nyerstábla-belépési pontok is ellenőrzik az elérhetőséget.

A telepített katalógus és egyesített EF-modell nem változik. A táblák, fájlok,
verziók, auditnapló, jogosultságok és konfiguráció megmaradnak. Az engedélyezett
fejlesztők továbbra is elérik a modul beállításait a visszakapcsoláshoz. A mentés
frissíti az aktuális böngészőt; más nyitott böngészőkben újratöltés kell a már
megjelenített tartalom eltávolításához. A kiszolgáló a böngésző állapotától
függetlenül a konfiguráció újratöltésekor tiltja a hozzáférést.

## Teljes beállításfelderítés és módosítás

A `GET /settings` Configuration.Read jogosultságot igényel, válasza a
[beállításfelderítés 1.0.0 sémáját](../../schemas/settings-response-1.0.0.schema.json)
követi:

```json
{
  "readOnly": false,
  "files": [{
    "name": "backend.json",
    "editor": null,
    "fields": [{
      "path": "Backend:Scheduler:Enabled",
      "type": "boolean",
      "value": "true",
      "sensitive": false,
      "editable": true,
      "action": null,
      "options": null
    }]
  }]
}
```

Egy létező konfigurációs fájl minden ismert tulajdonsága szerepel, akkor is, ha
hiányzik vagy null. Az alapérték megjelenítése nem ír fájlt. Az ismeretlen,
kitöltött skalármezők elérhetők maradnak. Az e-mail, naplózás/OpenTelemetry, OIDC
és továbbított fejlécek saját szerkesztői megtartják ellenőrzéseiket és titokvédelmüket.

A mezőtípus `string`, `number`, `boolean`, `array` vagy `object`. A felderítő
válaszban az érték továbbra is szöveg; tömbök és leképezések JSON-szöveget
tartalmaznak, ellenőrzött JSON-szerkesztővel. Az opcionális választások
`{ value, label }` elemek. Az `action` null, `generate` vagy `rotate`; a művelettel
kezelt titok nem gépelhető. A tárolt tulajdonságnevek írásmódja megmarad,
ismétlődő útvonal nem keletkezik.

A `PUT /settings/{file}` Configuration.Manage jogosultságot igényel,
`{ "values": { "<kettősponttal tagolt útvonal>": <típusos JSON-érték> } }`
kérést fogad. Csak módosított értékeket ír. Ismert hiányzó mező létrehozható;
ismeretlen hiányzó mező, hibás típus, átfedő vagy csak írásmódban különböző
ismételt útvonal 400 választ kap. A teljes dokumentum ellenőrzése megelőzi a
fájlcserét. Ismeretlen meglévő tömbelemek továbbra is szerkeszthetők. Az appsettings
szerkesztő meglévő naplózás/OpenTelemetry kérésével további skalárváltozásokat is
küld, de ezekkel nem írhatja felül a saját szerkesztővel kezelt szakaszokat.

A titkok maszkoltak. A kapcsolatleképezések megőrzik a változatlan maszkolt
értéket és az üres titokcserét; elem eltávolítása vagy kifejezett null törli azt.
Üres új kapcsolatnál a korábbi tartalékviselkedés marad. Csak írásmódban eltérő
ismételt kapcsolatnevek elutasítottak. Hiányzó JWT-aláírókulcs generálása
létrehozza azt; hiányzó varázshivatkozás-titok forgatása inicializál, megőrizve a
beállított korlátokat. Az automatikus forgatás továbbra is kihagyja a nem
konfigurált hivatkozásokat.

A csak olvasható konfiguráció minden írást és titokműveletet 403-mal utasít el.
A fájl- és jogosultsági hibák korábbi állapotkódjai megmaradnak. Hibás JSON-nál
honosított hiba jelenik meg, módosítási kérés nem indul.

## Alapértékek, kompatibilitás és átállás

A Core-alapértékek a konfigurációs rekordokból származnak, a modulalapértékeket
a séma adja (Forum moderáció: `Post`, dokumentációcím: `Documentation`,
alapértelmezett dokumentum: `README.md`). Az indító generátor modulkapcsolókat
és az alapértelmezett tömörítendő MIME-típuslistát is kiírja.
A `Frontend.StaticFiles.CompressedMimeTypes` most lecseréli az alaplistát,
nem hozzáfűz; kifejezett üres tömb minden típus tömörítését kikapcsolja.
Hiányzó listánál az alapértékek megmaradnak. A korábbi implicit hozzáfűzésre
támaszkodó telepítések sorolják fel az összes kívánt MIME-típust.

Adatbázis-migráció nem szükséges. Régi fájlokból minden új kulcs hiányozhat.
A backend és frontend együtt telepítendő: régi kliensek nem tartalmaznak
tömb-/leképezésszerkesztőt. Az induláskor olvasott adatbázis-, gyorsítótár-,
tanúsítvány-, OIDC- és telemetriabeállítások dokumentált újraindításigénye
megmarad; a modulkapcsolók nem igényelnek újraindítást.

A regressziós tesztek hiányzó alapértékeket, típusellenőrzést, gyűjteményírást,
titokmegőrzést, inicializálást, minden tényleges modulvégpontot és fájlt,
megszakítást, nyilvános menü-/elrendezésszűrést és az adatbázismodell megőrzését
ellenőrzik.
