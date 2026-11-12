# Projekt áttekintés

## Elrendezések és widgetek

Az ütemezett webhelyértesítések a `#/manage/messages` oldalon, a `Message` engedélyterülettel kezelhetők. Verziózott adatbázis-entitások szintekkel és opcionális megjelenési időszakkal; a `core.message` widgeteket kézzel kell az elrendezésbe helyezni, mindegyik egy üzenetet választ. Egyszerre több értesítés is megjelenhet. Az elrejtés böngészőhöz és verzióhoz kötött; a beépített lábléc változatlan marad.

A Core `#/manage/layouts` oldala (Adminisztráció > Elrendezések) több elrendezést, szakaszsorrendet, sor- és oszlopátfogást, elhelyezési előnézetet és pontos/előtagos útvonal-hozzárendelést kezel. A motor teljes nyilvános oldalakat renderel opcionális `core.header` és `core.footer` widgetekkel; az elrendezést nem veszi körül rögzített nyilvános keret. Az új konfiguráció fejléc-, `core.content` és láblécszakaszt tartalmaz. A keret a fő tartalomtárolót mozgatja a helyére, megőrizve a jelölést és kötéseket. A fiók-, adminisztrációs és korlátozott hozzáférésű oldalak rögzített keretet tartanak meg, ugyanazokkal a keretkomponensekkel. A keret minden szakaszt `.behind-layout-band` elembe csomagol: az oldalszélességű szakaszok Bootstrap `.container` elemben vannak (`behind-layout-section-site`), a teljes szélességűek a nézetablak teljes szélességét kitöltik (`behind-layout-section-full`). A szakasz és widget `cssClass` értéke a renderelt burkolóelemet formázza, a szerkesztő vezérlőit nem érinti. Mindkét telepítés témája örökli a közös reszponzív stílusokat.

A fejléc-/láblécwidgeteket a `.behind-page-header` és `.behind-page-footer` osztályokkal formázza, ne csak a rögzített keret `#header-container` és `#footer-container` azonosítóival. Az ismételt fejlécwidgetek egyedi navigáció-összecsukási azonosítókat kapnak.

Az elrendezések a rácson kívül opcionális szöveges vagy hivatkozással ellátott képes jelvényeket is tartalmazhatnak. Az abszolút `.behind-layout-badges` fedőréteg a szakaszok és menük fölött van, de az oldallal görgethető; csak az egyes jelvények fogadnak mutatóeseményt. A sarokcsoportok a lista sorrendjében helyezik el a jelvényeket, és mobilon korlátozzák szélességüket. A képek az elrendezésképek feltöltését, ellenőrzését és VFS-kapcsolását használják; a fiók- és kezelési keretek nem jelenítik meg a jelvényréteget.

A modulok a `ModuleDescriptor.Widgets` mezőben adnak widgeteket (Common: `ModuleWidget`), amelyeket a `GET /modules` közzétesz. A kulcs modul-előtagos, a felületi betöltő a modulhoz tartozik. A Core friss tartalmakat, menüket, egyéni formázott szöveget/HTML-t és csevegést biztosít; a Fórum csak telepített állapotban ad üzenet- és statisztikawidgeteket. A végpontok megtartják a szokásos jogosultsági szabályokat. A `models/invalidation` böngésző-újratöltés nélkül frissíti a feliratkozott régiókat: a menümódosítások a menüket, az elrendezésmentés a keretet, a tartalom lecserélése nélkül.

A modul felületi modellje `load(type, limit)` függvényt ad, `{items?, stats?, content?, messages?}` eredménnyel. A leíró mezői: `key`, fordításkulcsot tartalmazó `name` és AMD `model`. Az időrendi modul-adatfolyamok a Common.Database `IModuleCreationOrder.NewestFirst` felületét kérik; ezt a Core audit-létrehozási időpontokkal valósítja meg, így a modul nem UUID-t rendez és nem függ a Core-tól.

## Fórum és nyilvános profilok

A globális korhatárszűrést az újratölthető `Backend.ContentRestrictions.Enabled` vezérli (alapértéke false). A verziózott elemszabályok a közös szűrt kontextust használják, a tranzitív szülőhivatkozásokra, médiára, adatfolyamokra és darabszámokra is. A modulleírók `ModuleDatabase.ContentRestrictedEntities` és `ContentRestrictionParentProperties` mezői Core-függőség nélkül biztosítják az integrációt.

A fő `Behind.Web` telepítés a Core, Quotes és Sport mellett betölti a `Behind.Forum.*` modult; a meglévő webhelyek nem töltik be a Fórumot. A Common, Domain, Database, SQLite, PostgreSQL és WebApi projektek a `ForumModule.Descriptor` segítségével csatlakoznak. A felület fórumtáblák, témák és válaszok böngészését, valamint moderálással kiegészített adminisztrációt biztosít. A `forum.json` újratölthető, és megjelenik a Beállítások oldalon.

