<!-- coderabbit-ai-generated:v1 -->

# Konfigurációs JSON ellenőrzése 1.4.0

Állapot: aktuális. Ez a kiegészítő változat az [1.3.0 futásidejű konfigurációsséma-szerződést](./configuration-json-1.3.0.md) bővíti.

## Definíció és lefedettség

A [JSON Schema 2020-12](../../schemas/configuration-1.4.0.schema.json) a JSON-alapú
konfigurációs rekordokat, beágyazott rekordokat és konvertált értékeket írja le:
Caching, Database, Backend (fiókok, hozzászólások, korhatár, letöltések, ütemező),
Frontend (site, sites, galériák, képforgók, statikus fájlok), Security (JWT,
jogosultságemelés, adatvédelem, mágikus linkek, OIDC), Email/hitelesítés/sablonok,
Serilog minimumszintek, OpenTelemetry exportálók/naplózás, Forum és MarkdownViewer.
A névvel ellátott `$defs` a rekordokat írja le; a konvertált értékek tényleges
formátuma megmarad. A ConnectionStrings a Database és Caching testvérszakasza.

## Futásidő és kompatibilitás

A Common beágyazza a közzétett sémákat; a `JsonSchemas` megőrzi a lefordított
ellenőrzőket, felhasználói URL-ről nem tölt le sémát. A `ConfigurationSchema`
a teljes tervezett dokumentumot az eredeti írási zároláson belül, bármely bájt
lecserélése előtt ellenőrzi. Az induló konfiguráció generálása is ellenőrzött.
Az általános Beállítások, e-mail, naplózás/OpenTelemetry, OIDC, titokgenerálás és
automatikus mágikuslink-forgatás ugyanazt az írót használja. Sémahiba esetén
a beállítási API 400 választ és értékmentes naplóüzenetet ad; a belső író
`ConfigurationSchemaException` kivételt dob. Elutasításkor a fájl változatlan.

A konfigurációs tulajdonságnevek ellenőrzése nem kisbetűérzékeny, a tárolt
dokumentum és a szótárkulcsok módosítása nélkül. A hiányzó mezők megőrzik
alapértékeiket. Az opcionális rekordok és értékek a meglévő kötés szerint null
értéket is elfogadnak. A további mezők megmaradnak a konfigurációs rendszer és
modulbővítések kompatibilitásához. A számok JSON-számok maradnak, negatív és tört
értékekkel is, az általános Beállítások szerkesztőjének megfelelően, új egészszám-
vagy tartománykorlátozás nélkül. A futásidejű alapértékek és korlátozások maradnak.
A tartományellenőrzés, nyelvfelderítés, jogosultságok, csak olvasható állapot,
titokmaszkolás és külön szerkesztőellenőrzések továbbra is érvényesek.

A séma az ismert rekordok alakját ellenőrzi; nem állítja, hogy minden szöveg
használható URI, titok, telepített nyelv, cron-kifejezés vagy szolgáltató.
Ezeket a meglévő konverterek és alkalmazásellenőrzők vizsgálják.
A sémán kívüli egyedi modulszakaszok bővíthetők; új ismert rekordhoz ugyanabban
a módosításban séma és lefedettségi teszt szükséges.

## Kompatibilitási bizonyíték

A `ConfigurationSchemaTests` ellenőrzi a generált fájlokat, a rekordok és mezők
reflexiós lefedettségét, az általános Beállítások elfogadott/elutasított értékeit
és a fájl változatlanságát elutasításkor. A meglévő beállítási tesztek bekapcsolt
ellenőrzéssel futnak. Az összevetés szerint az SMTP Templates szótárként,
nem tömbként szerializálódik; a séma javítva lett. A null gyorsítótárszakasz és
névvel ellátott null kapcsolat megőrzi a memóriás visszaesést;
a [gyorsítótár 1.1.0](./caching-configuration-1.1.0.md) a régi dokumentációs
sémát a konfiguráció szintaxisának változtatása nélkül javítja.

