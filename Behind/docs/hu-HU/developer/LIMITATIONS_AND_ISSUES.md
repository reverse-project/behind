# Korlátozások és ismert hibák

Ez a dokumentum a Behind projekt ismert korlátozásait, hibáit és függőségi sebezhetőségeit foglalja össze.

## Ismert függőségi sebezhetőségek

A `dotnet list package --vulnerable --include-transitive` parancs 2026-11-12-én nem jelzett sebezhető csomagot a teljes megoldásban.

### Jelenlegi állapot

**Keretrendszer-verziók**:
- .NET SDK: 11.0.100
- ASP.NET Core: 11.0.0
- Entity Framework Core: 11.0.0-rc.3.26522.148
- Npgsql.EntityFrameworkCore.PostgreSQL: 11.0.0-rc.3
- OpenTelemetry: 1.17.0

### Nem javítható sebezhetőségek

Jelenleg nem ismert ilyen.

### Javított sebezhetőségek

✅ **SQLitePCLRaw.lib.e_sqlite3**: Frissítve a 3.53.3-as verzióra

✅ **Entity Framework Core csomagok**: Frissítve a 11.0.0-rc.3.26522.148-as verzióra

✅ **Microsoft.OpenApi (GHSA-v5pm-xwqc-g5wc)**: Tranzitív függőség, amelyet a Scalar.AspNetCore 2.14.11 húz be. Javítva a `Microsoft.OpenApi` 3.11.0 verzióra rögzítésével a `Directory.Packages.props` fájlban.

✅ **MessagePack**: Tranzitív függőség, amelyet a StreamJsonRpc húz be (az Aspire.Hosting igényli). Javítva a `MessagePack` 3.1.9 verzióra rögzítésével a `Directory.Packages.props` fájlban.

## Architekturális korlátozások

### 1. Egyetlen adatbázis-kontextus

**Korlátozás**: Az összes aggregátumhoz csak egy DbContext (ApplicationDbContext) tartozik.

**Hatás**:
- Nem választható szét könnyen szolgáltatásonkénti külön adatbázisokra
- Az összes aggregátum ugyanazon a tranzakciós hatókörön osztozik
- Az adatbázis szintjén nehezebb horizontálisan skálázni

**Enyhítés**:
- Használja a repository minta absztrakcióját a skálázási szempontokhoz
- Tervezze meg a kontextus szétválasztását, ha a skálázási követelmények megváltoznak
- A jelenlegi kialakítás a tipikus terhelésekhez elegendő

### 2. Memóriabeli gyorsítótárazás

**Korlátozás**: Nincs beépített gyorsítótár-réteg (Redis/Memcached).

**Hatás**:
- Minden lekérdezés az adatbázist terheli
- Nincs elosztott gyorsítótár-támogatás
- Teljesítményproblémák nagy forgalmú helyzetekben

**Enyhítés**:
- Gyorsítótár hozzáadása az Aspire-on keresztül (Valkey/Redis)
- Az EF Core lekérdezés-gyorsítótárazásának használata
- Eredmény-gyorsítótárazás megvalósítása a repository-kban

**Javaslat**: Nagy léptékű éles üzem előtt adjon hozzá gyorsítótár-réteget

### 3. Nincs eseményforrás-kezelés (Event Sourcing)

**Korlátozás**: Csak az AuditLogEntry-n keresztüli hagyományos eseménynaplózást használja.

**Hatás**:
- Nincs az állapotváltozásokat időben követő auditnyomvonal
- Az időbeli lekérdezések nehézkesek
- A megfelelőségi auditnyomvonalak nem teljesek

**Enyhítés**:
- Részletes AuditLogEntry rekordok a változás előtti és utáni értékekkel
- Archiválási stratégia a régi auditnaplókhoz
- Az auditnapló kézi átvizsgálása a megfelelőség érdekében

### 4. Korlátozott keresési lehetőségek

**Korlátozás**: Nincs teljes szövegű keresés.

**Hatás**:
- Szövegegyeztetéshez csak LIKE lekérdezések
- Nincs hibatűrő (fuzzy) keresés
- Gyenge teljesítmény nagy szöveges mezőknél

**Enyhítés**:
- Elasticsearch vagy hasonló hozzáadása az Aspire-on keresztül
- A keresés megvalósítása külön szolgáltatásban
- A szöveges mezők indexelése a LIKE lekérdezésekhez

## Funkcionális programozási korlátozások

