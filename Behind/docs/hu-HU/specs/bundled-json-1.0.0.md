# Beépített frontend JSON 1.0.0

Állapot: aktuális. Az első közzétett szerződés a meglévő beépített statikus
rekordokat írja le, nem konfigurációt, importálási/exportálási dokumentumot vagy
API-kérést/választ. Fordításkori regressziós ellenőrzést ad hozzá, nem futásidejű
kérésellenőrzést.

## Sémák és fájlok

Minden séma JSON Schema Draft 2020-12 formátumú, és a `Behind.Common` beágyazott
erőforrása.

| Beépített fájl a projekt `Frontend` könyvtárához képest | Séma |
| --- | --- |
| `changelog.json` | [changelog-1.0.0.schema.json](../../schemas/changelog-1.0.0.schema.json) |
| `licenses.json` | [licenses-1.0.0.schema.json](../../schemas/licenses-1.0.0.schema.json) |
| `locales/*.json`, `modules/<kulcs>/locales/*.json` | [locale-1.0.0.schema.json](../../schemas/locale-1.0.0.schema.json) |

A `BundledJsonSchemaTests` felderíti a mag és a modulok frontendkönyvtárainak
összes érintett forrásfájlját, és a beágyazott sémával ellenőrzi őket. Hibás
minták ellenőrzik a kötelező mezőket, típusokat és szótárértékeket.

## Változásnapló

Kiadásobjektumok nem üres tömbje. Minden kiadás kötelező mezője a nem üres,
nem csak térközökből álló szöveges `version` és `date`, valamint a `changes`
tömb. Minden változás kötelező mezője a nem üres szöveges `type` és `text`.
Fejlesztési kiadásnál az üres változáslista érvényes. Nincs új szemantikusverzió-
vagy dátumformátum-megkötés: a kiadási jegyzetek generátora nem üres szövegeket
fogad el. A változástípusok nem felsorolásértékek: az ismeretlen típusokat a
frontend szó szerint jeleníti meg, és a kiadási jegyzetek generátora is támogatja.

A meglévő változásnaplótesztek ezen felül ellenőrzik az aktuális verziót, a
verziók egyediségét és a korábbi kiadások nem üres változáslistáját.

## Licencek

Nem üres tömb. Minden rekord kötelező, nem üres szöveges mezői: `name`,
`version`, `license`, `copyright`, `url`, `scope` és `text`. A `scope` pontosan
`frontend`, `server` vagy `development`. Az opcionális `note` szöveg vagy null.
A verziószövegnek nem kell szemantikus verziónak lennie; az URL és a
licencszöveg útvonala megőrzi meglévő szöveges alakját, új formátum- vagy
útvonalmegkötés nélkül.

A meglévő licenctesztek továbbra is ellenőrzik a csomagok lefedettségét, a
név/verzió párok egyediségét, a GPL-kompatibilis licencazonosítókat és a
licencszöveg-erőforrások létezését. Ezeket a fájlok közötti és jogi
ellenőrzéseket a séma nem helyettesíti.

## A mag és a modulok nyelvi fájljai

Mindkettő ugyanazt az objektumsémát használja. A kötelező `messages` tetszőleges
kulcsokat rendel szöveges fordításokhoz. Az üres szótár és az üres fordítás
érvényes; a pontozott kulcsok, Unicode és a `{0}` jellegű helyőrzők megmaradnak.
A beágyazott üzenetobjektumok, tömbök, számok, logikai és null értékek hibásak.

Az opcionális `name` és `flag` szöveg vagy null. A hiányzó, null, üres vagy csak
térközös metaadat érvényes: a nyelvkatalógus hiányzó/üres név esetén a fájl
nyelvi kódját használja, hiányzó/üres zászló esetén pedig nem ad zászlót.
A modulok nyelvi fájljaiban nem kötelező zászló. A nyelvi kódot a fájlnév adja;
annak ellenőrzése és a fordítás teljessége külön feladat marad.

## Kompatibilitás és átállás

Mindhárom séma megengedi a további rekordtulajdonságokat, összhangban az
ismeretlen metaadatokat figyelmen kívül hagyó fogyasztókkal. Teljes beépített
rekordokat írnak le, nem minden olyan hibás bemenetet, amelyet a frontend
védekező kódja esetleg eltűr. A fordítások ezért szövegszótárak, a
változásnapló kiadásmezői pedig megfelelnek a kiadási jegyzetek generátorának.

Nincs szükség a beépített adatok módosítására vagy migrációra. A meglévő tömbök
és nyelvi objektumok nem kapnak sémaverziómezőt; a verzió a sémafájlhoz és ehhez
a specifikációhoz tartozik. A szerződés későbbi változtatásaihoz új szemantikus
verziót kell közzétenni, a jelenlegi sémák és specifikáció megtartásával.
