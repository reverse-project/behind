# Az EmailSender megvalósításának és tesztjeinek összefoglalása

## Áttekintés
Ez a dokumentum összefoglalja az EmailSender funkció megvalósítását és a hozzá tartozó átfogó tesztkészletet a Behind.Core.WebApi projekthez.

## Megvalósítás részletei

### Komponensek

#### 1. Templates osztály (`Behind.Common.Templating.Templates`)
Gyűjtemény jellegű tároló az e-mail sablonok kezeléséhez, a következő képességekkel:

**Alapfunkciók:**
- `Add(Template template)` - Egy Template példány hozzáadása a gyűjteményhez
- `Add(string name, string content)` - Kényelmi metódus egy SimpleTemplate hozzáadásához
- `Remove(string name)` - Sablon(ok) eltávolítása név alapján
- `Remove(Template template)` - Egy adott sablonpéldány eltávolítása
- `Get(string name) → Option<Template>` - Sablon lekérése név alapján (Option-t ad vissza)
- `GetOrDefault(string name, Template defaultTemplate) → Template` - Lekérés vagy tartalék sablon használata
- `IEnumerable<Template>` - Teljes felsorolási támogatás

**Példa a használatra:**
```csharp
var templates = new Templates();

templates.Add("ConfirmationEmail", "<p>Please confirm {{Email}}</p>");
templates.Add("PasswordReset", "<p>Reset your password {{ResetLink}}</p>");

var template = templates.GetOrDefault("ConfirmationEmail", 
    new SimpleTemplate("default", "No template"));

var isSome = templates.Get("ConfirmationEmail")
    .Match(
        none: () => false,
        some: t => true
    );
```

**Fő tervezési döntések:**
- Megváltoztathatatlan műveletek: a `Remove`, `Get`, `Add` szálbiztos szemantikát tart fenn
- Option-alapú lekérés: a `Get` `Option<Template>`-et ad vissza, így elegánsan kezelhetők a hiányzó sablonok
- Felsorolható interfész: lehetővé teszi a LINQ-műveleteket és a gyűjteménymintákat
- Null-ellenőrzés: minden publikus metódus `ArgumentNullException`-nel ellenőrzi az argumentumait

#### 2. SimpleTemplate record (`Behind.Common.Templating.Template`)
Sablon alaptípus két megvalósítással:
- **Template (absztrakt record)**: Alaptípus Name és Content mezőkkel
- **SimpleTemplate (lezárt record)**: Teljes sablonmegvalósítás paraméteres rendereléssel

**Képességek:**
```csharp
var template = new SimpleTemplate("greeting", "Hello {{name}}, welcome {{greeting}}!");

var rendered = template
    .AddParameter(new Parameter("name", "Alice"))
    .AddParameter(new Parameter("greeting", "aboard"))
    .Render();

// Eredmény: "Hello Alice, welcome aboard!"
```

**A paraméterkezelés működése:**
- A helyőrzők `{{PropertyName}}` szintaxist használnak
- A paraméterek behelyettesítése egymás után történik a `string.Replace` segítségével
- Megváltoztathatatlan lánc: az `AddParameter` új SimpleTemplate példányt ad vissza
- A nem illeszkedő helyőrzők a renderelt tartalomban érintetlenül maradnak

#### 3. EmailSender alaposztály (`EmailSender.cs`)
- **Absztrakt osztály**, amely az `IEmailSender` és az `IEmailSender<ApplicationUser>` interfészt valósítja meg
- Sablonalapú e-mail-generálást biztosít a gyakori helyzetekhez:
  - E-mail-cím megerősítő hivatkozások
  - Jelszó-visszaállító kódok
  - Jelszó-visszaállító hivatkozások
- A `DefaultEmailTemplates` osztályt használja a kész sablontartalomhoz