### 1. Mintaillesztés kimerítő volta

**Probléma**: A C# nem kényszeríti ki a kimerítő mintaillesztést, ahogy az F# teszi.

**Példa**:
```csharp
// This compiles even though not all User types handled
var result = user switch
{
    HumanUser => "Human",
    SystemUser => "System"
    // Missing: GuestUser, BannedUser
};
```

**Enyhítés**:
- Használja a `_ => throw new NotImplementedException()` ágat utolsó esetként
- Fordítói figyelmeztetések hozzáadása az .editorconfig fájlon keresztül
- Kódellenőrzés (code review) a hiányzó esetek kiszűrésére

### 2. Az Option<T> és a null együttélése

**Probléma**: Az NRT (Nullable Reference Types) és az Option<T> ütközhet egymással.

**Példa**:
```csharp
public string? Name { get; init; }  // Could be null
public Option<string> Value { get; init; }  // Never null, always Some/None

// Confusing: which represents missing value?
```

**Enyhítés**:
- Az Option<T> következetes használata a domain fogalmakhoz
- A null használata csak szélsőséges esetekben
- Az NRT használatának egyértelmű dokumentálása

### 3. Generikus megszorítások

**Probléma**: A C#-ban nem fejezhető ki az összes szükséges megszorítás.

**Példa**:
```csharp
// Would like: "T must be a record"
public interface IRepository<T> where T : record  // Not supported
{
}
```

**Enyhítés**:
- A követelmények egyértelmű dokumentálása
- Kikényszerítés kódellenőrzéssel
- Explicit interfészek használata a különböző aggregátumtípusokhoz

## Teljesítménnyel kapcsolatos szempontok

### Jelenlegi szűk keresztmetszetek

1. **Nincs lekérdezés-gyorsítótárazás** - Minden azonos lekérdezés az adatbázist terheli
2. **Az N+1 probléma lehetséges** - A kapcsolódó adatokat explicit módon kell az Include() hívással betölteni
3. **Nincs kapcsolatkészlet-hangolás** - Az alapértelmezett értékek használatosak
4. **Egyetlen adatbázis** - Nincsenek olvasási replikák

### Optimalizálási lehetőségek

- Redis gyorsítótár-réteg hozzáadása
- Lekérdezési eredmények gyorsítótárazásának megvalósítása
- Adatbázis-olvasási replikák hozzáadása
- A CQRS megfontolása nagy olvasási terhelésekhez
- Kötegelt műveletek alkalmazása, ahol lehetséges

## Biztonsági korlátozások

### 1. JWT-token visszavonása

**Korlátozás**: A JWT-tokenek a lejáratukig érvényesek; nincs visszavonási lista.

**Hatás**:
- A tokenek nem vonhatók vissza azonnal (például kijelentkezés után)
- Biztonsági kockázat a kompromittálódott tokenek esetén

**Enyhítés**:
- Rövid tokenlejárat (15–30 perc)
- Frissítő token rotáció
- Visszavonási lista bevezetése a kritikus esetekre
- A munkamenet-érvénytelenítések figyelése

### 2. Jelszóbonyolultság

**Korlátozás**: Csak alapszintű reguláris kifejezéses ellenőrzés.

**Hatás**:
- Nem ellenőrzi a gyakori jelszavak elleni védelmet
- Nincs szótáras támadás elleni védelem

**Enyhítés**:
- HaveIBeenPwned API-integráció hozzáadása
- Jelszóerősség-becslés használata (zxcvbn algoritmus)
- Sebességkorlátozás bevezetése a hitelesítésnél

### 3. Nincs sebességkorlátozás

**Korlátozás**: A végpontokon nincs beépített sebességkorlátozás.

**Hatás**:
- Brute force támadások lehetségesek
- Az API-val való visszaélés nem akadályozott
- DOS-sebezhetőség

**Enyhítés**:
- Sebességkorlátozó middleware hozzáadása (AspNetCore.RateLimiting)
- Felhasználónkénti sebességkorlátok bevezetése
- API-átjáró használata éles környezetben

### 4. CORS-konfiguráció

**Korlátozás**: A CORS-beállítást telepítésenként explicit módon kell elvégezni.

**Hatás**:
- Hibás konfiguráció kockázata
- Eltérések a fejlesztői és az éles környezet között

**Enyhítés**:
- Környezetspecifikus CORS-szabályzatok használata
- A CORS-beállítás egyértelmű dokumentálása
- A CORS-konfiguráció átvizsgálása a biztonsági auditok során