A Core az `IModuleIdentity` felületen adja át a munkamenet felhasználóját, az `IModuleUsers` felületen pedig a nyilvános feliratot és profilkép-azonosítót; a felirat a szóközök nélküli név és állandó UUID-alapú rövid azonosító, üres névnél `user#azonosító`, sohasem e-mail-cím. A modulok nem hivatkoznak a Core-ra. A Core Profil oldalán a meglévő VFS és Bootstrap stílus segítségével lehet képet feltölteni és eltávolítani, minden telepítésben. A szerzőhivatkozások a `#/user/<id>` profilra mutatnak, jogosultságellenőrzött adminisztrátori moderálással és megszemélyesítéssel. A születési dátum opcionális és nem nyilvános, az e-mail megosztásához külön engedély kell.

A funkció tesztjei a `Behind/` könyvtárból a `dotnet test tests/Behind.Tests/Behind.Tests.csproj --filter "FullyQualifiedName~Forum|FullyQualifiedName~ProfilePicture"` paranccsal futnak; a felület futásidejű tesztjeihez a `node --test tools/tests/forum-frontend.test.cjs` használható. A Node-tesztek a mellékelt Knockoutot és az alkalmazás lapozóját használják, további csomagok nélkül.

A Behind egy modern .NET 11 alkalmazás, amely a tiszta architektúra elveire, a funkcionális programozási paradigmákra, valamint a kódminőség és a karbantarthatóság hangsúlyozására épül.

## A projekt célja

A Behind egy skálázható, karbantartható háttérrendszernek készült, amely mikroszolgáltatás-architektúrát használ (a .NET Aspire segítségével), és a következőket nyújtja:
- Erős domain-vezérelt tervezés
- Funkcionális programozási minták
- Típusbiztos hibakezelés
- Átfogó API-dokumentáció
- Éles üzemre kész megfigyelhetőség

## Technológiai stack

### Alapvető keretrendszer
- **Futtatókörnyezet**: .NET 11
- **Webes keretrendszer**: ASP.NET Core 11.0.0
- **Nyelvi lehetőségek**: C# 15 (rekordok, bővítő tagok, felső szintű utasítások)

### Adatelérés
- **ORM**: Entity Framework Core 11.0.0-rc.3.26522.148
- **Támogatott adatbázisok**:
  - SQLite 3.53.3 (`SQLitePCLRaw.lib.e_sqlite3` 3.53.3)
  - PostgreSQL (Npgsql.EntityFrameworkCore.PostgreSQL 11.0.0-rc.3)
- **Migráció**: EF Core kódalapú migrációk a Behind.Core.Database.Migrator segítségével

### Biztonság és hitelesítés
- **Hitelesítés**: JWT ******
- **Jelszókivonatolás**: BCrypt.Net-Next 4.2.0
- **Kétlépcsős hitelesítés**: Otp.NET 1.4.1 (TOTP támogatás)

### Megfigyelhetőség és monitorozás
- **Nyomkövetés és metrikák**: OpenTelemetry 1.17.0
- **Műszerezés**: 
  - ASP.NET Core
  - HTTP-kliens
  - Futtatókörnyezet
- **Exportálás**: OTLP (OpenTelemetry Protocol) kompatibilis exporterek

### API-dokumentáció
- **OpenAPI**: Microsoft.AspNetCore.OpenApi 11.0.0
- **Felhasználói felület**: Scalar.AspNetCore 2.14.11 (a Swagger helyettesítője)
- **Biztonság**: ****** documentation

### Tesztelés
- **Keretrendszer**: xUnit 2.9.2
- **Mockolás**: NSubstitute 5.3.0
- **Lefedettség**: Coverlet 6.0.2

### Segédeszközök
- **CLI-eszközök**: System.CommandLine 2.0.7
- **Dinamikus lekérdezések**: System.Linq.Dynamic.Core 1.7.3
- **Szerializáció**: System.Text.Json TypeDescriptor gyárakkal

### Orkesztráció
- **Szolgáltatások üzemeltetése**: Aspire 13.6.5
- **Konténerek**: Docker-támogatás az Aspire-on keresztül
- **Adatszolgáltatások**: PostgreSQL, Valkey (a Redis alternatívája)

## Projektstruktúra

### /src/Aspire
.NET Aspire orkesztrációs projekt a szolgáltatások összeállításához és a helyi fejlesztői környezet beállításához.

**Fő projektek:**
- `Behind.AppHost` - Szolgáltatás-orkesztráció és konfiguráció

### /src/Common
Újrafelhasználható könyvtárak, amelyeket az összes projekt megoszt.

