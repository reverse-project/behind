# MCP és szolgáltatásfelhasználók 1.1.0

Állapot: aktuális. Ez a kiadás a [MCP és szolgáltatásfelhasználók 1.0.0](./mcp-service-users-1.0.0.md) specifikációját az engedélyezett modulok automatikus eszközeivel bővíti. Az üzenetekhez tartozó [MCP JSON-sémát](../../schemas/mcp-1.1.0.schema.json) ez a dokumentum használja. A [szolgáltatásfelhasználók kezelési sémája](../../schemas/service-users-1.0.0.schema.json) és a konfigurációs séma változatlan.

## Identitás és tárolás

A `ServiceUser` lezárt `User` altípus, a létrehozójától függetlenül, a
`HumanUser` mintájára. Guid azonosítója EF árnyékkulcs, nem a tartományi rekord
tulajdonsága. A kezelési vetületek külön adják vissza az azonosítót és a
megfigyelt hitelesítőadat-verziót. Saját szerepkörei, engedélyei és korlátozásai
vannak. Létrehozáskor a szokásos Users szerepkört kapja, a létrehozóét nem.
Nem igényel e-mailt, jelszót, születésnapot vagy böngészős munkamenetet.
Létrehozása és módosításai naplózottak; az időbélyegek az auditnaplóból származnak.

A kulcs az identitást és 256 véletlen bitet tartalmaz. Csak SHA-256 lenyomata
kerül tárolásra; ellenőrzése állandó idejű összehasonlítással történik.
A nyers kulcs csak létrehozáskor és cserénél jelenik meg egyszer.
A lecserélt kulcs, letiltott vagy törölt identitás nem hitelesíthet.
A visszaállítás nem éleszti újjá a régi kulcsot.
A hozzáférési módosítások új kulcs nélkül érvényesek a következő műveletnél.

## Beállítások

A `Backend.Accounts.ServiceUser` és `Backend.Mcp.Enabled` értékének is igaznak
kell lennie. Mindkettő alapértéke false; újraindítás nélkül újratöltődnek.
A hiányzó beállítások a meglévő telepítéseken letiltott állapotot jelentenek.
A `Backend.Mcp.MaxRequestBytes` és `MaxResponseBytes` pozitív egész,
alapértékük 1048576. A válaszkorlát az alkalmazás API törzsére vonatkozik a
JSON-RPC-válaszba csomagolás előtt.
Az `AllowedOrigins` alapértéke üres tömb. Origin nélküli kérés elfogadható;
a megadott Originnek pontosan egyeznie kell egy engedélyezett értékkel.

A backend Beállítások lapján találhatók a kapcsolók és az identitások kezelése.
Olvasáshoz Configuration.Read szükséges. Létrehozás, hozzáférésszerkesztés,
kulcscsere és engedélyezés: Configuration.Manage és User.Manage.
Törlés: Configuration.Manage és User.Delete. Visszaállítás és végleges törlés:
Configuration.Manage és fejlesztői hozzáférés. Megszemélyesített felhasználó
nem módosíthat, az írásvédett konfiguráció minden kezelési írást tilt.
Nem adható meg nem birtokolt engedély, és magasabb hozzáférésű identitás sem
kezelhető. A kulcs listában és szokásos válaszban nem jelenik meg, a böngésző
nem tárolja tartósan.

## Kezelési HTTP API

Minden válasz no-store. Hiányzó hitelesítés: 401; elégtelen hozzáférés: 403.
A JSON camel-case tulajdonságokat használ.

| Metódus és útvonal | Kérés | Siker |
| --- | --- | --- |
| GET `/service-users?deleted=false` | nincs | 200 `{users,canManage,canDelete,isDeveloper}` |
| GET `/service-users/{id}` | nincs | 200 `{id,version,name,enabled}` |
| POST `/service-users` | `{name}` | 201 `{user,apiKey}`, Location fejléc |
| PUT `/service-users/{id}` | `{version,enabled}` | 204 |
| POST `/service-users/{id}/rotate` | `{version}` | 200 `{user,apiKey}` |
| GET `/service-users/{id}/access` | nincs | 200 meglévő UserAccessResponse |
| PUT `/service-users/{id}/access` | `{roleIds,grants,restrictions}` | 204 |
| POST `/service-users/{id}/delete` | `{version}` | 204 |
| POST `/service-users/{id}/restore` | nincs | 204 |
| POST `/service-users/{id}/hard/delete` | nincs | 204 |

A `user` alakja `{id,version,name,enabled}`. Hibás név vagy kiválasztás: 400;
hiányzó erőforrás keresésnél: 404; elavult verzió: 409.
A törölt lista fejlesztői jogosultsághoz kötött. A Users szerepkör kötelező.
A hozzáférésszerkesztés megőrzi a meglévő jogosultságemelési ellenőrzéseket.
A végleges törlés 409 választ ad, ha a megőrzött auditnapló hivatkozik a felhasználóra.