#### 2. Implementációk
- **LoggerEmailSender**: Csak a címzettet és a tárgyat naplózza, a törzset soha, így a bejelentkezési hivatkozások és kódok nem kerülnek a naplókba
- **SmtpEmailSender**: E-maileket küld SMTP-n keresztül, a következőkkel:
  - Konfigurálható hitelesítés (Basic vagy hitelesítés nélkül)
  - A feladó címe a `SmtpEmailConfiguration.From` értékből származik, ennek hiányában a Basic hitelesítés felhasználóneve szolgál tartalékként; a küldés meghiúsul, ha egyik sincs beállítva
  - Az SMTP-hibák továbbadódnak a hívónak (a bejelentkezési hivatkozásokat küldő levélszolgáltatás naplózza őket)
  - Opcionális egyéni sablonok felülbírálása a `SmtpEmailConfiguration.Templates` útján
  - Tartalék: a `DefaultEmailTemplates`, ha nincsenek egyéni sablonok megadva
  - E-mail-típusonkénti sablonfeloldás `GetOrDefault` tartalékkal

#### 3. Kiterjesztő metódus (`EmailSenderExtensions.cs`)
```csharp
extension(EmailConfiguration configuration)
{
    public EmailSender GetInstance(IServiceProvider serviceProvider) => configuration switch
    {
        LoggerEmailConfiguration _ => new LoggerEmailSender(...),
        SmtpEmailConfiguration _ => new SmtpEmailSender(...),
        _ => throw new NotSupportedException(...)
    };
}
```
- Az EmailConfiguration típusa alapján létrehozza a megfelelő EmailSender példányt
- A függőségeket a szolgáltatásszolgáltatóból (service provider) oldja fel
- Támogatja:
  - `LoggerEmailConfiguration` → `LoggerEmailSender`
  - `SmtpEmailConfiguration` → `SmtpEmailSender` opcionális Templates támogatással
  - Ismeretlen típusok esetén `NotSupportedException` kivételt dob

#### 4. DefaultEmailTemplates (`DefaultEmailTemplates.cs`)
Statikus osztály, amely az alapértelmezett e-mail sablonokat biztosítja:
```csharp
public static class DefaultEmailTemplates
{
    public static readonly SimpleTemplate ConfirmationEmail = 
        new("ConfirmationEmail", "<p>Please confirm your email ({{Email}}) ...</p>");
    
    public static readonly SimpleTemplate PasswordResetCodeEmail = 
        new("PasswordResetCodeEmail", "<p>Your code: {{ResetCode}}</p>");
    
    public static readonly SimpleTemplate PasswordResetLinkEmail = 
        new("PasswordResetLinkEmail", "<p>Reset password: {{ResetLink}}</p>");
}
```

#### 5. SmtpEmailConfiguration bővítése
A frissített record opcionális Templates paraméterrel:
```csharp
public sealed record SmtpEmailConfiguration(
    string SmtpServer, 
    int Port, 
    Ssl Ssl, 
    Option<Authentication> Authentication,
    Option<Templates> Templates,  // Opcionális egyéni sablonok
    Option<string> From = default // Opcionális feladó cím
) : EmailConfiguration("Smtp");
```

**Sablonfeloldási folyamat (SmtpEmailSender):**
1. Ellenőrzi, hogy a `Templates` option tartalmaz-e értéket
2. Ha van érték (Some): a `GetOrDefault` segítségével lekéri a sablont, vagy az alapértelmezettet használja tartalékként
3. Ha nincs érték (None): közvetlenül az alapértelmezett sablont használja
4. Alkalmazza a paramétereket, és renderel

#### 6. WebApiExtensions integráció
Az `AddEmail()` metódus a `WebApiExtensions.cs` fájlban:
```csharp
WebApplicationBuilder AddEmail()
{
    // A konfiguráció betöltése (email.json)
    builder.Configuration.AddJsonFile("email.json", false, true);
    
    EmailConfiguration emailConfiguration = builder.Configuration
        .GetSection(EmailConfiguration.SectionName)
        .GetOptionAware<EmailConfiguration>() ?? new LoggerEmailConfiguration();
    
    // Regisztráció singletonként (megváltoztathatatlan és környezetfüggő)
    builder.Services.AddSingleton(Options.Create(emailConfiguration));
    
    // Az implementációk regisztrálása transientként (kérésenként, szálbiztos)
    builder.Services.AddTransient<IEmailSender>(emailConfiguration.GetInstance);
    builder.Services.AddTransient<IEmailSender<ApplicationUser>>(emailConfiguration.GetInstance);
    
    return builder;
}
```

