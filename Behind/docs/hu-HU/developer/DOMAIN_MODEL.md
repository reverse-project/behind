# Tartományi modell tervezése

## Elrendezéskonfiguráció modellje

A `LayoutConfiguration` revíziót, alapelrendezés-azonosítót, elrendezéseket és útvonal-hozzárendeléseket tartalmaz. A `SiteLayout` elnevezett rácsos szakaszokat és törlésjelzőt ad; a `LayoutSection` 1–12 sort és oszlopot, valamint `Width` értéket (`"site"`, alapértelmezett, vagy `"full"`; a nélküle tárolt elrendezések `"site"` értékkel töltődnek be, minden más érték érvénytelen) határoz meg. Az `Overlap` az előző szakaszhoz képest 0–2000 képponttal feljebb mozgatja, a `Layer` (-100–100) a szakaszok rétegsorrendjét adja meg. Minden `LayoutWidget` egyedi elhelyezésazonosítót, katalógustípust, címet, 1-től számozott pozíciót, sor-/oszlopátfogást, saját `Layer` értéket (-100–100) és típusfüggő beállításokat (menünév, egyéni tartalom/szerkesztő, elemszám) tartalmaz. A widgetek átfedhetik egymást, de a rácson belül kell maradniuk; minden elrendezésben pontosan egy `core.content` lehet. A hiányzó átfedési és rétegmezők alapértéke nulla. Az azonos pontosságú előtagmintával szemben a pontos útvonal nyer; a leghosszabb illeszkedő útvonal érvényesül. Csak aktív elrendezés rendelhető hozzá. Az egyéni HTML mentéskor és olvasáskor tisztításon esik át.

A `ModuleWidget` leíró Common-integrációs metaadat, nem tartományi entitás. Csak a telepített modulok kínálnak választható widgeteket; hiányzó modul widgetje nem hajthat végre tetszőleges felületi kódot. Az elrendezéskonfiguráció nem hoz létre verziózott adatbázis-entitásokat.

A `LayoutConfiguration.SchemaVersion` teljes oldalas elrendezésnél 1. A verzió nélküli és 0-s sémájú fájlok olvasáskor opcionális `core.header` és `core.footer` widgeteket kapnak a korábbi szakaszok elé és mögé; a revízió, hozzárendelések és törlési állapot megmarad. Az első szakasz korábban figyelmen kívül hagyott átfedése nullára változik. A jelenlegi elrendezések legfeljebb 22 szakaszt támogatnak, így mind a 20 korábbi szakasz elfér a hozzáadott keretelemekkel együtt. Az explicit 1-es sémájú elrendezések mindkét keretwidgetet elhagyhatják; pontosan egy `core.content` továbbra is kötelező. A szakasz és widget `CssClass` mezője alapból üres, legfeljebb 500 karakteres, szóközzel elválasztott ASCII-osztályneveket fogad (`[-_a-zA-Z][-_a-zA-Z0-9]*`). Ezek burkolóosztályok, nem HTML-tisztítási kivételek.

A `LayoutWidget.Borderless`, `NoMargin` és `NoPadding` egymástól független logikai értékek, elhagyva alapértékük hamis. Csak az elhelyezés keretét formázzák a `behind-layout-cell-borderless`, `behind-layout-cell-no-margin` és `behind-layout-cell-no-padding` osztályokkal; a beágyazott tartalmat és a szakasz térközeit nem állítják vissza. Sémaváltás nem szükséges.

A `LayoutWidget.HamburgerPosition` értéke `"default"` (alapérték, megőrzi a webhely jelölését), `"left"` vagy `"right"` lehet. A fejléc beillesztésekor a gyökérelem `data-hamburger-position` attribútumot kap; a rögzített keret visszaállítja az alapértéket. Csak a fejléc navigációs kapcsológombjára hat, a reszponzív töréspontokat nem módosítja.

A `SiteLayout.Badges` alapból üres lista (legfeljebb 20 elem). A `LayoutBadge` egyedi elhelyezési `Id`, kötelező `Text` (1–200, nem csak szóköz karakter; a kép helyettesítő szövege is), opcionális `Image` (`/layouts/images/{guid}`), opcionális biztonságos `Link` (a képforgó hivatkozási szabálya, vezérlőkarakterek és fordított perjel nélkül), `Position` (`top-left`, `top-right`, `bottom-left`, `bottom-right`) és `Width` (40–600 képpont, alapból 180) mezőt tartalmaz. A jelvény konfiguráció, nem entitás. Az elrendezés rétegzési környezetében abszolút fedőrétegként jelenik meg, a rácsszakaszok és menük felett; az azonos sarokban lévők a lista sorrendjében helyezkednek el, és az oldallal görgethetők. A kezelési/korlátozott útvonalakon nincs fedőréteg.