## Tesztelési korlátozások

### 1. Nincsenek E2E tesztek

**Korlátozás**: Csak egységtesztek állnak rendelkezésre; nincsenek végpontok közötti (end-to-end) tesztek.

**Hatás**:
- Az integrációs hibák nem derülnek ki
- Hiányzik a teljes folyamat validálása
- Meglepetések a telepítéskor

**Javaslat**:
- Integrációs tesztek hozzáadása a WebApplicationFactory használatával
- E2E tesztek hozzáadása tesztkonténerekkel
- Felvétel a CI/CD folyamatba

### 2. Korlátozott tesztadatbázis-támogatás

**Korlátozás**: A tesztek memóriabeli SQLite-ot használnak; az éles környezet lemezen tárolt SQLite-ot.

**Hatás**:
- Eltérés a teszt- és az éles környezet között
- Az SQLite-specifikus hibák csak éles üzemben derülnek ki

**Javaslat**:
- Tesztek hozzáadása PostgreSQL ellen
- A TestContainers használata az egységes teszteléshez
- Tesztelés a tényleges céladatbázison

### 3. Mock repository problémák

**Korlátozás**: A DbSet és az IQueryable mockolása bonyolult.

**Hatás**:
- A repository-tesztek gyakran törékenyek
- Az aszinkron lekérdezési operátorokat nehéz mockolni

**Javaslat**:
- Az InMemory EF Core szolgáltató használata a tesztekhez
- A TestContainers kihasználása
- Az összetett LINQ minimalizálása a repository-kban

## Telepítési korlátozások

### 1. Nincs konténer-biztonsági vizsgálat

**Korlátozás**: A build folyamatban nincs automatizált sebezhetőségvizsgálat.

**Hatás**:
- Sebezhető alapképek használata
- A futásidejű sebezhetőségek nem derülnek ki

**Javaslat**:
- Trivy vagy Grype vizsgálat hozzáadása
- Szabályzat-kikényszerítés bevezetése
- Rendszeres alapkép-frissítések

### 2. Egypéldányos telepítés

**Korlátozás**: Az architektúra extra munka nélkül csak egyetlen példányt támogat.

**Hatás**:
- Nincs magas rendelkezésre állás
- Egyetlen hibapont
- A skálázás architekturális változtatásokat igényel

**Javaslat**:
- Terheléselosztó használata több példányhoz
- Elosztott munkamenet-kezelés bevezetése
- Adatbázis-replikáció tervezése

### 3. Nincs blue-green telepítés

**Korlátozás**: Nincs beépített blue-green telepítési stratégia.

**Hatás**:
- Leállás a frissítések közben
- A visszaállítás nehézkes

**Javaslat**:
- Megvalósítás Kubernetesen keresztül
- Infrastructure as code használata
- Leállásmentes telepítési stratégia tervezése

## Adatokkal kapcsolatos korlátozások

### 1. Nincs titkosítás tárolás közben (data encryption at rest)

**Korlátozás**: Az adatbázisfájlok nincsenek titkosítva.

**Hatás**:
- A lemez eltulajdonítása felfedi az adatokat
- Megfelelőségi szabálysértések

**Javaslat**:
- Az SQLite-titkosítás (SEE) engedélyezése
- PostgreSQL használata titkosítási bővítményekkel
- Megvalósítás a tárolási szinten (EBS-titkosítás stb.)

### 2. Nincs mentési stratégia

**Korlátozás**: Nincs beépített mentési/visszaállítási mechanizmus.

**Hatás**:
- Adatvesztés lemezhiba esetén
- Nincs helyreállítási pont

**Javaslat**:
- Napi mentések bevezetése
- A visszaállítási eljárások rendszeres tesztelése
- Felügyelt adatbázis-szolgáltatások használata

### 3. Korlátozott auditnyomvonal

**Korlátozás**: Az AuditLogEntry nem rögzít minden változást.

**Hatás**:
- Megfelelőségi hiányosságok
- Hiányos auditnyomvonalak

**Javaslat**:
- Minden entitásváltozás rögzítése
- Változásadat-rögzítés (CDC) bevezetése
- Az auditnaplók külön archiválása

## Platformkorlátozások

### 1. .NET verzió

**Korlátozás**: A .NET ökoszisztémához kötött.

**Hatás**:
- Csak Windows/Linux (nincs macOS-kiszolgáló)
- A C#-ökoszisztéma könyvtáraira korlátozott