**Élettartam-elemzés:**
- ✅ **EmailConfiguration**: **Singletonként** regisztrálva - A konfiguráció megváltoztathatatlan és környezetfüggő
- ✅ **IEmailSender/IEmailSender<ApplicationUser>**: **Transientként** regisztrálva - Kérésenként új példány jön létre a szálbiztonság és az erőforrások megfelelő felszabadítása érdekében
- ✅ **LoggerEmailSender**: Kérésenként jön létre (Transient, gyáron keresztül)
- ✅ **SmtpEmailSender**: Kérésenként jön létre (Transient, gyáron keresztül), a SmtpEmailConfiguration beállításainak befecskendezésével

## Tesztkészlet

### Templates tesztek (`TemplatesTests.cs`)
Átfogó tesztlefedettség a Templates gyűjteményosztályhoz (22 teszt):

**Add(Template) tesztek:**
- `Add_Template_MakesItRetrievableByName` - A hozzáadás-majd-lekérés minta ellenőrzése
- `Add_Template_Null_ThrowsArgumentNullException` - Null-ellenőrzés
- `Add_Template_MultipleTemplates_AreAllEnumerable` - LINQ/gyűjtemény támogatás

**Add(name, content) tesztek:**
- `Add_NameAndContent_CreatesSimpleTemplate` - A kényelmi metódus működik
- `Add_NameAndContent_NullName_ThrowsArgumentNullException` - Névellenőrzés
- `Add_NameAndContent_NullContent_ThrowsArgumentNullException` - Tartalomellenőrzés

**Remove(name) tesztek:**
- `Remove_Name_RemovesMatchingTemplate` - Sikeres eltávolítás
- `Remove_Name_LeavesOtherTemplatesUntouched` - Nincsenek mellékhatások
- `Remove_Name_NonExistentName_DoesNothing` - Idempotens
- `Remove_Name_Null_ThrowsArgumentNullException` - Null-ellenőrzés

**Remove(Template) tesztek:**
- `Remove_Template_RemovesExactInstance` - Hivatkozás szerinti egyenlőség
- `Remove_Template_NotAdded_DoesNothing` - Biztonságos, ha nincs jelen
- `Remove_Template_Null_ThrowsArgumentNullException` - Null-ellenőrzés

**Felsorolási tesztek:**
- `GetEnumerator_NoTemplates_YieldsNothing` - Üres gyűjtemény
- `GetEnumerator_Generic_MatchesNonGeneric` - IEnumerable megfelelőség

**Get tesztek:**
- `Get_ExistingName_ReturnsSomeWithTemplate` - Sikeres eset (Option)
- `Get_UnknownName_ReturnsNone` - A hiányzó sablon kezelése
- `Get_DuplicateNames_ThrowsInvalidOperationException` - Duplikátumok felismerése

**GetOrDefault tesztek:**
- `GetOrDefault_ExistingName_ReturnsMatchingTemplate` - Sikeres eset
- `GetOrDefault_UnknownName_ReturnsDefaultTemplate` - A tartalék működik
- `GetOrDefault_NullName_ThrowsArgumentNullException` - Argumentumellenőrzés
- `GetOrDefault_NullDefaultTemplate_ThrowsArgumentNullException` - Argumentumellenőrzés

**Tesztek eredménye:**
- **Összesen**: 22 teszt
- **Sikeres**: 22 ✓
- **Sikertelen**: 0

### E-mail integrációs tesztek

#### 1. `EmailSenderExtensionsTests.cs`
A kiterjesztő metódus gyárlogikáját teszteli:
- `GetInstance_WithLoggerEmailConfiguration_ReturnsLoggerEmailSender` ✓
- `GetInstance_WithSmtpEmailConfiguration_ReturnsSmtpEmailSender` ✓
- `GetInstance_WithUnsupportedConfiguration_ThrowsNotSupportedException` ✓
- `GetInstance_LoggerEmailConfiguration_DoesNotRequireSmtpOptions` ✓

#### 2. `LoggerEmailSenderTests.cs`
A naplózó alapú e-mail-küldőt teszteli:
- `SendEmailAsync_LogsEmailInformation` ✓
- `SendEmailAsync_CompletesSuccessfully` ✓
- `SendConfirmationLinkAsync_UsesCorrectTemplate` ✓
- `SendPasswordResetCodeAsync_UsesCorrectTemplate` ✓
- `SendPasswordResetLinkAsync_UsesCorrectTemplate` ✓