## Ütemezett webhelyüzenetek

A `Message(VersionedKey<Guid> Id, Name, Title, Content, Level, StartsAt?, EndsAt?)` verziózott webhelyértesítés, a csevegéstől függetlenül. Szintje `Info`, `Success`, `Warning` vagy `Danger`; címe opcionális, tartalma tisztított formázott HTML. A megjelenési időszak kezdete beleértendő, vége kizárandó; bármelyik határ elhagyható. Mentéskor új verzió keletkezik, a törlés és visszaállítás az auditállapotot használja. A `core.message` widget a `LayoutWidget.Message` mezőben hivatkozik a logikai kulcsra; minden elhelyezés egy üzenetet választ, és egy elrendezésben több is lehet. A böngészős elrejtés az üzenet kulcsához és verziójához kötött.

## Csevegőüzenetek

A `ChatMessage(VersionedKey<Guid> Id, AuthorId, Text, ReplyToId?)` egyszerű szöveget (1–1 000 karaktert) tárol változtathatatlan verziókban. A létrehozási és utolsó szerkesztési idő az auditnaplóból származik; nincs időbélyegoszlop. A szerző csak a létrehozást követő öt perc lejárta előtt hozhat létre új verziót, a korábbi szerkesztésektől függetlenül. Az elvárt verzió megakadályozza a módosítások elvesztését. Az üzenetek azonnal megjelennek. Vendégek olvashatnak; bejelentkezve `Chat.Read`, küldéshez, válaszhoz és szerkesztéshez ezen felül `Chat.Write` kell. A `Chat.Delete` saját üzenet eltávolítására, a `Chat.Manage` bármely üzenet moderálására jogosít. Az eltávolítás minden verzió auditált átmeneti törlése. A válasz a logikai üzenetkulcsra hivatkozik, és a legújabb látható szülőt vagy eltávolítás után helyettesítő szöveget mutat. A nyilvános nevekhez és képekhez az `IModuleUsers` használatos.

## A Fórum tartománya

- `ForumBoard(Name, Description, Moderation?)`: a fórumtábla opcionális moderálási felülbírálása.
- `ForumPost(BoardId, TopicId?, AuthorId, Title, Body, State)`: null `TopicId` esetén téma, egyébként azonos táblában lévő közzétett témára adott válasz. A törzs egyszerű szöveg, szöveges kötéssel, nem HTML-ként jelenik meg.
- `ForumUserModeration(UserId, Moderation)`: verziózott felhasználói szabály; kulcsa a felhasználó azonosítója, így személyenként egy szabály lehet.
- `PublicationState`: `Published`, `Pending`, `Rejected`. A nyilvános lekérdezések a közzététel ellenőrzése előtt a legfrissebb verziót választják ki, és aktív táblát, valamint közzétett szülőtémát követelnek.
- `ForumSettings`: alapértéke `Post`; az engedélyezett felhasználói felülbírálás elsőbbséget élvez az engedélyezett táblaszabállyal, majd a globális móddal szemben. Csak az új üzeneteket érinti; a meglévő függő üzenetekhez továbbra is jóváhagyás kell.
- `HumanUser.ProfilePictureId`: opcionális Core VFS-képhivatkozás; az `IModuleUsers` a nyilvános feliratokat és képazonosítókat adja át. A felirat a környező szóközök nélküli név és egy állandó, UUID-alapú rövid azonosító, üres névnél `user#azonosító`; e-mail-cím sohasem helyettesíti a nevet.
- A `HumanUser.Birthday` nem kötelező és nem nyilvános; hiányában a korhatáros tartalom nem érhető el, ha a globális szűrés engedélyezett (alapértelmezés szerint kikapcsolt). A `HideEmail` alapértéke true; az elsődleges e-mail csak kifejezett engedéllyel jelenik meg a nyilvános profilon.

Ez a dokumentum a Behind projektben használt tartományi modell szerkezetét, az entitások tervezési mintáit és a tartományvezérelt tervezés (domain-driven design) alapelveit ismerteti.

## Tartományvezérelt tervezés (Domain-Driven Design, DDD)

A Behind a **tartományvezérelt tervezés** elveit követi, a tartományi logika és a technikai szempontok közötti egyértelmű határokkal.

### A DDD fő fogalmai