Nincs fájlmigráció. A korábban hibás ismert mezőt a teljes dokumentum mentése
előtt javítani kell. A korábbi specifikációk megmaradnak. A sémajavítások is
verziózottak; közzétett szerződést nem írunk felül.

## Az auditkeresés erőforráskorlátai

A `backend.json` `Backend` szakasza két opcionális numerikus mezővel bővül:

| Tulajdonság | Alapérték | Jelentés |
| --- | --- | --- |
| `AuditSearchMaxEntries` | 10000 | A címkekeresés során átvizsgálható auditbejegyzések legnagyobb száma. |
| `AuditSearchMaxCandidates` | 10000 | A kért címkekeresési laphoz tárolható jelöltablak legnagyobb mérete. |

A rendszer minden keresésnél a konfigurációból olvassa az értékeket, futásidőben
legalább 1-re igazítva őket. A beolvasási keretet túllépő keresés vagy a jelöltkereten
túli lap kérése HTTP 400 választ ad hiányos sikeres eredmény helyett. A séma mindkét kerethez 32 bites előjeles egész számot fogad el.
Ha az érték futásidőben nem alakítható egész számmá, az alapérték érvényesül.

Ez kiegészítő konfigurációváltozás: a mezőket elhagyó fájlok az alapértékeket kapják,
a meglévő telepítések fájljait nem kell migrálni. Az induló generálás mindkét alapértéket
tartalmazza. Az 1.0.0 dokumentumok és sémák változatlanok; a futásidejű ellenőrzés
az 1.2.0 sémát használja. A korábban nagyobb audithalmazokat kereső telepítéseknél
szűkíteni kell a keresést, vagy tudatosan növelni kell a konfigurált kereteket.
A korlátozott keresés biztonsági javítás.

## A galéria képpontkerete

A `Frontend:Galleries:MaxDecodedPixels` mező előjeles Int64-ellenőrzése és mentés előtti normalizálása az [1.0.2 konfigurációból](./configuration-json-1.0.2.md) változatlanul érvényes. Hiányzó vagy nem pozitív értéknél továbbra is az alapértelmezés érvényesül.

## Megbízható proxyk

A `backend.json` opcionális `Backend:ForwardedHeaders` szakaszának mezői: `Enabled` (alapérték: false), `KnownForwarders` (alapérték: üres szövegtömb) és `ForwardLimit` (pozitív egész, alapérték: 1). Migráció nem szükséges. A beállítások a `GET` és `PUT /settings/forwarded-headers` végponton érhetők el, az `enabled`, `knownForwarders` és `forwardLimit` JSON-mezőkkel. A módosítások a konfiguráció újratöltésekor lépnek életbe.

## Fiókcsaládok

A `security.json` fájlban a `Security.Accounts.Subaccounts` alapértéke igaz. Kikapcsolásával a család kezelése és a fiókváltás letiltható. A `Security.Accounts.LinkUnlink` alapértéke hamis. A meghíváshoz, jóváhagyáshoz és leválasztáshoz mindkettőt engedélyezni kell. A meglévő fájlokból az Accounts objektum elhagyható. A kikapcsolás megőrzi a tárolt kapcsolatokat.

Az opcionális `Security.Accounts.SwitchPinMaxLength` beállítás 1 és 128 közötti egész számot fogad el, az alapértéke 128. A korlát a létrehozáskor megadott mindkét PIN-kódra, a PIN-kód módosítására és a fiókváltásra érvényes. A Beállítások felületen mentett JSON-ban érvényes érték szükséges. A kézzel szerkesztett fájlban a tartományon kívüli egész szám helyett az alapérték érvényes. A korlát csökkentése megőrzi a tárolt PIN-kódokat, de hosszabb PIN-kóddal a fiókváltás 400-as hibát ad. Az elsődleges fiók tulajdonosa a profiljában rövidebbre cserélheti az alfiók PIN-kódját. A korlátnál hosszabb elsődleges PIN-kód a korlát növelésével használható újra. A teljes bejelentkezés továbbra is elérhető. Adatmigráció nem szükséges.