#### 3. `SmtpEmailSenderTests.cs`
Az SMTP alapú e-mail-küldőt teszteli:
- `SendEmailAsync_WithValidSmtpConfiguration_LogsSuccessMessage` ✓
- `SendEmailAsync_WithNoAuthentication_DoesNotSetCredentials` ✓
- `SendConfirmationLinkAsync_CreatesCorrectEmailContent` ✓
- `SendPasswordResetCodeAsync_CreatesCorrectEmailContent` ✓
- `SendPasswordResetLinkAsync_CreatesCorrectEmailContent` ✓

#### 4. `WebApiExtensionsEmailTests.cs`
Integrációs tesztek a DI-konfigurációhoz:
- `AddEmail_RegistersEmailSenderAsIEmailSender` ✓
- `AddEmail_WithLoggerEmailConfiguration_ReturnsLoggerEmailSender` ✓
- `AddEmail_WithSmtpEmailConfiguration_ReturnsSmtpEmailSender` ✓
- `AddEmail_EmailSenderRegisteredAsTransient` ✓
- `AddEmail_CanResolveMultipleEmailSenderInstances` ✓

### Tesztek eredménye
- **Összes teszt**: 21
- **Sikeres**: 21 ✓
- **Sikertelen**: 0
- **Időtartam**: ~251 ms

**E-mail + Templates tesztek összesen**: 43 teszt, mind sikeres

## Konfiguráció

### Az EmailConfiguration beállításai
Az e-mail-konfiguráció két szolgáltatót támogat, opcionális sablontestreszabással:

#### Logger konfiguráció (fejlesztés)
```json
{
  "Email": {
    "Provider": "Logger"
  }
}
```

#### SMTP konfiguráció (éles üzem) - Alapértelmezett sablonokkal
```json
{
  "Email": {
    "Provider": "Smtp",
    "SmtpServer": "smtp.example.com",
    "Port": 587,
    "From": "noreply@example.com",
    "Ssl": {
      "Enabled": true
    },
    "Authentication": {
      "Type": "Basic",
      "Username": "user@example.com",
      "Password": "secure-password"
    }
  }
}
```

#### SMTP konfiguráció - Egyéni sablonokkal
```json
{
  "Email": {
    "Provider": "Smtp",
    "SmtpServer": "smtp.example.com",
    "Port": 587,
    "Ssl": {
      "Enabled": true
    },
    "Authentication": {
      "Type": "No"
    },
    "Templates": {
      "ConfirmationEmail": "<html><body>Confirm {{Email}} at <a href=\"{{ConfirmationLink}}\">here</a></body></html>",
      "PasswordResetCodeEmail": "<html><body>Your code is {{ResetCode}}</body></html>",
      "PasswordResetLinkEmail": "<html><body>Click <a href=\"{{ResetLink}}\">here</a> to reset</body></html>"
    }
  }
}
```

**A sablonok felülbírálásának működése:**
- Ha a konfigurációban meg van adva a `Templates`, az SmtpEmailSender azokat a sablonokat használja
- A `Templates` szakaszból hiányzó sablonoknál a `DefaultEmailTemplates` szolgál tartalékként
- A LoggerEmailSender mindig a `DefaultEmailTemplates` sablonjait használja (nem támogatja a felülbírálást)
- A paraméterek behelyettesítése az alapértelmezett és az egyéni sablonokban egyformán működik

### Paraméterek referenciája

**A ConfirmationEmail paraméterei:**
- `{{Email}}` - A felhasználó e-mail-címe
- `{{ConfirmationLink}}` - A megerősítő tokent tartalmazó URL

**A PasswordResetCodeEmail paraméterei:**
- `{{Email}}` - A felhasználó e-mail-címe
- `{{ResetCode}}` - Numerikus vagy alfanumerikus visszaállító kód

**A PasswordResetLinkEmail paraméterei:**
- `{{Email}}` - A felhasználó e-mail-címe
- `{{ResetLink}}` - A visszaállító tokent tartalmazó URL

## Használt tervezési minták

