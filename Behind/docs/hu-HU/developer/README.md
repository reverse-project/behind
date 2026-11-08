# Behind kódbázis dokumentáció

Átfogó dokumentáció a Behind projekthez – egy modern, tiszta architektúrájú .NET alkalmazáshoz, amely a funkcionális programozási paradigmákat, a SOLID elveket és a Clean Code gyakorlatokat követi.

## 📚 Dokumentációs tartalomjegyzék

### Alapdokumentáció
- **[PROJECT_OVERVIEW.md](./PROJECT_OVERVIEW.md)** - A projekt felépítése, a technológiai stack és az általános architektúra
- **[DESIGN_PATTERNS.md](./DESIGN_PATTERNS.md)** - Architekturális minták és tervezési döntések
- **[CODING_STANDARDS.md](./CODING_STANDARDS.md)** - Kötelező kódolási szabványok, konvenciók és ajánlott gyakorlatok
- **[FUNCTIONAL_PROGRAMMING.md](./FUNCTIONAL_PROGRAMMING.md)** - A kódbázisban használt funkcionális programozási minták és monádok

### Architektúra útmutatók
- **[DATABASE_ARCHITECTURE.md](./DATABASE_ARCHITECTURE.md)** - Adatbázisréteg, Entity Framework minták és repository-tervezés
- **[WEBAPI_CONVENTIONS.md](./WEBAPI_CONVENTIONS.md)** - Web API konfiguráció, middleware és OpenAPI dokumentáció
- **[DOMAIN_MODEL.md](./DOMAIN_MODEL.md)** - Domain modellek, aggregátumok és entitástervezés
- **[EMAIL_SENDER_IMPLEMENTATION.md](./EMAIL_SENDER_IMPLEMENTATION.md)** - Az e-mail-küldő konfigurációja, megvalósítási részletei és használati útmutatója

### Üzemeltetési útmutatók
- **[LIMITATIONS_AND_ISSUES.md](./LIMITATIONS_AND_ISSUES.md)** - Ismert korlátozások, aktuális hibák és függőségi sebezhetőségek
- **[GETTING_STARTED.md](./GETTING_STARTED.md)** - Telepítés, fordítás, futtatás és fejlesztési munkafolyamat

## 🎯 Gyors navigáció

**Új a projektben?** Kezdje a [PROJECT_OVERVIEW.md](./PROJECT_OVERVIEW.md) fájllal

**Meg szeretné érteni a kódolási mintákat?** Olvassa el a [CODING_STANDARDS.md](./CODING_STANDARDS.md) és a [FUNCTIONAL_PROGRAMMING.md](./FUNCTIONAL_PROGRAMMING.md) fájlt

**Adatokkal kell dolgoznia?** Lásd a [DATABASE_ARCHITECTURE.md](./DATABASE_ARCHITECTURE.md) fájlt

**API-kat fejleszt?** Tekintse meg a [WEBAPI_CONVENTIONS.md](./WEBAPI_CONVENTIONS.md) fájlt

**Tisztában van az aktuális hibákkal?** Nézze át a [LIMITATIONS_AND_ISSUES.md](./LIMITATIONS_AND_ISSUES.md) fájlt

## 🏗️ Projektstruktúra

```
Behind/
├── src/
│   ├── Aspire/              # .NET Aspire orkesztráció
│   ├── Common/              # Megosztott könyvtárak
│   │   ├── Behind.Common    # Alapvető funkcionális minták és segédeszközök
│   │   ├── Behind.Common.Database    # Adatbázis-absztrakciók
│   │   └── Behind.Common.WebApi      # WebApi segédeszközök
│   ├── Core/                # Alapvető üzleti logika
│   │   ├── Behind.Core.Common        # Core domain segédeszközök
│   │   ├── Behind.Core.Domain        # Domain modellek és entitások
│   │   ├── Behind.Core.Database      # Adatbázis-implementációk
│   │   ├── Behind.Core.Database.Migrator # EF migrációk
│   │   ├── Behind.Core.Tools         # CLI eszközök
│   │   └── Behind.Core.WebApi        # Core API vezérlők
│   └── Web/                 # Webalkalmazás
├── tests/                   # Tesztprojektek
└── docs/
    ├── en-US/               # Angol nyelvű dokumentáció
    │   ├── developer/       # Ez a dokumentáció
    │   └── user-manual/     # Felhasználói kézikönyvek
    └── hu-HU/               # Magyar nyelvű dokumentáció (azonos struktúrával)

```

