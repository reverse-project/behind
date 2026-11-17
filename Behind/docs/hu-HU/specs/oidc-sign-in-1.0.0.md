# Behind OIDC bejelentkezés 1.0.0

Állapot: megvalósítva. Ez a kiegészítő nyilvános szerződés a Semantic Versioning 2.0.0 szabályait követi, az alkalmazás és az Auditor verziójától függetlenül. A korábbi hitelesítési végpontok és válaszformátumok változatlanok. A gépi [átadási séma](../../schemas/oidc-sign-in-1.0.0.schema.json) leírja az új szolgáltatólistát és beváltási kérést.

## Konfiguráció

A `security.json` fájl `Security` szakasza alatt vegye fel az alábbi részt, a valódi kliens-titkot védett telepítési konfigurációban adja meg, majd indítsa újra:

```json
{
  "Oidc": {
    "TicketLifetime": "00:02:00",
    "AuthenticationTimeout": "00:10:00",
    "MaxPendingTickets": 1000,
    "Providers": [
      {
        "Id": "company",
        "DisplayName": "Company account",
        "Enabled": true,
        "Authority": "https://identity.example.org/realms/company",
        "ClientId": "behind",
        "Scopes": ["openid", "profile", "email"],
        "AutoCreateUsers": true,
        "LinkByVerifiedEmail": true,
        "RoleClaimType": "realm_access.roles",
        "RoleMappings": {"content-editors": ["Editor"]},
        "AutoCreateRoles": true,
        "AutoCreateRolePrefix": "company:"
      }
    ]
  }
}
```

Több szolgáltatóhoz több objektum vehető fel. Az ID állandó, egyedi ASCII betű/szám/aláhúzás/kötőjel azonosító; kis- és nagybetűben eltérő másolat tiltott. A már kapcsolt szolgáltató ID-ját tartsa meg. Az üres lista a korábbi hitelesítés módosítása nélkül kikapcsolja az OIDC-t. Engedélyezett szolgáltatónál megjelenített név, kliensazonosító, hitelesítő adat/query/fragment nélküli HTTPS authority, nem üres szerepkör-claim és `openid` scope kötelező. Hibás konfiguráció esetén az indulás kifejezett hibával leáll. Nyilvános kliensnél a `ClientSecret` opcionális; a Beállítások maszkolja. Valódi titkot ne tároljon forráskódban.

Alapértékek: engedélyezett szolgáltató; `openid`, `profile`, `email` scope; automatikus fióklétrehozás true; e-mailes összekapcsolás false; szerepkör-claim `roles`; nincs leképezés vagy automatikus szerepkör-létrehozás. Az automatikus előtag `oidc:<provider-id>:`. Az időkorlátok és kapacitás pozitívak. Minden OIDC beállítás induláskor olvasódik.

A szolgáltató ID tokenje tartalmazza az `email`, `email_verified`, opcionális `name` és a kiválasztott szerepkör claimet. A Behind nem hívja a UserInfo végpontot, és az e-mail meglétéből nem következtet annak megerősítésére.

## Böngészős protokoll és végpontok

1. `GET /sign/in/oidc/providers`: HTTP 200, `[{ "id": "company", "displayName": "Company account" }]`. Nem tartalmaz titkot, claimet vagy leképezést. Kikapcsolt globális bejelentkezésnél vagy JWT nélkül üres lista.
2. Böngészős navigáció `GET /sign/in/oidc/company`: HTTP 302, authorization code folyamat PKCE-vel. Secure, HttpOnly, SameSite=None, folyamatonkénti süti köti a böngészőt. Ismeretlen/tiltott szolgáltató vagy bejelentkezés 404; HTTPS nélkül 400.
3. A szolgáltatónál regisztrálja a pontos `https://<backend>/sign/in/oidc/callback/company` callbacket. A standard ASP.NET Core OIDC middleware ellenőrzi a védett state-et, korrelációt, nonce-ot, aláírást, issuert, audience-t és token-élettartamot, majd beváltja a kódot. A hitelesítő adatok és tokenek a szerveren maradnak; tokenmentés nincs.
4. Sikeres hitelesítés kizárólag a konfigurált `Frontend:Url#/signin?oidc=<opaque-ticket>` címre irányít. Hibánál `#/signin?oidcError=failed`; részletes szolgáltatói hiba nem nyilvános.
5. `POST /sign/in/oidc/redeem`, JSON `{ "ticket": "<opaque-ticket>" }`, eredeti böngészős sütivel: pontosan egyszer váltható be. A felület törli a ticketet a hash-útvonalból. Hibás/lejárt/újrajátszott/nem kötött ticket 401 és nincs munkamenet; letiltott bejelentkezés vagy JWT hiánya 404; hibás kérés 400.