### 1. Templates gyűjtemény minta
A `Templates` osztály speciális gyűjteményt biztosít a megváltoztathatatlan e-mail sablonok kezelésére:
- **Option-alapú API**: a `Get(string)` `Option<Template>`-et ad vissza a funkcionális hibakezeléshez
- **Alapértelmezést ismerő lekérés**: a `GetOrDefault` biztonságos tartalékmintákat tesz lehetővé
- **Megváltoztathatatlan műveletek**: a sablonok csak olvashatók; a gyűjtemény megőrzi a hivatkozások integritását
- **IEnumerable megfelelőség**: teljes LINQ-támogatás a belső lista védelme mellett

**Példa a mintára:**
```csharp
var customTemplates = new Templates();
customTemplates.Add("WelcomeEmail", "<p>Welcome {{Name}}!</p>");

var template = customTemplates
    .Get("WelcomeEmail")
    .Match(
        none: () => DefaultEmailTemplates.ConfirmationEmail,
        some: t => t
    );
```

### 2. Paraméterek behelyettesítése megváltoztathatatlan láncon keresztül
A `SimpleTemplate.AddParameter` metódus új sablonpéldányt ad vissza:
```csharp
var rendered = template
    .AddParameter(new Parameter("field1", "value1"))
    .AddParameter(new Parameter("field2", "value2"))
    .Render();
```
- Minden `AddParameter` új, megváltoztathatatlan SimpleTemplate-et hoz létre
- Az eredeti sablon változatlan marad (funkcionális tisztaság)
- A paraméterek a hívás sorrendjében érvényesülnek

### 3. Kiterjesztő metódusok
Az `extension(T)` szintaxis letisztult DSL-t biztosít a típusok konfigurációs kiegészítéséhez:
```csharp
extension(EmailConfiguration configuration)
{
    public EmailSender GetInstance(IServiceProvider serviceProvider) => ...
}
```

### 4. Factory minta
A `GetInstance` kiterjesztő metódus gyárként működik: a konfiguráció típusa alapján a megfelelő EmailSender példányokat hozza létre, a függőségek feloldásával.

### 5. Függőséginjektálás
- Konfigurációvezérelt példányosítás
- Service provider befecskendezése a függőségek feloldásához
- Transient élettartam az állapotmentes szolgáltatásokhoz
- Opcionális Templates felülbírálás konfigurációs szinten

### 6. Template Method minta
Az absztrakt `EmailSender` osztály sablonmetódusokat biztosít a gyakori e-mail-helyzetekhez, miközben lehetővé teszi, hogy az alosztályok valósítsák meg a tényleges küldési logikát, és testreszabják a sablonfeloldást.

### 7. Strategy minta (konfigurációalapú)
Sablonfeloldási stratégiák az `Option<Templates>` segítségével:
- **Alapértelmezett stratégia**: közvetlenül a `DefaultEmailTemplates` használata
- **Felülbíráló stratégia**: egyéni Templates használata, az alapértelmezettekre való tartalékolással

## Használati példák

### 1. példa: Alapértelmezett sablonok használata a Logger-rel
```csharp
// Konfiguráció (fejlesztés/appsettings.Development.json)
// Nincsenek egyéni sablonok - a DefaultEmailTemplates kerül felhasználásra

// A kódjában
var emailSender = serviceProvider.GetRequiredService<IEmailSender>();
var user = new ApplicationUser { DisplayName = "Alice" };

await emailSender.SendConfirmationLinkAsync(
    user, 
    "alice@example.com", 
    "https://app.example.com/confirm?token=abc123"
);
// Kimenet: a Logger a konzolra írja a megerősítő e-mailt
```

### 2. példa: Egyéni sablonok konfiguráción keresztül
```csharp
// Konfiguráció (email.json)
{
  "Email": {
    "Provider": "Smtp",
    "SmtpServer": "smtp.gmail.com",
    "Port": 587,
    "Ssl": { "Enabled": true },
    "Authentication": { "Type": "Basic", "Username": "...", "Password": "..." },
    "Templates": {
      "ConfirmationEmail": "<h1>Welcome!</h1><p>Confirm <a href='{{ConfirmationLink}}'>here</a></p>"
    }
  }
}

// Az SmtpEmailSender automatikusan az egyéni sablont használja, ha az jelen van,
// a hiányzó sablonoknál a DefaultEmailTemplates szolgál tartalékként
```