**Projektek:**
- **Behind.Common** (alapvető építőkő)
  - Funkcionális programozási segédeszközök (Option, Try, Exceptional, Validation)
  - Bővítő metódusok és funkcionális kombinátorok
  - Szűrő- és lekérdezés-absztrakciók
  - JSON-szerializációs konverterek
  - Konfigurációs bővítések
  - Közös értékobjektumok és absztrakciók

- **Behind.Common.Database** (adatréteg-absztrakciók)
  - Repository minta interfészei (IRepository, IImmutableEntityRepository)
  - Unit of Work minta
  - DbContext bővítések
  - Szűrők alkalmazása az IQueryable-re

- **Behind.Common.WebApi** (API-réteg segédeszközei)
  - WebApplication builder bővítések
  - OpenAPI/OpenTelemetry konfiguráció
  - Globális kivételkezelők
  - API-szűrők és middleware-ek
  - Konfiguráció-szerializáció

### /src/Core
Alapvető üzleti logika és domain-implementáció.

**Projektek:**
- **Behind.Core.Common**
  - Core domain segédeszközök
  - E-mail értékobjektum
  - Jelszókivonatolási absztrakciók
  - TOTP-konfiguráció
  - Konverterek a domain típusokhoz

- **Behind.Core.Domain** (domain modellek – DDD)
  - Felhasználó aggregátum (HumanUser, SystemUser, GuestUser, BannedUser)
  - Szerepkör aggregátum (UserRole, SystemRole)
  - Hozzáférés (Grant) és jogosultság (Permission) modellek
  - Munkamenet (Session) és token modellek
  - E-mail értékobjektum
  - Auditnapló-bejegyzések
  - Szégyenfal-bejegyzések
  - Oldalak (a `VersionedKey` és az `ActiveVersion` segítségével verziózva) és a virtuális fájlrendszer feltöltött fájljai
  - Hírek kategóriákkal (fejléckép) és hozzászólásokkal
  - Cikkek saját kategóriákkal (fejléckép), cím és tartalom szerint kereshetők
  - Képgalériák opcionális leírásokkal, kiszolgálóoldali bélyegképekkel, piszkozatokkal és közzétételsel
  - Letöltések: mappák fája (slug és megjelenítendő név) a virtuális fájlrendszer feltöltött fájljaival (megjelenítendő név, méret, feltöltés ideje és feltöltő)
  - Menük menüelemekkel (almenük, cél, felhasználói menüelemek, láthatóság szerepkör szerint) és a beépített főmenü
  - Útvonal-aliasok: rövid útvonalak (`#/manuals`), amelyek az alkalmazás egy hosszabb oldalát nyitják meg (`#/downloads/products/manuals`)
  - Hozzáférési beállítások: korlátozó oldal, amely a weboldalt csak a kiválasztott szerepkörrel rendelkező felhasználóknak mutatja meg (egyéni üzenettel), valamint regisztrációmoderálás (az új fiókok jóváhagyásra várnak, mielőtt bejelentkezhetnének)

- **Behind.Core.Database** (adatimplementáció)
  - DbContext konfiguráció
  - Entitástípus-konfigurációk
  - Repository-implementációk
  - Lekérdezési specifikációk

- **Behind.Core.Database.Migrator**
  - EF Core migrációs eszközök
  - Adatbázis-inicializálás

- **Behind.Core.Database.Sqlite**
  - SQLite-specifikus konfigurációk

- **Behind.Core.Database.Postgres**
  - PostgreSQL-specifikus konfigurációk (`CoreContextPostgres`, `VersioningContextPostgres`)
  - Saját migrációk, kontextusonként egyetlen `Initial` migrációval kezdve

- **Behind.Core.WebApi** (API-végpontok)
  - API-vezérlők
  - Kérés/válasz modellek
  - WebApi-specifikus konfigurációk

- **Behind.Core.Tools**
  - CLI-eszközök és segédprogramok
  - `dataprotection create`: létrehozza az adatvédelmi tanúsítványt (véletlenszerű vagy bekért jelszóval) és egy `security.json` fájlt a `Security:DataProtection` szakasszal
  - `startup create`: előállítja a `security.json` fájlt (adatvédelmi tanúsítvány és kulcsok elérési útja, TOTP titkos kulcs, JWT aláírókulcs, fejlesztői jogosultságemelés TOTP-kulcsa, magic link titok), a `database.json` fájlt (SQLite), valamint a `backend.json` és a `frontend.json` fájlt (oldalcím, alapértelmezett nyelv és oldalbeállítások, hogy a Beállítások oldal szerkeszthesse őket). Kapcsolók: `--data-directory` (alapértelmezés: `data`), `--config-directory` (alapértelmezés: `.`), `--issuer`, `--audience`, `--email-confirmation`, `--prompt`, `--force`, `--skip-existing`. Hiba esetén nullától eltérő kilépési kóddal tér vissza
  - Konténerként is futtatható (a `tools/publish-core-tools-container.sh` a `reverse-project/behind/core-tools` képet publikálja); a konfigurációs fájlokba írt elérési utak abszolútak, ezért az adatkönyvtárat úgy csatolja, hogy a többi konténer is lássa:

    ```bash
    podman run --rm --user 0:0 \
        -v ./data:/app/data:Z -v ./config:/app/config:Z \
        git.prod.zwebnt.hu/reverse-project/behind/core-tools:latest \
        startup create --data-directory /app/data --config-directory /app/config --skip-existing
    ```

    A generált hitelesítő URI-kat a program egyszer írja ki; a kép alapértelmezés szerint a `1654` felhasználóként fut, ezért vagy tegye a csatolt könyvtárakat írhatóvá számára, vagy adja meg a `--user` kapcsolót a fentiek szerint.

