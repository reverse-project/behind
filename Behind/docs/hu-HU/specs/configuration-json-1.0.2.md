<!-- coderabbit-ai-generated:v1 -->

# Konfigurációs JSON ellenőrzése 1.0.2

Állapot: aktuális. Az [1.0.1](./configuration-json-1.0.1.md) utódja, mezőspecifikus Int64-javítással. A korábbi specifikációk és sémák változatlanok.

## Definíció és lefedettség

A [JSON Schema 2020-12](../../schemas/configuration-1.0.2.schema.json) a JSON-alapú
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
tulajdonságnevek és szótárkulcsok módosítása nélkül. A közös író mentés előtt
Int64 JSON-számmá normalizálja a MaxDecodedPixels egész értékeit. A hiányzó mezők megőrzik
alapértékeiket. Az opcionális rekordok és értékek a meglévő kötés szerint null
értéket is elfogadnak. A további mezők megmaradnak a konfigurációs rendszer és
modulbővítések kompatibilitásához. A többi szám JSON-szám marad, negatív és tört értékekkel is. Kizárólag a
MaxDecodedPixels mező követel meg az előjeles Int64 tartományába eső egészet
(-9223372036854775808 és 9223372036854775807 között).
A futásidejű alapértékek és korlátozások maradnak.
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

A meglévő egész képpontkeretek kompatibilisek. Az 1.0.1 séma által korábban
elfogadott tört vagy tartományon kívüli MaxDecodedPixels értéket mentés előtt
Int64 értékre kell cserélni vagy el kell távolítani. A hibás ismert mezőket javítani kell. A korábbi specifikációk megmaradnak. A sémajavítások is
verziózottak; közzétett szerződést nem írunk felül.

## A galériaképek dekódolási kerete

A `frontend.json` `Frontend:Galleries:MaxDecodedPixels` Int64 egész beállításának alapértéke 4 000 000 képpont. Hiányzó vagy nem pozitív értéknél a `GalleriesConfiguration.DecodePixelLimit` ezt az alapértéket használja. A korlát a galéria bélyegképeihez, borítóihoz és generált mozaikjaihoz dekódolt egyes bitképekre vonatkozik. A dekódolás a kért kimenethez megfelelő méretet választ, és elutasítja a képet, ha a kodek nem tud a megadott képpontkereten belül dekódolni. Változatlan tájolásnál nem készül további teljes méretű másolat.

A `MaxImageBytes` továbbra is ettől függetlenül a tömörített feltöltés bájtjait korlátozza; a meglévő 10 MiB alapérték és a nem pozitív értékek visszaesése változatlan. A képpontkeret növelése növeli a natív memóriaigényt, beleértve a tájolás és a mozaik köztes bitképeit. A beállítás bitképenkénti korlát, nem a teljes folyamat memóriájának vagy párhuzamosságának korlátja.

A bővítés kompatibilis a meglévő konfigurációs fájlokkal: nem szükséges migráció, hiányzó értéknél a biztonságosabb alapérték lép életbe. Korábban elfogadott képeknél meghiúsulhat a bélyegkép létrehozása, ha a kodek nem tud a kereten belül dekódolni. Az üzemeltető a rendelkezésre álló memória mérlegelése után nagyobb pozitív értéket állíthat be. A generált induló konfiguráció tartalmazza a `MaxDecodedPixels` mezőt; az általános Beállítások szerkesztő meglévő konfigurációs jogosultságokkal jeleníti meg. A 4000000.0 és 4e6 alakok matematikai egészként elfogadhatók; az író 4000000 alakban tárolja őket, hogy a long konfigurációs kötés újratöltéskor is működjön. A tört és az Int64 tartományán kívüli értékeket az író a fájl módosítása előtt elutasítja. A hiányzó és nem pozitív egész értékek alapértékre esnek vissza. Ez a korábbi séma elfogadási szerződését javítja; a futásidejű képpontkeret és a többi numerikus mező változatlan.