### 3. példa: Programozott sablonkezelés
```csharp
var templates = new Templates();

templates.Add("WelcomeEmail", "<p>Welcome {{Name}}!</p>");
templates.Add("OrderConfirmation", "<p>Order #{{OrderId}} confirmed</p>");

var template = templates.GetOrDefault(
    "WelcomeEmail",
    new SimpleTemplate("fallback", "Default message")
);

var html = template
    .AddParameter(new Parameter("Name", "Bob"))
    .Render();
```

### 4. példa: Biztonságos sablonlekérés Option-nel
```csharp
var templates = new Templates();
templates.Add("greeting", "Hello {{name}}!");

var greeting = templates
    .Get("greeting")
    .Match(
        none: () => new SimpleTemplate("default", "No greeting available"),
        some: t => t
    )
    .AddParameter(new Parameter("name", "World"))
    .Render();

// Eredmény: "Hello World!"
```

A megvalósítás könnyű bővíthetőségre készült:

### Új e-mail-szolgáltató hozzáadása
1. Hozzon létre egy új `EmailConfiguration` record-ot:
   ```csharp
   public sealed record SendGridEmailConfiguration(...) : EmailConfiguration("SendGrid");
   ```

2. Hozzon létre egy új `EmailSender` implementációt:
   ```csharp
   public sealed class SendGridEmailSender : EmailSender { ... }
   ```

3. Frissítse a `GetInstance` kiterjesztő metódust:
   ```csharp
   SendGridEmailConfiguration _ => new SendGridEmailSender(...),
   ```

4. Adjon hozzá megfelelő teszteket a tesztkészlethez

### Új e-mail sablonok hozzáadása
1. Adjon új metódusokat az absztrakt `EmailSender` osztályhoz
2. Szükség szerint valósítsa meg őket az alosztályokban
3. Adjon hozzá teszteket minden megvalósításhoz

## Bevált gyakorlatok

✅ **Megváltoztathatatlan konfiguráció**: Az EmailConfiguration és a Templates lezárt record-ok/osztályok (megváltoztathatatlanok)
✅ **Funkcionális Templates API**: Option-alapú lekérés Match-csel a biztonságos hibakezelésért
✅ **Függőséginjektálás**: Minden függőséget a DI-konténer old fel
✅ **Sablonminta**: A közös funkcionalitás az alaposztályban, a sajátosságok az alosztályokban
✅ **Hibakezelés**: Az SmtpEmailSender továbbadja az SMTP-hibákat; ArgumentNullException az érvénytelen bemenetekre
✅ **Szálbiztonság**: A Transient élettartam biztosítja, hogy a kérések között nincs megosztott állapot; a sablonok megváltoztathatatlanok
✅ **Tesztelés**: Átfogó tesztlefedettség (43 teszt) NSubstitute mockolással és xUnit-tal
✅ **Konfigurációs rugalmasság**: Több szolgáltató, szolgáltatónkénti opcionális sablon-felülbírálással
✅ **Funkcionális tisztaság**: Az `AddParameter` új sablonpéldányt ad vissza; az eredeti változatlan marad

## Ellenőrzőlista

- [x] A Templates osztály tesztjei mind sikeresek (22/22)
- [x] Az e-mail-küldő tesztek mind sikeresek (21/21)
- [x] Az összes e-mailes teszt lefordul és sikeresen lefut (összesen 43/43)
- [x] A kiterjesztő metódus megvalósítása helyes
- [x] A WebApiExtensions DI-regisztrációja megfelelően van hatókörhöz kötve
- [x] Mind a LoggerEmailSender, mind az SmtpEmailSender helyesen működik
- [x] A konfiguráció betöltése az email.json fájlból működik
- [x] Az alapértelmezett tartalék a DefaultEmailTemplates sablonjaira működik
- [x] Az egyéni sablonok felülbírálása működik a SmtpEmailConfiguration.Templates útján
- [x] Az e-mail sablonok helyesen generálódnak a paraméterek behelyettesítésével
- [x] A service provider általi feloldás működik a transient szolgáltatásokra
- [x] Az Option-alapú sablonlekérés biztonságos és funkcionális
- [x] Az alkalmazás figyelmeztetések nélkül épül fel és fut