### /src/Web
Webalkalmazás (frontend vagy webspecifikus háttérfunkciók).

**Projektek:**
- `Behind.Web` - Webspecifikus funkcionalitás

### /tests
xUnit-ot használó tesztprojektek.

**Projektek:**
- **Behind.Tests**
  - Egységtesztek az összes funkcionalitáshoz
  - Teszt-segédeszközök és fixture-ök
  - Mockolási segédeszközök

## Modulok

A Behind egy Core modulból és tetszőleges számú opcionális modulból áll. A gazdaprojekt (`src/Web/Behind.Web`) a Core mellett tölti be a modulokat: az `AddCore()` kötelező, a modul pedig a `services.AddModule(descriptor)` hívással regisztrálja magát, a sorrendtől függetlenül, a `UseCore()` pedig kiszolgálja az összes modul frontend-fájljait.

A modul egyetlen megváltoztathatatlan `ModuleDescriptor` objektumban (`Behind.Common/Modules`) ír le mindent, amire az alkalmazásnak a beillesztéséhez szüksége van:

| Hozzájárulás | Leíró tagja | Hatás a Core-ban |
|---|---|---|
| Hozzáférések | `GrantAreas` (`ModuleGrantArea(area, audience, actions)`) | minden művelethez létrejönnek a hozzáférések, korlátozások és engedélyezési szabályzatok, a beépített szerepkörök célközönség szerint megkapják őket (Everyone: az Administrators minden műveletet, a Users `Read` jogot; Administration: csak az Administrators; Developer: a Developers `Read`/`Manage` jogot), és megjelennek a Szerepkörök és a Felhasználók oldalon |
| Alapértelmezett menüelemek | `MenuItems` | a migrátor biztosítja őket a főmenüben (`Link` vagy `User` típus, mindenki, az adminisztrációs szerepkörök vagy a fejlesztők számára látható) |
| Adminisztrációs elemek | `AdministrationPages` (névkulcs, ikon, `#/` href, hozzáférés) | megjelennek az Adminisztráció oldalon azoknak a felhasználóknak, akik rendelkeznek a hozzáféréssel |
| Oldalak | `Routes` (`#/` útvonal, vezérlő, `Open`) | a frontend útválasztó regisztrálja őket az útvonal-alias tartalék előtt |
| Frontend-fájlok és stílusok | `Frontend` | beágyazott `Frontend/` fájlok, amelyek összeolvadnak a Core statikus fájljaival, a `modules/<key>/` alatti névtérben |
| Adatbázis | `Database` | az egybeolvasztott DbContext entitáskonfigurációi, soft delete-je és csak olvasható nyers táblái |

A `GET /modules` (nyílt, akárcsak az `/about`) közzéteszi az útvonalakat, az adminisztrációs oldalakat és a stílusokat a frontend számára, a `ModuleCatalog.Create` pedig indításkor ellenőrzi a hozzájárulásokat (kulcs- és területminták, ismétlődő hozzáférési területek, útvonalak és menüelem-azonosítók, a `modules/<key>/` alatti vezérlők és stílusok); az `AccessCatalog.From` emellett elutasítja a Core által már használt területeket és menüelem-azonosítókat, valamint az ismeretlen hozzáférést igénylő adminisztrációs oldalakat.

A teljes ellenőrzőlistát a `.github/skills/add-module/SKILL.md` fájlban, az egybeolvasztott adatbázist és a migrációkat pedig a `DATABASE_ARCHITECTURE.md` fájlban találja.

### Példamodul: Quotes

A `src/Quotes` egy teljes, szándékosan kicsi példamodul (idézetek listája szerzővel), amely a modul-ellenőrzőlista minden részét bemutatja. A gazdaprojekt, a `src/Web/Behind.Web` tölti be a Core mellett:

| Projekt | Tartalma |
|---|---|
| `Behind.Quotes.Domain` | a megváltoztathatatlan `Quote` rekord (`VersionedKey<Guid>` azonosító, szöveg, szerző) |
| `Behind.Quotes.Common` | `QuotesModuleNames`: a modulkulcs, a hozzáférési terület, a hozzáférések nevei, az útvonalak és a menüelem azonosítója |
| `Behind.Quotes.Database` | `IQuotesContext`, `QuoteConfiguration`, `IQuoteCommands`/`IQuoteQueries` és azok implementációi, `QuotesAuditLabelSource`, `QuotesSeed`, `AddQuotesDatabase()` |
| `Behind.Quotes.Database.Sqlite`, `Behind.Quotes.Database.Postgres` | a csak tervezési időben használt migrációs kontextusok, azok tervezési idejű gyárai és a `CreateQuotes` migrációk |
| `Behind.Quotes.WebApi` | `QuotesModule.Descriptor`, a `/quotes` végpontok, `AddQuotes()`/`UseQuotes()` és a `Frontend/modules/quotes/` alatti beágyazott frontend |

A projektoldal a `src/Web` alatt található: a `Behind.Web.Database.Sqlite`/`.Postgres` tartalmazza szolgáltatónként az egyetlen DbContextet (`WebContextSqlite`, `WebContextPostgres`: `AbstractCoreContext` plusz `IQuotesContext`), a `Behind.Web.Database.Migrator` a projekt migrátora (a Core migrációk, majd a Quotes migrációk, végül a seedek), a `Behind.Web` pedig az `AddCore()`/`UseCore()` után hívja az `AddQuotes()`/`UseQuotes()` metódust.

### Modul: Sport

A `src/Sport` a sportok (elsőként a foci) modulja sportágakkal, pozíciókkal, statisztikatípusokkal, szezonokkal, csapatokkal, játékosokkal, szezontagságokkal és statisztikákkal. A Quotes szerkezetét követi (`Behind.Sport.Domain`, `.Common`, `.Database`, `.Database.Sqlite`, `.Database.Postgres`, `.WebApi`), általános részeit pedig a Common projektek adják minden modulnak: `VersionedCommands<TEntity, TDraft>`/`IVersionedCommands<TDraft>`/`VersionedSaveResult` (verziózott létrehozás, módosítás, logikai törlés, visszaállítás, végleges törlés, ensure), `Keyed<TEntity>`, `IModuleFiles` (VFS-feltöltések modulok számára), `ImageUploadExtensions` (képfeltöltő és -kiszolgáló végpontsegédek) és `VersionedSaveResultExtensions`. A projektkontextusok az `ISportContext`-et is megvalósítják, a `Behind.Web.Database.Migrator` pedig a Quotes után alkalmazza a Sport migrációit. A frontend a `Frontend/modules/sport/` alatt található a nyilvános oldalakkal (`#/sport`, csapatok, játékosok, szezonok, ranglisták, statisztikák, minden entitás mindenhol hivatkozva) és egy általános kezelőoldallal (`#/manage/sport/<erőforrás>`).

Az FSE oldal (`src/Sites/Behind.Sites.FSE`) betölti a Sport modult: a `Behind.Sites.FSE.Database.Sqlite`/`.Postgres` tartalmazza szolgáltatónként az egyetlen DbContextet (`FseContextSqlite`, `FseContextPostgres`: `AbstractCoreContext` plusz `ISportContext`), a `Behind.Sites.FSE.Database.Migrator` a migrátora (a Core migrációk, majd a Sport migrációk, végül a seedek), az `AddFse()`/`UseFse()` pedig az `AddCore()`/`UseCore()` után hívja az `AddSport()`/`UseSport()` metódust.

### Adatbázis nélküli modul: Markdown Viewer

A `src/MarkdownViewer` modulnak nincs szüksége adatbázistáblára, ezért nincsenek adatbázis-projektjei és `ModuleDescriptor.Database` értéke sem: a VFS helyett a kiszolgáló egy forrás- és egy kimeneti könyvtárával dolgozik. A `src/Web/Behind.Web` tölti be az `AddMarkdownViewer()`/`UseMarkdownViewer()` hívással:

| Projekt | Tartalma |
|---|---|
| `Behind.MarkdownViewer.Domain` | a megváltoztathatatlan `MarkdownEntry`, `MarkdownPage` és `GeneratedSite` rekordok |
| `Behind.MarkdownViewer.Common` | `MarkdownViewerModuleNames`, `MarkdownViewerConfiguration` (a `markdownviewer.json` `MarkdownViewer` szakasza) és `MarkdownWorkspace`/`MarkdownGeneration`, az ellenőrzött könyvtárak (`ForViewing`/`ForGeneration`, `MarkdownWorkspaceErrors`) |
| `Behind.MarkdownViewer.WebApi` | a `MarkdownViewerModule.Descriptor` (`ConfigurationFiles = ["markdownviewer.json"]`), a Markdig megjelenítés hivatkozás-átírással és `SanitizeMarkdown()`, a `StaticSite` generátor, a `/markdownviewer` végpontok és a beágyazott frontend a `Frontend/modules/markdownviewer/` alatt |