## 🔑 Alapelvek

A Behind kódbázis az alábbi alapelvekre épül:

1. **Funkcionális programozás az első** - A monádok (Option, Try, Exceptional, Validation), a megváltoztathatatlan adatstruktúrák és a tiszta függvények intenzív használata
2. **Tiszta architektúra** - A felelősségi körök egyértelmű szétválasztása külön rétegekkel a Common, a Core Domain és a Core API számára
3. **DRY (Don't Repeat Yourself)** - Megosztott kód a Common és a Core.Common könyvtárakban
4. **SOLID elvek** - Egyetlen felelősség, nyitott/zárt, Liskov-féle helyettesítés, interfészek szétválasztása, függőségek megfordítása
5. **Clean Code** - Önmagát dokumentáló kód; szükségtelen megjegyzések nélkül
6. **Típusbiztonság** - Erős típusosság rekordokkal és megváltoztathatatlan objektumokkal
7. **Szálbiztonság** - Async/await minták megfelelő megszakítási támogatással

## 📦 Technológiai stack

- **Keretrendszer**: .NET 10 ASP.NET Core-ral
- **Adatbázis**: Entity Framework Core 10.0.10 SQLite és PostgreSQL támogatással
- **Hitelesítés**: JWT ****** **Biztonság**: BCrypt a jelszavak kivonatolásához, TOTP támogatás
- **Megfigyelhetőség**: OpenTelemetry a nyomkövetéshez, a metrikákhoz és a naplózáshoz
- **API-dokumentáció**: OpenAPI Scalar felhasználói felülettel
- **Tesztelés**: xUnit NSubstitute-tal a mockoláshoz
- **Orkesztráció**: .NET Aspire mikroszolgáltatásokhoz

## 🚀 Első lépések

1. Tekintse át a [CODING_STANDARDS.md](./CODING_STANDARDS.md) fájlt a kötelező minták megismeréséhez
2. Ismerkedjen meg a [FUNCTIONAL_PROGRAMMING.md](./FUNCTIONAL_PROGRAMMING.md) fájllal a monadikus minták elsajátításához
3. Nézze meg a [DESIGN_PATTERNS.md](./DESIGN_PATTERNS.md) fájlt az architekturális döntésekért
4. A telepítési útmutatóért lásd a [GETTING_STARTED.md](./GETTING_STARTED.md) fájlt

## ⚠️ Fontos megjegyzések

- **Nincsenek megjegyzések a kódban** - Ha a kódhoz megjegyzés kell, az rossz kód. A kódnak önmagát kell dokumentálnia.
- **Bővítő metódusok** - Használja a C# 12 `extension` szintaxisát, a fájlok neve pedig `Extensions` végződésű legyen
- **Megváltoztathatatlanság** - Részesítse előnyben a rekordokat, a megváltoztathatatlan objektumokat és a funkcionális megközelítéseket
- **Tesztek kötelezők** - Minden új funkcionalitáshoz egységteszteket kell mellékelni
- **Ismert hibák** - A függőségi sebezhetőségekkel kapcsolatban lásd a [LIMITATIONS_AND_ISSUES.md](./LIMITATIONS_AND_ISSUES.md) fájlt

## 📖 Dokumentációs verziók

Ez a dokumentáció a kódbázis aktuális állapotát tükrözi a legutóbbi commit szerint.

---

**Utolsó frissítés**: 2026-09-13  
**Keretrendszer verziója**: .NET 10  
**Dokumentáció felépítése**: Tiszta, moduláris Markdown fájlok
