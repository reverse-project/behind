# Behind OIDC bejelentkezés 2.0.0

Állapot: megvalósítva. A specifikáció Semantic Versioning 2.0.0 szerint verziózott, az alkalmazástól és az Auditortól függetlenül. Felváltja a történeti [1.1.0](./oidc-sign-in-1.1.0.md) változatot. A 2.0.0 a helyi szerepköröket pontos, kis- és nagybetűérzékeny egyezéssel keresi; az átadási és beállítási JSON alakja változatlan. A [JSON-séma](../../schemas/oidc-sign-in-2.0.0.schema.json) tartalmazza az átadási és beállítási formátumokat.

## Globális fiók-bejelentkezési kapcsoló

A `backend.json` fájl `Backend:Accounts:Oidc` kapcsolója az **összes** OIDC szolgáltatót engedélyezi vagy tiltja, a Password, MagicLink és más módoktól függetlenül:

```json
{"Backend":{"Accounts":{"Oidc":true}}}
```

Alapértéke true, a korábban konfigurált szolgáltatók működésének megőrzésére. Szolgáltatók nélkül az OIDC a kapcsoló állásától függetlenül nem használható. A Beállítások **backend.json > Backend > Accounts > Oidc** mezője akkor is megjelenik, ha a fájlból hiányzik; mentése felveszi a logikai értéket. A normál konfigurációfigyelő újraindítás nélkül frissíti, lekérdezéses fájlfigyelésnél néhány másodperces késéssel. A `GET /accounts/settings` válasz tartalmazza az `oidc` logikai értéket.

False esetén a nyilvános szolgáltatólista üres; a bejelentkezés indítása és a függő átadási ticket beváltása 404-et ad; a szolgáltatói callback általános hibára irányít; a függő OIDC második hitelesítési lépés elutasított. Új OIDC munkamenet nem készül. A jelszavas hitelesítés, korábbi munkamenetek és szerepkörök nem változnak.

## Szolgáltatószerkesztő és mentés

A **security.json** Beállítások fül megtartja az általános biztonsági mezőket, Generálás és Forgatás műveleteket, és külön **OpenID Connect szolgáltatók** űrlapot kap. Szolgáltatók hozzáadhatók, szerkeszthetők és eltávolíthatók, egyenként engedélyezhetők; állíthatók a scope-ok, fiók-összekapcsolás/létrehozás, szerepkör-claim útvonal, kifejezett leképezések, automatikus szerepkör-létrehozás, időkorlátok és ticketkapacitás. A globális kapcsoló nem a `Security:Oidc` részen tárolódik.

- A `GET /settings/security.json/oidc` `Configuration.Read` jogosultságot igényel, és az alábbi konfiguráció camelCase JSON alakját adja. A `clientSecret` mindig üres, a `hasClientSecret` jelzi a tárolt titok meglétét, a `clearClientSecret` false. Valódi vagy maszkolt titoktartalom sem kerül a válaszba.
- A `PUT /settings/security.json/oidc` `Configuration.Manage` jogosultsággal lecseréli a szolgáltatólistát. Üres `clientSecret` a pontos állandó szolgáltató-ID alapján őrzi meg a korábbi titkot, nem a sor pozíciója szerint. Nem üres érték lecseréli, `clearClientSecret=true` törli. Egyidejű törlés és csere érvénytelen. A kapcsolódástól független és ismeretlen JSON tulajdonságok megmaradnak, a megtartott szolgáltatóké is. A titokállapot-/törlésjelző felületi mezők nem tárolódnak.
- Sikeres mentés 204; hibás szolgáltató, ismételt ID, leképezés, scope vagy korlát 400, írás nélkül. Csak olvasható konfiguráció 403, hiányzó security.json 404. Jogosulatlan kérés a meglévő szabályok szerint 401/403. A válaszok `Cache-Control: no-store` fejlécet kapnak; a hibák a meglévő hibakezelést követik, a napló nem tartalmaz titkot.
- A szolgáltató- és korlátmódosítások újraindítást igényelnek. A mentés nem indítja újra a folyamatot és nem regisztrál dinamikusan hitelesítési sémát. Az általános biztonsági és szolgáltatói szerkesztő nem mentett módosításai függetlenek. Más ID új identitásnévteret jelent; a meglévő azonosító maradjon állandó.

## Szolgáltatókonfiguráció

A `security.json` fájl `Security` szakasza alatt adja meg, a valódi titkot védett telepítési konfigurációból biztosítsa, majd indítsa újra:

```json
{
  "Oidc": {
    "TicketLifetime": "00:02:00",
    "AuthenticationTimeout": "00:10:00",
    "MaxPendingTickets": 1000,
    "Providers": [{
      "Id": "company", "DisplayName": "Company account", "Enabled": true,
      "Authority": "https://identity.example.org/realms/company", "ClientId": "behind",
      "Scopes": ["openid", "profile", "email"],
      "AutoCreateUsers": true, "LinkByVerifiedEmail": true,
      "RoleClaimType": "realm_access.roles",
      "RoleMappings": {"content-editors": ["Editor"]},
      "AutoCreateRoles": true, "AutoCreateRolePrefix": "company:"
    }]
  }
}
```

Több objektum független szolgáltatókat jelent. Az ID egyedi ASCII betű/szám/aláhúzás/kötőjel sorozat; kis-/nagybetűben eltérő másolat tiltott. Engedélyezett szolgáltatóhoz név, hitelesítő adat/query/fragment nélküli HTTPS authority, kliensazonosító, nem üres szerepkör-claim és `openid` scope szükséges. Hibás konfiguráció megállítja az indulást. Nyilvános kliensnél a `ClientSecret` opcionális; valódi titkot ne tároljon forráskódban. Egyedi `Enabled` alapértéke true; scope-ok `openid`, `profile`, `email`; automatikus fióklétrehozás true; e-mailes kapcsolás és automatikus szerepkör-létrehozás false; szerepkör-claim `roles`; nincs leképezés. Az automatikus előtag `oidc:<provider-id>:`. Az időkorlátok és kapacitás pozitívak.