A frontend és backend HTTPS-t igényel. Azonos eredet ajánlott; eltérő eredetnél kifejezett, hitelesített CORS-szabály és a kötő sütit engedő böngésző szükséges. Az átadás korlátozott, folyamaton belüli tárolóban van; több példánynál sticky routing szükséges. Újraindítás vagy másik példányra irányítás az ideiglenes átadást érvényteleníti, nem a tárolt identitást. URL nem tartalmaz hozzáférési/frissítési tokent. A válaszok `Cache-Control: no-store` fejlécet kapnak.

## Fiókok és jogosultságok

Első használatkor szabályos e-mail és szolgáltatói `email_verified=true` kötelező. Az automatikus létrehozáshoz `Backend:Accounts:SignUp=true` is kell; az alapértelmezett felhasználói szerepkör és moderált jóváhagyás érvényes. Meglévő fiók összekapcsolásához `LinkByVerifiedEmail=true` és a pontos helyi cím megerősítése kell. Ezzel megbízik a szolgáltató e-mail-ellenőrzésében és fiókéletciklusában; csak megbízható szolgáltatónál kapcsolja be.

A tartós azonosító a szolgáltató ID, ellenőrzött issuer és subject, nem az e-mail. A kapcsolat nem rendelhető csendben másik felhasználóhoz, puhán törölt helyi fiók vagy módosult szolgáltatói cím esetén sem. Újraindítás után megmarad; a felhasználó végleges törlése a kapcsolódó infrastruktúra-bejegyzéseket is eltávolítja. A Behind meglévő e-mail-megerősítési, jóváhagyási, kitiltási és kétlépcsős ellenőrzése megelőzi a munkamenet létrehozását. Az OIDC második lépés megőrzi a szolgáltatót; nem függ a Password/MagicLink kapcsolótól. Eltávolított szolgáltató újraindítás után nem fejezhet be függő kétlépcsős folyamatot.

A szerepkörök ismételt szöveges claimből, JSON szövegtömbből vagy pontozott JSON útvonalból (`realm_access.roles`) olvashatók. Hibás alak kifejezett hibát eredményez. A külső nevek leképezése pontos és kis-/nagybetű-érzékeny; helyi keresés a meglévő kis-/nagybetű-független szabályt követi. Minden leképezett cél létező aktív szerepkör. Nem leképezett szerepkör automatikus létrehozás nélkül figyelmen kívül marad; engedélyezve előtaggal, engedély és fejlesztői státusz nélküli normál szerepkör készül vagy használódik fel. Privilegizált automatikus cél tiltott. Kifejezett leképezés tudatosan adhat magas jogosultságot: a konfiguráció biztonságérzékeny.

A szinkronizáció csak a jelenlegi szolgáltató hozzájárulását cseréli. Kézi és más szolgáltatói hozzárendelések megmaradnak. Hiányzó külső szerepkörnél a korábbi OIDC-tagság megszűnik, ha más forrás nem igényli. A szinkronizáció fiókfeloldáskor történik, nem folyamatosan; szolgáltató letiltása/törlése önmagában nem von vissza szerepkört vagy munkamenetet. Azonnali visszavonáshoz használja az adminisztrációt.

## Válaszok, tárolás és migráció

Sikeres beváltás a meglévő `{accessToken, expires, refreshToken}` választ vagy `{ticket, methods, setupRequired, function}` második lépést adja. A függő jóváhagyás, megerősítetlen e-mail, kitiltás és nem elérhető második lépés megtartja a meglévő HTTP 403 szerződést. Fiókfeloldási hiba 401; tárolási/konfigurációs hiba naplózott szerverhiba, nem sikernek látszó tartalékérték.

Engedélyezés előtt alkalmazza a Core SQLite/PostgreSQL és összevont host-migrációkat. Az identitás-/forrás-infrastruktúra a nyers adatbázisban csak olvasható; a helyi felhasználók és szerepkörök megtartják a meglévő kezelési életciklust. Az új táblák nem írják át a korábbi fiókokat, jelszavakat, címeket vagy szerepköröket. Letiltott szolgáltatókkal a régi kód visszaállítható; identitáskapcsolatot csak tudatos megszüntetéskor töröljön. Az Auditor motor és formátumai változatlanok.