A Beállítások oldal azért mutatja a `markdownviewer.json` fület, mert a modul a leírójában deklarálja a fájlt (`ConfigurationFileCatalog.For(root, moduleNames)`). Csak azok a forráskönyvtárbeli fájlok olvashatók, amelyeket a `MarkdownSourceExtensions` felsorol (nincs rejtett fájl, nincs szimbolikus hivatkozás, `.md` és képtípusok rögzített listája), így egy útvonal soha nem hagyhatja el a forráskönyvtárat. A forráskönyvtárban közvetlenül lévő nyelvi mappák (`^[a-z]{2,3}-[A-Z]{2}$`) nyelvfüggővé teszik (`MarkdownLocaleExtensions`: `Locales`, `ForLocale`, `Candidates(kért, alapértelmezett)`: a kért, majd a `Frontend:Site:DefaultLocale`, majd az első nyelv); a frontend elküldi az `i18n.locale()` értékét, és újratölt, ha a nyelvválasztó változik, a generátor pedig nyelvenként egy weboldalt készít. A generátor egy `.markdownviewer-generated` jegyzéket ír a kimeneti könyvtárba, és csak a benne felsorolt fájlokat törli.

## Architektúra rétegei

### 1. Common réteg (/src/Common)
**Cél**: Újrafelhasználható, általános segédeszközök és absztrakciók biztosítása

**Felelősségek**:
- Funkcionális programozási minták (monádok)
- Általános adatbázis-absztrakciók
- Általános WebApi segédeszközök
- Szerializációs konverterek
- Szűrő-absztrakciók

**Példák**: Option<T>, Try<T>, IRepository<T>

### 2. Core réteg (/src/Core)
**Cél**: A domainspecifikus üzleti logika megvalósítása

**Felelősségek**:
- Domain modellek és aggregátumok
- Domain szabályok és validáció
- Adatperzisztencia (DbContext, repository-k)
- Core API vezérlők
- Domainspecifikus segédeszközök

**Példák**: User, Role, Grant aggregátumok; jogosultság-ellenőrzés

### 3. Web réteg (/src/Web)
**Cél**: A webspecifikus feladatok kezelése

**Felelősségek**:
- Webalkalmazás-funkciók
- Webspecifikus middleware-ek
- Webspecifikus vezérlők vagy végpontok

## Fő tervezési minták

### 1. Monadikus kompozíció
Funkcionális monádokat használ a hibakezeléshez és az opcionális értékekhez:
- `Option<T>` - A nullable helyettesítője Railway-Oriented Programming támogatással
- `Try<T>` - Kivételkezelés kivétel-dobás nélkül
- `Exceptional<T>` - Kivétel vagy sikeres eredmény típusa
- `Validation<T>` - Hibákat gyűjtő validáció

### 2. Railway-Oriented Programming
A függvények kivételek dobása helyett eredménytípusokat adnak vissza, ami lehetővé teszi a kompozíciót:
```csharp
public Option<User> FindUser(Guid id)  // None if not found
public Exceptional<Auth> Authenticate(Credentials)  // Exception or Auth
public Validation<Command> ValidateCommand(Dto)  // Many errors or valid command
```

### 3. Bővítő tagok (C# 14+ `extension(Type)` szintaxis)
Minden bővítő metódus modern C# szintaxist használ a `*Extensions.cs` fájlokban:
```csharp
extension(Type)
{
    public new method(...) { ... }
}
```

### 4. Repository minta
Az általános `IRepository<T>` és `IRepository<T, TKey>` interfészek a következőket nyújtják:
- Keresés, hozzáadás, módosítás és törlés műveletek
- Async/await támogatás CancellationToken-nel
- Option<T> visszatérési típusok null helyett

### 5. Szűrő/lekérdezés-specifikáció
A `Filter<T>` rekordok a lekérdezési predikátumokat foglalják magukba az összeállítható szűréshez.

### 6. Megváltoztathatatlan rekordok
A domain objektumok `record` típusokat használnak a következőkért:
- Értékszemantika
- Alapértelmezett megváltoztathatatlanság
- ToString implementáció
- Strukturális egyenlőség

## Technológiai döntések

| Döntés | Indoklás |
|----------|-----------|
| Funkcionális programozás | Típusbiztos hibakezelés, összeállítható, tesztelhető |
| Rekordok az osztályok helyett | Megváltoztathatatlanság, egyenlőség, tömörség |
| Monádok (Option/Try/stb.) | A null értékek és a kivételalapú vezérlési folyam kiküszöbölése |
| Bővítő metódusok | Nem invazív segédeszköz-bővítések, folyékony API-k |
| EF Core | LINQ-támogatás, async/await, migrációk, adatbázis-független |
| JWT-hitelesítés | Állapotmentes, elosztott rendszerbarát |
| OpenTelemetry | Gyártófüggetlen, szabványalapú megfigyelhetőség |
| xUnit + NSubstitute | Modern tesztelés, tiszta izoláció |