1. **Mindenütt jelenlévő nyelv (Ubiquitous Language)** - A tartományi szakértők és a fejlesztők által közösen használt fogalmak
2. **Határolt kontextusok (Bounded Contexts)** - Elszigetelt üzleti tartományok saját modellekkel
3. **Aggregátumok (Aggregates)** - Összetartozó entitások csoportjai konzisztenciahatárokkal
4. **Értékobjektumok (Value Objects)** - Azonosító nélküli, változtathatatlan objektumok
5. **Entitás (Entity)** - Egyedi azonosítóval rendelkező objektumok
6. **Gyökér (Root)** - Az aggregátum belépési pontja, amely a konzisztenciát kezeli

## A tartományi réteg szerkezete

```
Behind.Core.Domain/
├── User.cs                 # User aggregátumgyökér
├── Role.cs                 # Role aggregátumgyökér
├── Grant.cs                # Grant értékobjektum
├── Email.cs                # Email értékobjektum
├── Session.cs              # Session entitás
├── AccessToken.cs          # Token értékobjektum
├── RefreshToken.cs         # Token értékobjektum
├── AuditLogEntry.cs        # Audit értékobjektum
├── DomainConstants.cs      # Tartományszintű konstansok
├── UserContext.cs          # Az aktuális felhasználó kontextusa
├── SystemContext.cs        # A rendszer végrehajtási kontextusa
├── Password.cs             # Password értékobjektum
├── Site.cs                 # Webhelyverzió-rekord (VersionedKey azonosító, piszkozat / közzétett)
├── SiteSection.cs          # Webhelyszekció-rekord (téma, cím, tartalom, képbeállítások) és a téma-/képpozíció-enumok
├── News.cs                 # Hír-, hírkategória- és hozzászólásrekordok
├── Article.cs              # Cikk- és cikkkategória-rekordok
├── Gallery.cs              # Galéria- és galériaelem-rekordok
├── Carousel.cs             # Webhelykörhinta- és körhintadia-rekordok (CarouselExtensions: intervallum- és hivatkozás-ellenőrzés)
├── Download.cs             # Letöltési mappa és letöltés (feltöltött fájl) rekordok
├── DownloadExtensions.cs   # A letöltés megjelenített neve, alapértelmezés szerint a fájlnév
├── VfsFile.cs              # A virtuális fájlrendszer egy feltöltött fájljának metaadatai
├── VfsFileExtensions.cs    # Fájlnév-tisztítás és a fájl tárolási kulcsa
├── Menu.cs                 # Menü- és menüelem-rekordok, a cél- és fajtaenumok
├── BuiltInMenus.cs         # A beépített főmenü a füleivel és a felhasználói menüelemekkel
├── MenuUrlExtensions.cs    # Mely menüelem-URL-ek biztonságosak (relatív, http, https, mailto, tel)
├── MenuVisibilityExtensions.cs # A menüelemek szerepkör alapú láthatósága
├── RouteAlias.cs           # Az útvonal-alias rekord (rövid útvonal és célútvonal)
├── AccessSettings.cs        # Az egyedi (singleton) hozzáférési beállítások (korlátozási oldal és regisztrációmoderálás)
├── AccessSettingsExtensions.cs # Vonatkozik-e a korlátozás a felhasználó hozzáférésére
├── RouteTargetExtensions.cs # Mely útvonal-alias célok érvényesek (belső #/ útvonalak)
└── Reason.cs              # Üzleti indok értékobjektum
```

## Fő aggregátumok

### 1. User aggregátum

**Aggregátumgyökér**: Az absztrakt `User` rekord

**Felelősség**: A felhasználói azonosság, a szerepkörök és az engedélyek kezelése.

**Szerkezet**:

```csharp
public abstract record User(Option<IEnumerable<Role>> Roles)
{
    public Guid Id { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public string ConcurrencyToken { get; init; }

    public bool HasPermission(Grant grant) =>
        Roles.Match(
            () => false,
            roles => roles.Any(r => r.HasPermission(grant))
        );

    public bool HasRole(Role role) =>
        Roles.Match(
            () => false,
            roles => roles.Contains(role)
        );
}
```

**Konkrét felhasználótípusok** (megkülönböztetett unió):

```csharp
public sealed record HumanUser : User
{
    public IEnumerable<Email> Emails { get; init; }
    public string DisplayName { get; init; }

    public HumanUser(
        string displayName,
        IEnumerable<Email> emails,
        Option<IEnumerable<Role>> roles) : base(roles)
    {
        DisplayName = displayName;
        var emailList = emails.ToList();

        if (emailList.Count == 0)
            throw new ArgumentException("HumanUser must have at least one email", nameof(emails));

        Emails = emailList;
    }
}

public sealed record SystemUser(
    Option<IEnumerable<Role>> Roles) : User(Roles);

public sealed record GuestUser(
    Option<IEnumerable<Role>> Roles) : User(Roles);

public sealed record BannedUser() : User(None);
```