## MCP Streamable HTTP

A `/mcp` állapotmentes Streamable HTTP végpont. Minden POST, GET és DELETE
kérés `Authorization: Bearer <service-key>` fejlécet igényel.
A kulcs nem böngészős JWT, és közvetlen alkalmazás-API-khoz nem használható.
Letiltva: 404; hibás vagy hiányzó kulcs: 401 és WWW-Authenticate;
nem engedélyezett Origin: 403. Helyi fejlesztésen kívül HTTPS szükséges.

POST: Content-Type application/json, Accept application/json és text/event-stream.
A szerver JSON-t ad, nem SSE-t. GET és DELETE: 405, mert nincs szerverstream vagy
tartós szállítási munkamenet. Nincs Mcp-Session-Id.
Támogatott verziók: 2025-11-25, 2025-06-18, 2025-03-26.
Ismeretlen inicializálási verzióval 2025-11-25 kerül kiválasztásra;
ismeretlen MCP-Protocol-Version fejléc: 400.

POST-onként egy JSON-RPC 2.0 objektum küldhető, nem köteg.
Az id karakterlánc vagy szám, és változatlan marad.
Duplikált tulajdonság vagy hibás üzenet: 400/-32600;
hibás JSON: 400/-32700; túl nagy kérés: 413;
nem támogatott tartalomtípus: 415; hibás Accept: 406.
A `notifications/` metódusok 202 választ kapnak törzs nélkül,
és soha nem hajtanak végre eszközt.

Támogatott kérések:

- `initialize`: protocolVersion, capabilities és clientInfo objektum szükséges;
  válasz: protocolVersion, serverInfo, tools képesség és utasítások.
- `ping`: üres eredmény.
- `tools/list`: a két beépített eszközt és minden jelenleg engedélyezett modul eszközeit adja vissza.
- `tools/call`: `{name}` szükséges; az `arguments` elhagyható olyan eszköznél,
  amelynek nincs kötelező argumentuma, például az `api_schema` vagy egy
  törzs és paraméter nélküli modulolvasás esetén. Hibás paraméter, ismeretlen vagy
  letiltott modul-eszköz: -32602.
- Ismeretlen metódus: -32601. Protokollhiba nem hajt végre parancsot.

A beépített `api_schema` paraméter nélkül az OpenAPI dokumentumot adja vissza.
Az `api_request` paraméterei: `{method,path,query?,body?}`.
A metódus GET, POST, PUT, PATCH vagy DELETE; path helyi útvonal behelyettesített
útvonalparaméterekkel; query karakterlánc-értékű objektum; body JSON.
Abszolút URL, kódolt útvonal, pontszegmens, beágyazott query és fragment tiltott.
Mindkét beépített eszköz változatlanul elérhető.

### Automatikusan felderített moduleszközök

Az MCP minden telepített és engedélyezett modul alkalmas JSON-műveleteit a
modul `ModuleEndpoint` útvonal-metaadataiból és az ASP.NET Core API Explorer
metaadataiból deríti fel. A `tools/list` minden kéréskor beolvassa a jelenleg
engedélyezett modulokat: az újonnan engedélyezett modul eszközei kapcsoló nélkül
megjelennek, a letiltottaké eltűnnek. Nincs külön MCP-konfigurációs kapcsoló.

A műveletek neve:
`<modul>_<kisbetűs HTTP-metódus>_<a modul előtagja utáni útvonalszegmensek>`.
Az útvonalparaméterek szegmense `by_<paraméternév>` alakú; a modulgyökér
műveletneve csak `<modul>_<metódus>`. Például:
`forum_get_posts`, `forum_get_posts_by_id`, `forum_post_posts`, `quotes_get`,
`sport_get_teams` és `markdownviewer_get_documents_by_path`.

Minden művelet objektum típusú `inputSchema` sémája csak az útvonal, lekérdezés
és JSON-törzs bemeneteit írja le. Útvonalparaméterek esetén a `route` objektum
szerepel és kötelező; értékei karakterláncok. A catch-all útvonalérték `/`
karaktert is tartalmazhat. A `query` objektum API Explorer-lekérdezési
paramétereket ír le az API-típusukkal (karakterlánc, szám vagy logikai érték).
A `body` tulajdonság a kérés típusának az alkalmazás beállított JSON-szerializálója
szerinti sémáját használja. A kötelező mezők az API paraméterkötést követik.
Ha egy bemenettípus nem tartozik az útvonalhoz, a megfelelő tulajdonság elmarad.