A szolgáltató ID tokenje tartalmazza az `email`, `email_verified`, opcionális `name` és kiválasztott szerepkör claimet. A Behind nem hív UserInfo-t és nem feltételez megerősítést az e-mail puszta meglétéből. Konfigurált szolgáltatóknál a backend és `Frontend:Url` HTTPS-t igényel akkor is, ha a globális mód éppen ki van kapcsolva, hogy újraindítás nélkül engedélyezhető legyen.

## Böngészős bejelentkezés

A `GET /sign/in/oidc/providers` engedélyezett `[{id, displayName}]` listát ad, titok és leképezés nélkül. Letiltott globális bejelentkezés, OIDC vagy hiányzó JWT esetén üres. A böngészős `GET /sign/in/oidc/<id>` authorization code folyamatot indít PKCE-vel (302); ismeretlen/tiltott szolgáltató 404, HTTPS nélkül 400. Secure, HttpOnly, SameSite=None, folyamatonkénti süti köti a böngészőt. A pontos `https://<backend>/sign/in/oidc/callback/<id>` címet regisztrálja.

A standard ASP.NET Core middleware ellenőrzi a védett state-et, korrelációt, nonce-ot, aláírást, issuert, audience-t és élettartamot, majd beváltja a kódot. A tokenek a szerveren maradnak és nem mentődnek. Siker csak a konfigurált `Frontend:Url#/signin?oidc=<opaque-ticket>` címre irányít, hiba `#/signin?oidcError=failed` címre. A frontend törli az URL-ticketet, majd az eredeti sütivel `{ticket}` kérést küld a `/sign/in/oidc/redeem` címre. A ticket egyszer használható, böngészőhöz kötött, időben/kapacitásban korlátozott, folyamaton belüli. Hibás/lejárt/újrajátszott/nem kötött ticket 401, hibás kérés 400. URL nem tartalmaz hozzáférési/frissítési tokent. Azonos eredet ajánlott; más eredethez kifejezett hitelesített CORS és engedélyezett süti kell. Több példánynál sticky routing szükséges; újraindítás érvényteleníti a függő átadásokat.

## Fiókok, szerepkörök és válaszok

Első bejelentkezéshez érvényes e-mail és `email_verified=true` kell. Automatikus létrehozáshoz `Backend:Accounts:SignUp` is szükséges; alap Users szerepkör és moderált jóváhagyás érvényes. Meglévő címhez kapcsoláshoz `LinkByVerifiedEmail=true` és a pontos helyi cím megerősítése is kell. Csak megbízható szolgáltatónál engedélyezze. Később a tárolt szolgáltató/ellenőrzött issuer/subject azonosít, nem a módosult e-mail. Puhán törölt felhasználó nem kapcsolható csendben máshová; végleges törlés eltávolítja az infrastruktúra-bejegyzéseket.

A szerepkör lehet ismételt szöveg, JSON szövegtömb vagy pontozott JSON útvonal. Hibás alak kifejezett hiba. A külső kulcsok és helyi szerepkörnevek pontosan, kis- és nagybetűérzékenyen illeszkednek. Kifejezett cél létező aktív szerepkör; tudatos leképezés adhat emelt jogosultságot. Nem leképezett szerepkör automatikus létrehozás nélkül mellőzött; engedélyezve előtagos, nulla engedélyű/korlátozású, nem fejlesztői szerepkör készül vagy használódik, privilegizált automatikus cél tiltott. A szinkronizáció csak a jelenlegi szolgáltató hozzájárulását cseréli, a kézi és más szolgáltatói tagságot megőrzi. Fiókfeloldáskor történik, nem folyamatosan; szolgáltató eltávolítása nem von vissza korábbi szerepkört/munkamenetet.

Frissítés előtt ellenőrizni kell a leképezések céljainak, az automatikus előtagoknak és claimértékeknek a kis- és nagybetűit. A hibás cél most elutasítja a bejelentkezést, az automatikus szerepkör pedig külön nevet hozhat létre. A következő bejelentkezés előtt javítani kell a beállításokat és a szolgáltató claimjeit, majd ellenőrizni a szolgáltatói tagságokat. A meglévő szerepkör-azonosítók és tagságok nem változnak és nem vonódnak össze.

A Behind megerősítési, jóváhagyási, kitiltási és kétlépcsős ellenőrzése megelőzi a munkamenetet. Az OIDC második lépés megőrzi a szolgáltatót és független a Password/MagicLink kapcsolótól. Sikeres beváltás a meglévő `{accessToken, expires, refreshToken}` vagy `{ticket, methods, setupRequired, function}` választ adja; a jóváhagyási/megerősítési/kitiltási/nem elérhető második lépés HTTP 403 válaszai változatlanok. Fiókfeloldási hiba 401; konfigurációs/tárolási hiba szerverhiba.

## Kompatibilitás és migráció

Engedélyezés előtt az 1.0.0 Core SQLite/PostgreSQL migrációi a meglévő host-migrátorral alkalmazandók. Az identitás-/forrás-infrastruktúra a nyers adatbázisban csak olvasható, a felhasználók és szerepkörök életciklusa változatlan. Az 1.1.0 nem ad adatbázis-migrációt és nem ír át hitelesítő adatot. Hiányzó `Backend:Accounts:Oidc` mellett a korábbi telepítések megtartják az engedélyezett működést. Auditor szerződés nem változik.