**Miért több típus**:
- Típusbiztonság - A fordító megakadályozza az érvénytelen műveleteket
- Egyértelmű tartományi szemantika - A SystemUser és a HumanUser közötti különbség explicit
- Egyszerű lekérdezés - A `user is SystemUser` szándéka világos
- Mintaillesztés - Kimerítő típusellenőrzés

**A User műveletei**:

```csharp
extension(User)
{
    public Validation<User> AddRole(Role role)
    {
        return Roles.Match(
            () => Valid(this with { Roles = Some(new[] { role }) }),
            existing => existing.Contains(role)
                ? Invalid(new Error("ROLE_EXISTS", "User already has this role"))
                : Valid(this with { Roles = Some(existing.Append(role)) })
        );
    }

    public Validation<User> RemoveRole(Role role)
    {
        return Roles.Match(
            () => Invalid(new Error("NO_ROLES", "User has no roles")),
            existing => !existing.Contains(role)
                ? Invalid(new Error("ROLE_NOT_FOUND", "User doesn't have this role"))
                : Valid(this with
                {
                    Roles = existing.Count() == 1
                        ? None
                        : Some(existing.Where(r => r != role))
                })
        );
    }

    public Validation<HumanUser> AddEmail(Email email)
    {
        return this is not HumanUser human
            ? Invalid(new Error("NOT_HUMAN", "Only HumanUsers can have emails"))
            : human.Emails.Contains(email)
                ? Invalid(new Error("EMAIL_EXISTS", "Email already assigned"))
                : Valid(human with { Emails = human.Emails.Append(email) });
    }
}
```

### 2. Role aggregátum

**Aggregátumgyökér**: Az absztrakt `Role` rekord

**Felelősség**: A felhasználói szerepkörök és a hozzájuk tartozó engedélyek/jogosultságok (grant) meghatározása.

**Szerkezet**:

```csharp
public abstract record Role(string Name, Option<IEnumerable<Grant>> Grants)
{
    public Guid Id { get; init; }

    public bool HasPermission(Grant grant) =>
        Grants.Match(
            () => false,
            grants => grants.Contains(grant)
        );
}

public sealed record UserRole(
    string Name,
    Option<IEnumerable<Grant>> Grants) : Role(Name, Grants);

public sealed record SystemRole(
    Option<IEnumerable<Grant>> Grants) : Role(DomainConstants.System, Grants);
```

**Szerepkör-hierarchia**:

- **SystemRole** - Speciális szerepkör a rendszerműveletekhez
  - Emelt szintű engedélyekkel rendelkezik
  - Normál esetben nem rendelhető hozzá felhasználókhoz
  - Belső használatra fenntartva

- **UserRole** - Szokásos, a felhasználókhoz hozzárendelhető szerepkörök
  - Admin, Moderator, Member stb.
  - Összeállítható engedélyek
  - A felhasználók kezelik

**A Role műveletei**:

```csharp
extension(Role)
{
    public Validation<Role> AddGrant(Grant grant)
    {
        return Grants.Match(
            () => Valid(this with { Grants = Some(new[] { grant }) }),
            existing => existing.Contains(grant)
                ? Invalid(new Error("GRANT_EXISTS", "Grant already exists"))
                : Valid(this with { Grants = Some(existing.Append(grant)) })
        );
    }

    public Validation<Role> RemoveGrant(Grant grant)
    {
        return Grants.Match(
            () => Invalid(new Error("NO_GRANTS", "Role has no grants")),
            existing => !existing.Contains(grant)
                ? Invalid(new Error("GRANT_NOT_FOUND", "Grant not assigned"))
                : Valid(this with
                {
                    Grants = existing.Count() == 1
                        ? None
                        : Some(existing.Where(g => g != grant))
                })
        );
    }
}
```

## Értékobjektumok

Az értékobjektumok **azonosító nélküli, változtathatatlan objektumok**. Fogalmakat reprezentálnak, nem dolgokat.

### 1. Email

```csharp
public record Email(string Value)
{
    public Email(string value) : this(
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Email required", nameof(value))
            : value.Trim().ToLowerInvariant()
    )
    {
        if (!value.Contains("@"))
            throw new ArgumentException("Invalid email format", nameof(value));
    }

    public override string ToString() => Value;
}
```