## Fordítás és telepítés

### Helyi fejlesztés
- Dev container előre beállított .NET 11 SDK-val
- Aspire-orkesztráció a szolgáltatások összehangolásához
- SQLite (vagy PostgreSQL) a helyi adatbázishoz
- Docker compose az Aspire-on keresztül

### Fejlesztői környezet
- Konfiguráció az `appsettings.json` és az `appsettings.Development.json` fájlon keresztül
- Környezetenként beállított OpenTelemetry
- Adatbázis-migrációk a `Behind.Core.Database.Migrator` segítségével

### Éles telepítés
- Aspire által orkesztrált Docker-konténerek
- PostgreSQL a tartós adatokhoz
- OTLP exporter a központosított megfigyelhetőséghez
- JWT-hitelesítés kötelező

## Függőségkezelés

- **Központosított verziókezelés** a `Directory.Packages.props` fájlon keresztül
- **Tranzitív rögzítés** engedélyezve a reprodukálható buildekhez
- **Rendszeres frissítések** a függőségek naprakészen tartásához
- **Biztonsági vizsgálat** a sebezhetőségek feltárására (lásd: LIMITATIONS_AND_ISSUES.md)

## Minőségbiztosítás

### Kódszabványok
- **Nincsenek megjegyzések** - A kódnak önmagát kell dokumentálnia
- **Clean Code** - Beszédes nevek, kis függvények, egyetlen felelősség
- **SOLID elvek** - Különösen a függőségek megfordítása és az interfészek szétválasztása
- **DRY** - Nincs kódduplikáció; a megosztott logika a Common-ban él

### Tesztelés
- **Egységtesztek kötelezők** - Minden nyilvános funkcionalitáshoz tesztek tartoznak
- **Keretrendszer**: xUnit NSubstitute-tal
- **Tesztek helye**: `/tests/Behind.Tests/`, modulok szerint rendezve

### Dokumentáció
- **Kódszintű dokumentáció** - Önmagát dokumentálja a beszédes nevek és típusok révén
- **Architekturális dokumentáció** - Ez a mappa útmutatókat és mintákat tartalmaz
- **API-dokumentáció** - OpenAPI/Swagger az alkalmazásban a Scalar segítségével

## Teljesítmény és skálázhatóság

### Async végig
- Minden I/O-művelet async/await-et használ
- CancellationToken-támogatás mindenhol
- Szálbiztos minták

### Skálázhatóság
- Állapotmentes API-tervezés (JWT)
- A repository minta megkönnyíti az adatforrás cseréjét
- Az Aspire-orkesztráció támogatja a horizontális skálázást
- OpenTelemetry az elosztott nyomkövetéshez

## Biztonság

