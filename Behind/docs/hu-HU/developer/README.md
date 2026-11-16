# Behind kódbázis dokumentáció

Átfogó dokumentáció a Behind projekthez – egy modern, tiszta architektúrájú .NET alkalmazáshoz, amely a funkcionális programozási paradigmákat, a SOLID elveket és a Clean Code gyakorlatokat követi.

## 📚 Dokumentációs tartalomjegyzék

### Alapdokumentáció
- **[Projektáttekintés](./PROJECT_OVERVIEW.md)** - A projekt felépítése, a technológiai stack és az általános architektúra
- **[Tervezési minták](./DESIGN_PATTERNS.md)** - Architekturális minták és tervezési döntések
- **[Kódolási szabványok](./CODING_STANDARDS.md)** - Kötelező kódolási szabványok, konvenciók és ajánlott gyakorlatok
- **[Funkcionális programozás](./FUNCTIONAL_PROGRAMMING.md)** - A kódbázisban használt funkcionális programozási minták és monádok

### Architektúra útmutatók
- **[Adatbázis-architektúra](./DATABASE_ARCHITECTURE.md)** - Adatbázisréteg, Entity Framework minták és repository-tervezés
- **[Web API-konvenciók](./WEBAPI_CONVENTIONS.md)** - Web API konfiguráció, middleware és OpenAPI dokumentáció
- **[Domainmodell](./DOMAIN_MODEL.md)** - Domain modellek, aggregátumok és entitástervezés
- **[E-mail-küldő megvalósítása](./EMAIL_SENDER_IMPLEMENTATION.md)** - Az e-mail-küldő konfigurációja, megvalósítási részletei és használati útmutatója

### Üzemeltetési útmutatók
- **[Korlátozások és problémák](./LIMITATIONS_AND_ISSUES.md)** - Ismert korlátozások, aktuális hibák és függőségi sebezhetőségek
- **[Első lépések](./GETTING_STARTED.md)** - Telepítés, fordítás, futtatás és fejlesztési munkafolyamat
- **[Behind Core Tools](./tools/README.md)** - Indítási konfigurációhoz, TOTP titkokhoz és kiadási jegyzetekhez használható parancsok
- **[Konténerközzétételi szkriptek](./tools/container-publishing.md)** - Az alkalmazás konténerképeinek elkészítése és közzététele

## 🎯 Gyors navigáció

**Új a projektben?** Kezdje a [Projektáttekintéssel](./PROJECT_OVERVIEW.md)

**Meg szeretné érteni a kódolási mintákat?** Olvassa el a [Kódolási szabványokat](./CODING_STANDARDS.md) és a [Funkcionális programozást](./FUNCTIONAL_PROGRAMMING.md)

**Adatokkal kell dolgoznia?** Lásd az [Adatbázis-architektúrát](./DATABASE_ARCHITECTURE.md)

**API-kat fejleszt?** Tekintse meg a [Web API-konvenciókat](./WEBAPI_CONVENTIONS.md)

**Tisztában van az aktuális hibákkal?** Nézze át a [Korlátozásokat és problémákat](./LIMITATIONS_AND_ISSUES.md)

**Parancssori konfigurációra vagy kiadási jegyzetekre van szüksége?** Lásd a [Behind Core Tools](./tools/README.md) útmutatót

**Konténerképeket tesz közzé?** Lásd a [Konténerközzétételi szkriptek](./tools/container-publishing.md) útmutatót

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

- **Keretrendszer**: .NET 11 ASP.NET Core 11.0.0-val
- **Adatbázis**: Entity Framework Core 11.0.0-rc.3.26522.148 SQLite- és PostgreSQL-támogatással
- **Hitelesítés**: JWT ****** **Biztonság**: BCrypt a jelszavak kivonatolásához, TOTP támogatás
- **Megfigyelhetőség**: OpenTelemetry 1.17.0 a nyomkövetéshez, a metrikákhoz és a naplózáshoz
- **API-dokumentáció**: OpenAPI Scalar felhasználói felülettel
- **Tesztelés**: xUnit NSubstitute-tal a mockoláshoz
- **Orkesztráció**: .NET Aspire 13.6.5 a szolgáltatások orkesztrálásához

## 🚀 Első lépések

1. Tekintse át a [Kódolási szabványokat](./CODING_STANDARDS.md) a kötelező minták megismeréséhez
2. Ismerkedjen meg a [Funkcionális programozással](./FUNCTIONAL_PROGRAMMING.md) a monadikus minták elsajátításához
3. Nézze meg a [Tervezési mintákat](./DESIGN_PATTERNS.md) az architekturális döntésekért
4. Az útmutatóért lásd az [Első lépéseket](./GETTING_STARTED.md)
5. A konfigurációk, TOTP titkok vagy kiadási jegyzetek generálásához használja a [Behind Core Tools](./tools/README.md) eszközt
6. A konténerképek közzétételéhez használja a [Konténerközzétételi szkripteket](./tools/container-publishing.md)

## ⚠️ Fontos megjegyzések

- **Nincsenek megjegyzések a kódban** - Ha a kódhoz megjegyzés kell, az rossz kód. A kódnak önmagát kell dokumentálnia.
- **Bővítő metódusok** - Használja a C# 14+ `extension(Type)` szintaxisát, a fájlok neve pedig `Extensions` végződésű legyen
- **Megváltoztathatatlanság** - Részesítse előnyben a rekordokat, a megváltoztathatatlan objektumokat és a funkcionális megközelítéseket
- **Tesztek kötelezők** - Minden új funkcionalitáshoz egységteszteket kell mellékelni
- **Ismert hibák** - A függőségi sebezhetőségekkel kapcsolatban lásd a [Korlátozások és problémák](./LIMITATIONS_AND_ISSUES.md) leírást

## 📖 Dokumentációs verziók

Ez a dokumentáció a kódbázis aktuális állapotát tükrözi a legutóbbi commit szerint.

---

**Utolsó frissítés**: 2026-11-12
**Keretrendszer verziója**: .NET 11
**Dokumentáció felépítése**: Tiszta, moduláris Markdown fájlok