**Kompromisszum**: A projekt hatóköre miatt elfogadott

### 2. SQLite korlátozások (fejlesztés)

**Korlátozások**:
- Nincs egyidejű írás
- Korlátozott lekérdezés-optimalizálás
- Nincsenek tárolt eljárások
- Nincsenek fejlett funkciók

**Enyhítés**:
- Az SQLite használata csak fejlesztéshez
- Éles környezetben a PostgreSQL ajánlott
- Az SQLite kompromisszumainak tudomásul vétele

## Üzemeltetési korlátozások

### 1. Nincs monitorozási irányítópult

**Korlátozás**: Nincs beépített monitorozási felület.

**Hatás**:
- Külső eszközöket kell használni (Grafana, Datadog)
- Nincs állapotellenőrző (health check) felület

**Javaslat**:
- Állapotellenőrző végpont megvalósítása
- Metrika-irányítópultok hozzáadása
- APM-eszközök használata

### 2. Nincs adminisztrációs felület

**Korlátozás**: Nincs adminisztrációs felület az üzemeltetési feladatokhoz.

**Hatás**:
- Az adminisztrációs műveletek csak az API-n keresztül érhetők el
- Nincs webalapú kezelőfelület

**Javaslat**:
- Adminisztrációs irányítópult készítése
- Üzemeltetési végpontok megvalósítása
- Az API dokumentálása adminisztrációs használatra

### 3. Korlátozott naplózási konfiguráció

**Korlátozás**: Az OpenTelemetry beállítása összetett; korlátozott naplózási felület.

**Hatás**:
- Az üzemeltetési átláthatóság nehézkes
- Az éles hibák hibakeresése nehezebb

**Javaslat**:
- Strukturált naplólekérdezések bevezetése
- Naplózási felület/irányítópult hozzáadása
- A gyakori lekérdezések dokumentálása

## Kompatibilitási megjegyzések

### Támogatott platformok
- ✅ Windows Server 2016+
- ✅ Linux (Ubuntu 20.04+, CentOS 7+, Debian 10+)
- ✅ macOS (csak fejlesztéshez)
- ❌ Docker Swarm (használjon Kubernetest)
- ⚠️ Azure App Service (egyes bővítményekkel lehetséges problémák)

### Támogatott adatbázisok
- ✅ SQLite 3.4+ (fejlesztés)
- ✅ PostgreSQL 12+ (éles környezetben ajánlott)
- ❌ SQL Server (jelenleg nem támogatott)
- ❌ MySQL (jelenleg nem támogatott)

### Támogatott böngészők (az API-dokumentációhoz)
- ✅ Chrome 90+
- ✅ Firefox 88+
- ✅ Safari 14+
- ✅ Edge 90+

## Hibák bejelentése

Ha biztonsági sebezhetőséget fedez fel:
1. **NE** nyisson nyilvános GitHub-hibajegyet
2. Küldjön e-mailt a zoltan@abel.co.hu címre a részletekkel
3. Hagyjon 48 órát a visszaigazolásra
4. Egyeztesse a közzététel ütemezését

Nem biztonsági jellegű hibák esetén:
1. Először keressen a meglévő hibajegyek között
2. Nyisson GitHub-hibajegyet a reprodukálás lépéseivel
3. Adja meg a környezet részleteit
4. Írja le az elvárt és a tényleges viselkedést

## Összefoglalás

| Kategória | Állapot | Hatás |
|----------|--------|--------|
| Függőségi biztonság | ⚠️ Nem javítható sebezhetőségek | Mérsékelt (fejlesztés/staging) |
| Teljesítmény | ⚠️ Nincs gyorsítótár | Mérsékelt |
| Skálázhatóság | ⚠️ Egyetlen példány | Csak közepes terheléshez |
| Biztonság | ⚠️ Nincs sebességkorlátozás | Magas kockázatú végpontok |
| Üzemeltetés | ⚠️ Korlátozott monitorozás | Kézi felügyelet szükséges |
| Tesztelés | ⚠️ Csak egységtesztek | Integrációs hiányosságok |

---

**Ne feledje**: Ez a kódbázis kis és közepes terheléshez éles üzemre kész. Vállalati léptékű telepítésekhez tervezzen architekturális fejlesztéseket.

A korlátozásokkal kapcsolatos kérdésekkel forduljon a fejlesztői csapathoz, vagy nyisson egy vitaindító hibajegyet.