### Beépített
- JWT ******
- BCrypt jelszókivonatolás
- TOTP kétlépcsős hitelesítés
- Szerepköralapú hozzáférés-vezérlés (RBAC)
- Hozzáférésalapú (grant-based) jogosultságok
- E-mail-megerősítés regisztrációkor: a `Security:EmailConfirmation` (alapértelmezés: `true`) megerősítő hivatkozást küld; `false` érték esetén az új e-mail-cím `ConfirmedAt` értéke a regisztráció időpontja lesz; a `POST /sign/up/confirm/resend` új hivatkozást küld (404, ha a megerősítés ki van kapcsolva, 204 attól függetlenül, hogy a fiók létezik-e, sebességkorlátozását a `ConfirmationEmailExtensions.ResendRateLimitPolicy` végzi)
- Captcha a bejelentkezési és a regisztrációs oldalon: a `GET /captcha` (sebességkorlátozását a `CaptchaExtensions.RateLimitPolicy` végzi) egy `{ id, image }` objektumot ad vissza (a `Behind.Common` `CaptchaImageExtensions` osztálya által előállított PNG data URI); az `id` egy AES-GCM-mel lepecsételt jegy (lejárat és válasz), amelyet csak a kiszolgáló tud felnyitni, így semmilyen kihívás nem kerül tárolásra. A `POST /sign/in/password`, `/sign/in/magiclink`, `/sign/up/password` és `/sign/up/confirm/resend` a `RequireCaptcha<TRequest>()` révén `captcha: { id, answer }` mezőt igényel (a kérés megvalósítja az `ICaptchaRequest` interfészt); a `CaptchaService` egy kihívást csak egyszer fogad el, a lejáratáig (5 perc), az elutasítást pedig 400-as `{ code: "captchaInvalid" }` válasszal jelzi
- Kétlépcsős hitelesítés (TOTP QR-kóddal, hozzáférési kulcsok, helyreállító kódok): felhasználónként opcionális, a `Security:TwoFactorRequired` (alapértelmezés: `false`, a `security.json` fájlban) beállítással mindenki számára kötelező. A szolgáltatások a `Behind.Core.WebApi/TwoFactor` alatt találhatók: a `TwoFactorGate` a jelszó vagy a magic link után (és az e-mail-megerősítés és a tiltás ellenőrzése után, még a munkamenet létrejötte előtt) dönti el, hogy a `SignInEndpoints.StartSession` a tokenek helyett `TwoFactorChallengeResponse` választ ad-e `{ ticket, methods, setupRequired, function }` tartalommal; a `TwoFactorTickets` az ASP.NET Core Data Protection segítségével pecsételi le a jegyet (felhasználó, Verify/Setup/Profile szakasz, 5 perces lejárat, opcionális WebAuthn-szertartás); a `TwoFactorVerification` ellenőrzi a hitelesítő kódokat (SHA-1, hat számjegy, egy 30 másodperces lépésnyi eltérés, egy lépést a `UserTotp.LastUsedStep` révén csak egyszer fogad el), a helyreállító kódokat (`RecoveryCodes`: 20 darab, egyenként 8 karakteres kód az `A-Z0-9` karakterekből, BCrypt-kivonatként tárolva, a felhasznált kód törlődik, az utolsó felhasználása új kódcsomagot generál) és a hozzáférési kulcsokat (a `PasskeyCeremonies` a `Fido2` csomagot burkolja; a relying party azonosítója és eredete a `Frontend:Url` értékből vagy a kérés címéből származik; az aláírásszámláló tárolásra kerül); a `TwoFactorEnrollment` elindítja és megerősíti a hitelesítő alkalmazást (`Totp.Generate`, `ToOtpUri`, `ToQrCodeSvg`), regisztrálja és eltávolítja a hozzáférési kulcsokat, és megtagadja az utolsó módszer eltávolítását, amíg a kétlépcsős hitelesítés kötelező; a `TwoFactorSignIn` fejezi be a munkamenetet. Végpontok: `TwoFactorSignInEndpoints` (`/sign/in/two-factor/...`, jegyalapú, sebességkorlátozását a `TwoFactorExtensions.RateLimitPolicy` végzi) és `TwoFactorProfileEndpoints` (`/profile/two-factor/...`, bejelentkezett felhasználóknak, megszemélyesítési munkamenetekhez nem). A hibás kódokra 403-as `{ code: "invalidCode" }`, a hibás vagy lejárt jegyre 401-es `{ code: "ticketInvalid" }` válasz érkezik; a `SignInResponse.RecoveryCodes` a frissen generált kódokat tartalmazza

### Ajánlott gyakorlatok
- Érzékeny mezők maszkolása a naplókban
- Biztonságos konfigurációkezelés
- Nincsenek beégetett titkok
- Bemenet-ellenőrzés a Validation<T> segítségével

## Megfigyelhetőség

### Nyomkövetés
- Elosztott nyomkövetés az OpenTelemetry-n keresztül
- ASP.NET Core műszerezés
- HTTP-kliens műszerezés
- OTLP exporter támogatás

### Metrikák
- Futtatókörnyezeti metrikák
- HTTP-kérések metrikái
- ASP.NET Core metrikák

### Naplózás
- Strukturált naplózás az OpenTelemetry-vel
- Érzékeny mezők maszkolása
- Környezetspecifikus konfiguráció

## Jelenlegi állapot

### Használt verziók
- .NET SDK: 11.0.100
- ASP.NET Core és Microsoft.Extensions: 11.0.0
- Entity Framework Core: 11.0.0-rc.3.26522.148
- OpenTelemetry: 1.17.0
- Microsoft.AspNetCore.Authentication.JwtBearer: 11.0.0

### Ismert korlátozások
- A függőségi sebezhetőségekkel kapcsolatban lásd a [LIMITATIONS_AND_ISSUES.md](./LIMITATIONS_AND_ISSUES.md) fájlt
- A `dotnet list package --vulnerable --include-transitive` nem jelez sebezhető csomagot (ellenőrizve: 2026-11-12)

---

**Következő lépések**:
- Tekintse át a [CODING_STANDARDS.md](./CODING_STANDARDS.md) fájlt a kötelező gyakorlatokért
- Tanulmányozza a [FUNCTIONAL_PROGRAMMING.md](./FUNCTIONAL_PROGRAMMING.md) fájlt a minták megértéséhez
- Nézze meg a [DESIGN_PATTERNS.md](./DESIGN_PATTERNS.md) fájlt az architekturális döntésekért