Csak a szokásos JSON-műveletek érhetők el. Ide tartoznak a nyilvános és kezelési
műveletek, a moderálás, az átmeneti törlés, a visszaállítás és a végleges törlés;
ezekre is a meglévő engedélyezési szabályok érvényesek. A multipart/fájl-feltöltés
és a bináris letöltés kimarad. Az API Explorer válaszmetaadatai azonosítják a
JSON-műveleteket; a végpont tényleges JSON-válasz DTO-ját `.Produces<T>()`
deklarációval kell megadni, a bináris válaszokhoz pedig nem JSON típusú
válaszmetaadatot kell használni. A törzs nélküli sikeres műveletek, például a 204
választ adó életciklus-műveletek a sikeres státusz-metaadatuk alapján
(`.Produces(204)`) továbbra is elérhetők; ezekhez nem kell JSON DTO, és a
sikeres eszközeredmény státuszt és üres törzsszöveget tartalmaz.
Az alkalmazáshibák eszközhibák maradnak, nem alakulnak át sikeres eredménnyé.

Az eszközfelderítés nincs a szolgáltatásfelhasználó engedélyei szerint szűrve,
így a séma az aktuális engedélyezett API-felületet írja le, nem jogosultsági
pillanatfelvételt. Minden hívás egyszer, a normál alkalmazás-útválasztáson és
middleware-en halad át, beleértve a modul elérhetőségét, hitelesítést,
engedélyezést, nyelvi, életkori és törlési ellenőrzéseket, a szolgáltatás saját
identitásával. A szükséges élő engedéllyel nem rendelkező hívó a szokásos tiltott
eredményt kapja. A modul letiltásakor eszközei eltűnnek; az elavult vagy ismeretlen
eszköznév -32602 hibát ad. A módosító művelet egyszer kerül végrehajtásra,
automatikus újrapróbálás nélkül.

Az elosztás folyamaton belül történik. Engedélyezési eredmény és
parancsválasz nincs gyorsítótárazva. Nincs külső HTTP-kérés vagy tetszőleges
fejléc. Az X-Content-Locale és az Accept-Language megmarad. Interaktív
bejelentkezés, munkamenet, profil, alfiók, megszemélyesítés, MCP- és
szolgáltatásfelhasználó-kezelés nem hívható. Átirányítás és bináris válasz nem támogatott.

Az `api_request` és a moduleszközök eredménye egyaránt
`content:[{type:"text",text:"..."}]` és `isError`. A szöveg JSON
`{status,body}`, az alkalmazás HTTP-státuszával és karakterláncként megadott
választörzsével. Például a törzs nélküli 204 siker szövege
`{"status":204,"body":""}`, nem üres tartalomtömb. A HTTP-hiba `isError`
értéket állít, nem jeleníti meg sikeres műveletként. A konfigurált méretkorlátot
meghaladó alkalmazásválasz 502-es státuszt ad, és jelzi, hogy a módosítás már végrehajtódhatott.
Nincs automatikus újrapróbálás. A megszakítás továbbadódik az alkalmazásműveleteknek;
az audit a szolgáltatás saját identitását rögzíti.

## Migráció és kompatibilitás

Az 1.1.0 az MCP-eszközök felületét bővíti: az `api_schema`, az `api_request`,
a szolgáltatásfelhasználó-kezelés, a hitelesítés, az átvitel és a konfiguráció
változatlan. Az ügyfeleknek dinamikusnak kell tekinteniük a `tools/list` választ,
és nem feltételezhetik, hogy pontosan két eszköz van benne. A meglévő
szolgáltatásfelhasználó-kezelő ügyfelek és 1.0.0-sémáik továbbra is kompatibilisek.
A modulműveletek megjelenítéséhez az alkalmazás azon verziója szükséges, amely
tartalmazza a modul útvonalait és API Explorer-metaadatait. Nincs adatbázis-migráció,
új beállítás vagy külső szolgáltatás.

Az MCP engedélyezése előtt futtassa a meglévő, laposított SQLite/PostgreSQL
környezet migrációit. Ezek szolgáltatásidentitásokat adnak hozzá, és a közös
felhasználói hozzáférési, audit- és szerzőhivatkozásokat a User hierarchiára
bővítik, megőrizve a meglévő emberi felhasználókat és tagságokat. A TPC polimorf
hivatkozás nem rendelkezhet egyetlen konkrét felhasználói táblára mutató
adatbázis-idegenkulccsal; az alkalmazás identitás-ellenőrzései maradnak mérvadók.
Az emberi bejelentkezési adatok továbbra is kizárólag HumanUserhez tartoznak.
A backend és frontend együtt telepítendő. Új csomag vagy külső szolgáltatás nem kell.