**Jellemzők**:
- Változtathatatlan - Egy Email nem módosítható, csak újat lehet létrehozni
- Nincs azonosítója - Két Email("user@example.com") azonos
- Beépített ellenőrzés - Érvénytelen email nem hozható létre
- Tartományi szemantika - „Email”, nem pusztán „string”

### 2. Password

```csharp
public record Password(string Hash)
{
    public static Password Create(string plainPassword) =>
        new(BCrypt.HashPassword(plainPassword));

    public bool VerifyHash(string plainPassword) =>
        BCrypt.Verify(plainPassword, Hash);
}
```

**Jellemzők**:
- Soha nem tárolja a jelszót egyszerű szövegként
- A kivonat egységbe zárt
- Beépített ellenőrző metódus
- Nem „módosítható” - csak lecserélhető

### 3. Grant

```csharp
public record Grant(string Name, Option<string> Description = default);
```

**Tartományi grantek**:

```csharp
public static class DomainGrants
{
    public static readonly Grant ManageUsers = new("manage_users");
    public static readonly Grant ManageRoles = new("manage_roles");
    public static readonly Grant ModerateContent = new("moderate_content");
    public static readonly Grant ViewAnalytics = new("view_analytics");
}
```

### 3a. Korlátozás (Restriction) és számított grantek

```csharp
public sealed record Restriction(string Name);

public sealed record UserAccess(
    IReadOnlyList<Role> Roles,
    IReadOnlyList<string> Grants,
    IReadOnlyList<string> Restrictions);
```

A `Restriction` egy olyan grantet nevez meg, amelyet el kell venni. A szerepkörök grantekat és korlátozásokat
hordoznak, a felhasználók pedig közvetlen granteket és korlátozásokat. A felhasználó számított grantjei az
összes szerepkörből származó grant és az összes közvetlen grant, levonva az összes szerepköri korlátozást és
az összes közvetlen korlátozást, így a korlátozás mindig erősebb a grantnél.

A beépített szerepkör-grantek egyszer jönnek létre, a szerepkör (vagy a beépített grant) első létrehozásakor,
így a Szerepkörök oldalon végzett módosítások az újraindítások után is megmaradnak. A szerkesztők egy
szerepkört vagy felhasználót csak olyan grantekre emelhetnek, amelyekkel maguk is rendelkeznek, kivéve, ha
birtokolják a `RawDatabase.Manage` grantet.

Végpontok: `GET/POST /roles`, `PUT /roles/{id}/access`, `POST /roles/{id}/reset-grants` (csak beépített szerepkörök),
`POST /roles/{id}/delete`,
`GET/PUT /users/{id}/access`. A `GET /session` a számított granteket a `user.grants` mezőben adja vissza.

### 4. Munkamenettel kapcsolatos értékobjektumok

```csharp
public record AccessToken(string Value, DateTime ExpiresAt)
{
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
}

public record RefreshToken(string Value, DateTime ExpiresAt)
{
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
}

public record TokenNonce(string Value)
{
    public static TokenNonce Generate() =>
        new(Guid.NewGuid().ToString("N"));
}
```

### 5. Reason

```csharp
public record Reason(string Value)
{
    public Reason(string value) : this(
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Reason required", nameof(value))
            : value.Trim()
    ) { }
}
```

**Használat**: A „miért” dokumentálása az auditnaplókhoz.

```csharp
public record BanUserCommand(
    Guid UserId,
    Reason Reason);  // Miért tiltották ki a felhasználót?

public record DeleteContentCommand(
    Guid ContentId,
    Reason Reason);  // Miért törölték a tartalmat?
```

## Entitások

Az entitások **egyedi azonosítóval rendelkező, változtatható objektumok**.

### Session entitás

```csharp
public record Session
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public AccessToken AccessToken { get; init; }
    public RefreshToken? RefreshToken { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public bool IsActive { get; init; }

    public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow >= ExpiresAt;
}

extension(Session)
{
    public Validation<Session> Renew(AccessToken newToken, RefreshToken? newRefreshToken)
    {
        if (!RefreshToken?.IsExpired == true)
            return Invalid(new Error("REFRESH_EXPIRED", "Refresh token expired"));

        return Valid(this with
        {
            AccessToken = newToken,
            RefreshToken = newRefreshToken,
            ExpiresAt = newToken.ExpiresAt
        });
    }

    public Validation<Session> Invalidate()
    {
        return Valid(this with { IsActive = false });
    }
}
```

### Második faktoros entitások

A felhasználó második faktorral védheti a fiókját. A rekordok egy felhasználóhoz tartoznak (árnyék `Id` és `UserId`), változtathatatlanok, és a változtathatatlan repositoryn keresztül frissülnek:

```csharp
public sealed record UserTotp([property: Sensible] string Secret, bool IsConfirmed = false, long LastUsedStep = 0);

public sealed record UserPasskey(string Name, string CredentialId, string PublicKey, long SignCount = 0);

public sealed record UserRecoveryCode([property: Sensible] string Hash);
```

A `UserTotp` a hitelesítő alkalmazás titkát tárolja (megerősítettnek számít, amint begépeltek egy abból származó kódot), a `UserPasskey` egy WebAuthn-hitelesítő adatot, a `UserRecoveryCode` pedig a 20 egyszer használható helyreállítási kód egyikét (BCrypt-kivonat; lásd a `RecoveryCodeExtensions` osztályt a `Behind.Core.Common` projektben). Lásd: [Adatbázis-architektúra](./DATABASE_ARCHITECTURE.md#kétfaktoros-hitelesítés).

## Quote (Quotes példamodul)

A `Behind.Quotes.Domain.Quote(VersionedKey<Guid> Id, string Text, string Author)` a példamodul entitása (lásd: `PROJECT_OVERVIEW.md`). A `Text` legfeljebb 1000, az `Author` legfeljebb 200 karakter lehet, mindkettő kötelező. Mint minden verziózott entitásnál, a mentés a következő verziót szúrja be; a létrehozás és a módosítás ideje az auditnaplóból olvasható ki, és soha nem tárolódik az idézeten. A modul grant-területe a `Quotes` (`Quotes.Read`, `Quotes.Write`, `Quotes.Delete`, `Quotes.Manage`).

## Sport modul

A `Behind.Sport.Domain` a megváltoztathatatlan `Discipline`, `Position`, `StatType` (`StatScope`: `Player` vagy `Team`), `Season`, `Team` (opcionális `LogoId`), `Player` (pozíció, születési dátum, nemzetiség, magasság, testsúly, `Side` és életrajz), `PlayerSeason` (játékos, szezon, csapat, mezszám és az adott szezon képe) és `Stat` (szezon, statisztikatípus, alany és érték) rekordokat tartalmazza, mindegyik `VersionedKey<Guid>` azonosítóval. Egy játékosnak szezononként egy `PlayerSeason`-ja van, így a különböző szezonoknak különböző képe és csapata lehet. A modul hozzáférési területe a `Sport` (`Sport.Read`, `Sport.Write`, `Sport.Delete`, `Sport.Manage`).

## Grantek és a hozzáférési katalógus

A Core grant-területek (`GrantArea`, `GrantAction`) és a beépített szerepkörök (`BuiltInAccess`) a hozzáférési modell Core-része. A modulok a `ModuleDescriptor` segítségével adnak hozzá grant-területeket; az `AccessCatalog` (`AccessCatalog.Core` vagy `AccessCatalog.From(moduleCatalog)`) a teljes, változtathatatlan nézet, amelyet minden fogyasztó használ: a létrehozandó grantek, korlátozások és beépített szerepkörök (`catalog.Grants`/`Restrictions`/`Roles`), `IsKnownGrant`, `IsDeveloperGrant`, `GrantsDeveloperAccess`, `IsValid(selection)`, `IsBuiltIn(roleId)` és `FindRole(roleId)` (a beépített szerepkörök visszaállítási alapértékei tartalmazzák a modulok granteit). A modulok grant-azonosítói determinisztikusak (`Guid.FromName("61000000", "<Area>.<Action>")`), így minden telepítés ugyanazokat az azonosítókat hozza létre. Éles kódban soha ne olvassa a `BuiltInAccess.Grants`, `Roles` vagy `GrantNames` tagokat: ezek csak a Core-t írják le.

## Audit és megfelelőség

### AuditLogEntry

```csharp
public record AuditLogEntry(
    Guid Id,
    string EntityName,
    Guid EntityId,
    string Operation,  // Created, Updated, Deleted, PermissionGranted
    IReadOnlyDictionary<string, string> Changes,  // Előtte/utána értékek
    Option<Guid> UserId,  // Ki végezte a módosítást?
    DateTime Timestamp)
{
    public AuditLogEntry(
        string entityName,
        Guid entityId,
        string operation,
        IReadOnlyDictionary<string, string> changes,
        Option<Guid> userId = default) : this(
        Id: Guid.NewGuid(),
        EntityName: entityName,
        EntityId: entityId,
        Operation: operation,
        Changes: changes,
        UserId: userId,
        Timestamp: DateTime.UtcNow)
    { }
}
```

**Használat**:

```csharp
var audit = new AuditLogEntry(
    EntityName: "User",
    EntityId: user.Id,
    Operation: "Created",
    Changes: new Dictionary<string, string>
    {
        ["Email"] = newUser.Email.Value,
        ["DisplayName"] = newUser.DisplayName
    },
    UserId: currentUser.Map(u => u.Id)
);

await auditRepository.AddAsync(audit);
```

### WallOfShameEntry

```csharp
public record WallOfShameEntry(
    Guid Id,
    Guid UserId,
    string Reason,
    DateTime CreatedAt);

extension(User)
{
    public Validation<WallOfShameEntry> Ban(Reason reason, Option<Guid> bannedById)
    {
        if (this is BannedUser)
            return Invalid(new Error("ALREADY_BANNED", "User already banned"));

        return Valid(new WallOfShameEntry(
            Id: Guid.NewGuid(),
            UserId: this.Id,
            Reason: reason.Value,
            CreatedAt: DateTime.UtcNow
        ));
    }
}
```

## Tartományi események

A tartományi események a tartományban történt jelentős eseményeket reprezentálják.

```csharp
public abstract record DomainEvent
{
    public Guid EventId { get; init; }
    public DateTime OccurredAt { get; init; }
    public Option<Guid> UserId { get; init; }
}

public record UserCreatedEvent(
    Guid UserId,
    Email Email,
    Option<Guid> CreatedById = default) : DomainEvent;

public record UserBannedEvent(
    Guid UserId,
    Reason Reason,
    Option<Guid> BannedById = default) : DomainEvent;

public record RoleGrantedEvent(
    Guid UserId,
    Guid RoleId,
    Option<Guid> GrantedById = default) : DomainEvent;

public record PermissionGrantedEvent(
    Guid RoleId,
    Grant Grant,
    Option<Guid> GrantedById = default) : DomainEvent;
```

**Használat**:

```csharp
public class UserService
{
    private readonly IEventPublisher eventPublisher;
    private readonly List<DomainEvent> events = new();

    public async Task<User> CreateUserAsync(CreateUserDto dto)
    {
        var user = new HumanUser(dto.DisplayName, new[] { new Email(dto.Email) }, None);

        // Esemény rögzítése
        events.Add(new UserCreatedEvent(
            UserId: user.Id,
            Email: user.Emails.First(),
            CreatedById: currentUser.Map(u => u.Id)
        ));

        await repository.AddAsync(user);

        // Események közzététele más szolgáltatások számára
        foreach (var @event in events)
        {
            await eventPublisher.PublishAsync(@event);
        }

        return user;
    }
}
```

## Tartományi konstansok

A tartományszintű konstansok központosítása:

```csharp
public static class DomainConstants
{
    // Rendszerszerepkörök
    public const string System = "System";
    public const string AdminRole = "Admin";
    public const string ModeratorRole = "Moderator";
    public const string MemberRole = "Member";

    // Grantnevek
    public const string ManageUsersGrant = "manage_users";
    public const string ManageRolesGrant = "manage_roles";
    public const string ModerateContentGrant = "moderate_content";

    // Alapértelmezett azonosítók
    public static readonly Guid DefaultRoleId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly Guid AdminRoleId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    // Korlátok
    public const int MaxEmailsPerUser = 5;
    public const int MaxPasswordLength = 128;
    public const int MinPasswordLength = 8;
    public const int SessionTimeoutMinutes = 30;
}
```

## Ellenőrzés a tartományban

A tartományi ellenőrzés az üzleti szabályok érvényesítéséről szól, nem a szintaxisról.

```csharp
public static class DomainValidation
{
    public static Validation<HumanUser> ValidateNewUser(
        string displayName,
        Email email,
        string plainPassword)
    {
        return (
            from displayNameValid in ValidateDisplayName(displayName)
            from passwordValid in ValidatePassword(plainPassword)
            select new HumanUser(displayNameValid, new[] { email }, None)
        );
    }

    private static Validation<string> ValidateDisplayName(string displayName)
    {
        return string.IsNullOrWhiteSpace(displayName)
            ? Invalid(new Error("DISPLAY_NAME_REQUIRED", "Display name is required"))
            : displayName.Length > 100
                ? Invalid(new Error("DISPLAY_NAME_TOO_LONG", "Display name too long"))
                : Valid(displayName.Trim());
    }

    private static Validation<Password> ValidatePassword(string plainPassword)
    {
        var errors = new List<Error>();

        if (plainPassword.Length < DomainConstants.MinPasswordLength)
            errors.Add(new Error("PASSWORD_TOO_SHORT", "Password must be at least 8 characters"));

        if (!plainPassword.Any(char.IsUpper))
            errors.Add(new Error("PASSWORD_NO_UPPER", "Password must contain uppercase letter"));

        if (!plainPassword.Any(char.IsDigit))
            errors.Add(new Error("PASSWORD_NO_DIGIT", "Password must contain digit"));

        return errors.Count > 0
            ? Invalid(errors)
            : Valid(Password.Create(plainPassword));
    }
}
```

## Entitáskonfiguráció (adatbázis-leképezés)

Az értékobjektumok és az entitások konfigurálása a Behind.Core.Database projektben történik:

```csharp
extension(ModelBuilder)
{
    public ModelBuilder ConfigureUserAggregate(this ModelBuilder modelBuilder)
    {
        var userBuilder = modelBuilder.Entity<User>();

        userBuilder.HasDiscriminator<string>("UserType")
            .HasValue<HumanUser>(nameof(HumanUser))
            .HasValue<SystemUser>(nameof(SystemUser))
            .HasValue<GuestUser>(nameof(GuestUser))
            .HasValue<BannedUser>(nameof(BannedUser));

        userBuilder.OwnsMany(u => (u as HumanUser)!.Emails, emailBuilder =>
        {
            emailBuilder.ToJson();
            emailBuilder.HasKey("Id");
            emailBuilder.Property(e => e.Value).HasMaxLength(255);
        });

        userBuilder.HasMany(u => u.Roles)
            .WithMany()
            .UsingEntity("UserRoles");

        return modelBuilder;
    }
}
```

## Öröklődés és kompozíció

### Akkor használjon öröklődést, ha:
- Megkülönböztetett uniókat hoz létre (User típusok)
- Polimorf viselkedést tart fenn
- Az adatbázisszintű öröklődésnek van értelme

### Akkor használjon kompozíciót, ha:
- Viselkedéseket kombinál (Session a Token-nel)
- Kerüli a mély hierarchiákat
- A rugalmasság fontosabb az öröklődésnél

**Példa - Session kompozíció**:

```csharp
public record Session(
    Guid Id,
    Guid UserId,
    AccessToken Token,
    Option<RefreshToken> RefreshToken,
    DateTime CreatedAt);

extension(Session)
{
    public bool IsTokenExpired(this Session session) =>
        session.Token.IsExpired;

    public Validation<Session> RenewToken(AccessToken newToken)
    {
        return session.RefreshToken.Match(
            () => Invalid(new Error("NO_REFRESH", "No refresh token")),
            refresh => refresh.IsExpired
                ? Invalid(new Error("REFRESH_EXPIRED", "Refresh token expired"))
                : Valid(session with { Token = newToken })
        );
    }
}
```

## A tartományi modellezés legjobb gyakorlatai

✓ **TEGYE**:
- Tegye lehetetlenné az érvénytelen állapotokat
- Foglalja egységbe az ellenőrzést az aggregátumokban
- Használjon értékobjektumokat a tartományi fogalmakhoz
- Ellenőrizzen a tartományi határokon
- Használjon megkülönböztetett uniókat a típusbiztonságért
- Dokumentálja a tartományi fogalmakat világos nevekkel

✗ **NE TEGYE**:
- Ne használjon általános `string`/`int` típust tartományi fogalmakhoz
- Ne engedje meg érvénytelen aggregátumok létrehozását
- Ne tegye közzé a megvalósítási részleteket
- Ne hagyja ki az ellenőrzést a határokon
- Ne keverje a tartományi logikát a perzisztenciával
- Ne hozzon létre túl sok felelősséggel rendelkező „isten” objektumokat

## Összefoglalás

| Fogalom | Cél | Példa |
|---------|---------|---------|
| Aggregátum | Konzisztenciahatár | User aggregátum |
| Entitás | Változtatható, azonosítóval rendelkező | Session |
| Értékobjektum | Változtathatatlan, azonosító nélküli | Email, Password |
| Tartományi esemény | Valami jelentős történt | UserBannedEvent |
| Tartományi szabály | Üzleti logikai megszorítás | A felhasználónak rendelkeznie kell email címmel |
| Specifikáció | Lekérdezés egységbezárása | UserSpecifications.Active |
| Kontextus | Határolt kontextus | Behind.Core.Domain |

---

**Következő lépések**:
- A perzisztencia-leképezésről lásd a DATABASE_ARCHITECTURE.md fájlt
- Az API-reprezentációról lásd a WEBAPI_CONVENTIONS.md fájlt
- Az eredménykezelésről lásd a FUNCTIONAL_PROGRAMMING.md fájlt
