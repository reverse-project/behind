# MCP és szolgáltatásfelhasználók 1.0.0

Állapot: aktuális. Sémák: [kezelési API](../../schemas/service-users-1.0.0.schema.json),
[JSON-RPC](../../schemas/mcp-1.0.0.schema.json),
[konfiguráció](../../schemas/configuration-1.8.0.schema.json).

## Identitás és tárolás

A `ServiceUser` lezárt `User` altípus, a létrehozójától függetlenül, a
`HumanUser` mintájára. Guid azonosítója EF árnyékkulcs, nem a tartományi rekord
tulajdonsága. A kezelési vetületek külön adják vissza az azonosítót és a
megfigyelt hitelesítőadat-verziót. Saját szerepkörei, engedélyei és korlátozásai
vannak. Létrehozáskor a szokásos Users szerepkört kapja, a létrehozóét nem.
Nem igényel e-mailt, jelszót, születésnapot vagy böngészős munkamenetet.
Időbélyegei az auditnaplóból származnak.

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
A `MaxRequestBytes` és `MaxResponseBytes` pozitív egész, alapértékük 1048576.
A válaszkorlát az alkalmazás API törzsére vonatkozik a JSON-RPC burkolás előtt.
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
- `tools/list`: az alábbi két eszköz.
- `tools/call`: `{name,arguments}`; hibás paraméter: -32602.
- Ismeretlen metódus: -32601. Protokollhiba nem hajt végre parancsot.

Az `api_schema` paraméter nélkül az OpenAPI dokumentumot adja vissza.
Az `api_request` paraméterei: `{method,path,query?,body?}`.
A metódus GET, POST, PUT, PATCH vagy DELETE; path helyi útvonal behelyettesített
útvonalparaméterekkel; query karakterlánc-értékű objektum; body JSON.
Abszolút URL, kódolt útvonal, pontszegmens, beágyazott query és fragment tiltott.

A kérés folyamaton belül a szokásos útválasztáson, hitelesítésen, engedélyezésen,
modulkapcsolókon, nyelvi, életkori és törlési szűrőkön halad át, a szolgáltatás
saját identitásával. Engedélyezési eredmény és parancsválasz nincs gyorsítótárazva.
Nincs külső HTTP-kérés vagy tetszőleges fejléc. X-Content-Locale és Accept-Language
megmarad. Interaktív bejelentkezés, session, profil, alfiók, megszemélyesítés,
MCP és szolgáltatásfelhasználó-kezelés nem hívható. Csak JSON válasz támogatott;
bináris átvitel és átirányítás nem.

Az eszközeredmény `content:[{type:"text",text:"..."}]` és `isError`.
A szöveg JSON `{status,body}`, az API státuszával és szöveges törzsével.
HTTP-hiba isError értéket állít. Túl nagy válasz: 502 és figyelmeztetés, hogy a
módosítás már megtörténhetett. Nincs automatikus újrapróbálás.
A megszakítás továbbadódik; az audit a szolgáltatás saját identitását rögzíti.

## Migráció és kompatibilitás

Engedélyezés előtt futtassa a telepítés laposított SQLite/PostgreSQL migrációit.
Az új identitások mellett a közös hozzáférési, audit- és szerzőhivatkozások a
User hierarchiára bővülnek; a meglévő felhasználók és tagságok megmaradnak.
A TPC polimorf hivatkozás nem rendelkezhet egyetlen konkrét felhasználói táblára
mutató adatbázis-idegenkulccsal; az alkalmazás ellenőrzései maradnak mérvadók.
Az emberi bejelentkezési adatok továbbra is kizárólag HumanUserhez tartoznak.
A backend és frontend együtt telepítendő. Új csomag vagy külső szolgáltatás nem kell.
