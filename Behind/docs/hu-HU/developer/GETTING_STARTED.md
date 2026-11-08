# Első lépések

Gyorsindítási útmutató a Behind projekt beállításához és az azon végzett fejlesztéshez.

## Előfeltételek

### Rendszerkövetelmények
- **Operációs rendszer**: Windows 10+, Ubuntu 20.04+, macOS 11+ (csak fejlesztéshez)
- **RAM**: legalább 8 GB, 16 GB ajánlott
- **.NET SDK**: 10.0 (telepítse dev containerrel vagy manuálisan)
- **Git**: 2.25+
- **Docker**: 20.10+ (opcionális, konténerizált fejlesztéshez)

### Fejlesztői környezet

#### 1. lehetőség: Dev container (ajánlott)

Előre meghatározott fejlesztői környezetet használ:

```bash
# Clone repository
git clone https://github.com/your-org/behind.git
cd behind

# VS Code will prompt to reopen in container
# Or manually: Ctrl+Shift+P > Dev Containers: Reopen in Container
```

**A dev container tartalma**:
- Előre telepített .NET 10 SDK
- Node.js és npm
- ESLint
- Git, curl, wget, gpg
- Minden szükséges eszköz

#### 2. lehetőség: Helyi telepítés

```bash
# Install .NET 10 SDK
# https://dotnet.microsoft.com/download/dotnet/10.0

# Verify installation
dotnet --version  # Should show 10.0.x

# Restore dependencies
cd Behind
dotnet restore

# Verify build
dotnet build Behind.slnx
```

## Projektstruktúra

```
Behind/
├── src/
│   ├── Aspire/              # Szolgáltatás-orkesztráció
│   ├── Common/              # Megosztott könyvtárak
│   │   ├── Behind.Common
│   │   ├── Behind.Common.Database
│   │   └── Behind.Common.WebApi
│   ├── Core/                # Domain és üzleti logika
│   │   ├── Behind.Core.Common
│   │   ├── Behind.Core.Domain
│   │   ├── Behind.Core.Database
│   │   ├── Behind.Core.Database.Migrator
│   │   ├── Behind.Core.WebApi
│   │   └── Behind.Core.Tools
│   └── Web/                 # Webalkalmazás
└── tests/
    └── Behind.Tests/        # Egységtesztek
```

## Fordítás és építés

### A megoldás fordítása

```bash
cd Behind

# Restore packages and build
dotnet build Behind.slnx

# Build specific project
dotnet build src/Core/Behind.Core.WebApi/Behind.Core.WebApi.csproj

# Release build (optimized)
dotnet build Behind.slnx -c Release

# Verbose output
dotnet build Behind.slnx --verbosity detailed
```

### Hibák ellenőrzése

```bash
# Run static analysis
dotnet build Behind.slnx /t:Analyzers

# List all compiler warnings
dotnet build Behind.slnx /p:TreatWarningsAsErrors=false
```

## Tesztek futtatása

### Az összes teszt futtatása

```bash
cd Behind

# Execute all tests
dotnet test tests/Behind.Tests/Behind.Tests.csproj

# Verbose output
dotnet test tests/Behind.Tests/Behind.Tests.csproj --verbosity normal

# Run single test class
dotnet test tests/Behind.Tests/Behind.Tests.csproj \
    --filter "FullyQualifiedName~UserRepositoryTests"

# Run with coverage
dotnet test tests/Behind.Tests/Behind.Tests.csproj
```

A lefedettséget alapértelmezés szerint a `coverlet.msbuild` gyűjti, a generált kódot (EF Core migrációk, az `obj/` alatti Roslyn source generator kimenet, valamint a `[GeneratedCode]`/`[CompilerGenerated]` attribútumú típusok) pedig automatikusan kizárja. A lefedettség beállításait lásd a [Behind.Tests.csproj](../../../tests/Behind.Tests/Behind.Tests.csproj) fájlban.

**Megjegyzés:** Ha a Test Explorer lefedettségi nézete elavult generált fájlokat mutat, zárja be és nyissa meg újra a VS Code-ot, vagy futtassa a `dotnet clean` parancsot a build gyorsítótár törléséhez.

### Figyelő mód (folyamatos tesztelés)

```bash
dotnet watch --project tests/Behind.Tests/Behind.Tests.csproj test
```

## Adatbázis beállítása

### Adatbázis létrehozása

```bash
cd Behind

# Using SQLite (development)
# Database automatically created on first run

# Using PostgreSQL: set "Provider": "Postgres" and an Npgsql connection string in database.json,
# then run Behind.Core.Database.Migrator to create the schema

# If needed, manually initialize
dotnet ef database update \
    --project src/Core/Behind.Core.Database.Sqlite \
    --startup-project src/Core/Behind.Core.Database.Migrator \
    --context CoreContextSqlite
```

A migrátor a verziózási kontextus (`VersioningContextSqlite`) migrációit is alkalmazza, és lefuttatja a VFS régi adatokat átalakító migrációját, ezért az adatbázis frissítésének előnyben részesített módja a `Behind.Core.Database.Migrator` futtatása.

### Migrációk létrehozása

```bash
cd Behind

# Create new migration (core context)
dotnet ef migrations add "AddUserTable" \
    --project src/Core/Behind.Core.Database.Sqlite \
    --startup-project src/Core/Behind.Core.Database.Migrator \
    --context CoreContextSqlite \
    --output-dir Migrations

# Create new migration (versioning context)
dotnet ef migrations add "AddSomething" \
    --project src/Core/Behind.Core.Database.Sqlite \
    --startup-project src/Core/Behind.Core.Database.Migrator \
    --context VersioningContextSqlite \
    --output-dir Migrations/Versioning

# Remove last migration (before applied)
dotnet ef migrations remove \
    --project src/Core/Behind.Core.Database.Sqlite \
    --startup-project src/Core/Behind.Core.Database.Migrator \
    --context CoreContextSqlite
```

Az eszközt egyszer telepítse a `dotnet tool install --global dotnet-ef` paranccsal.

PostgreSQL esetén ugyanezeket a parancsokat futtassa a `--project src/Core/Behind.Core.Database.Postgres` kapcsolóval és a `CoreContextPostgres` / `VersioningContextPostgres` kontextusokkal, a migrációt pedig mindkét szolgáltatói projekthez adja hozzá.

### Kapcsolati karakterláncok

**Fejlesztés** (appsettings.Development.json):
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=app.db"
  }
}
```

**Éles környezet** (appsettings.json):
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=db.example.com;Port=5432;Database=behind;User=postgres;******"
  }
}
```

## Az alkalmazás futtatása

### 1. lehetőség: Közvetlen futtatás

```bash
cd Behind

# Run the API service
dotnet run \
    --project src/Core/Behind.Core.WebApi/Behind.Core.WebApi.csproj

# Application runs at https://localhost:5001
```

### 2. lehetőség: Aspire-orkesztráció (ajánlott)

```bash
cd Behind/src/Aspire

# Run Aspire dashboard and services
dotnet run --project Behind.AppHost/Behind.AppHost.csproj

# Services coordinated automatically via Aspire
# Dashboard at http://localhost:18888
```

### 3. lehetőség: Docker

```bash
# Build Docker image
docker build -t behind:latest .

# Run container
docker run -p 5001:5001 \
    -e ConnectionStrings__DefaultConnection="Data Source=/data/app.db" \
    -v behind-data:/data \
    behind:latest
```

## API-hozzáférés

### OpenAPI dokumentáció

```
https://localhost:5001/openapi/v1.json  # OpenAPI specification
https://localhost:5001/scalar/v1         # Scalar UI
https://localhost:5001/swagger/v1        # Swagger UI (if enabled)
```

### Példa API-hívások

```bash
# Get user (requires auth)
curl -X GET https://localhost:5001/api/users/123 \
    -H "Authorization: ******"

# Create user (public)
curl -X POST https://localhost:5001/api/users \
    -H "Content-Type: application/json" \
    -d '{
      "email": "user@example.com",
      "displayName": "John Doe",
      "password": "Secure@Pass123"
    }'

# Health check
curl https://localhost:5001/health
```

## Naplózás és hibakeresés

### Naplók megtekintése

```bash
# Real-time log output
dotnet run --project src/Core/Behind.Core.WebApi/Behind.Core.WebApi.csproj

# Structured logging
export ASPNETCORE_ENVIRONMENT=Development
dotnet run
```

### Hibakeresés a VS Code-ban

**Indítási konfiguráció** (.vscode/launch.json):
```json
{
    "version": "0.2.0",
    "configurations": [
        {
            "name": ".NET Core Launch (web)",
            "type": "coreclr",
            "request": "launch",
            "program": "${workspaceFolder}/Behind/bin/Debug/net10.0/Behind.Core.WebApi.dll",
            "args": [],
            "cwd": "${workspaceFolder}/Behind",
            "stopAtEntry": false,
            "serverReadyAction": {
                "pattern": "\\bNow listening on:\\s+(https?://\\S+)",
                "uriFormat": "{0}",
                "action": "openExternally"
            }
        }
    ]
}
```

## Fejlesztési munkafolyamat

### 1. Feature ág létrehozása

```bash
git checkout -b feature/user-authentication
```

### 2. Módosítások elvégzése

```bash
# Edit files following CODING_STANDARDS.md
# Create tests in tests/Behind.Tests/
# Update documentation if needed
```

### 3. Tesztek futtatása

```bash
dotnet test tests/Behind.Tests/Behind.Tests.csproj
```

### 4. Build ellenőrzése

```bash
dotnet build Behind.slnx
dotnet build Behind.slnx -c Release
```

### 5. Commit és push

```bash
git add .
git commit -m "feat: add user authentication"
git push origin feature/user-authentication
```

### 6. Pull request létrehozása

- Hivatkozzon a kapcsolódó hibajegyekre
- Írja le a változtatásokat és azok indokát
- Győződjön meg arról, hogy a CI/CD sikeresen lefut
- Kérjen átnézést a csapattól

## Kódminőség

### Statikus elemzés

```bash
# Run code analyzers (built-in)
dotnet build Behind.slnx /t:Analyzers

# Format code
dotnet format Behind.slnx

# Check formatting only
dotnet format Behind.slnx --verify-no-changes
```

### Tesztlefedettség

```bash
dotnet test tests/Behind.Tests/Behind.Tests.csproj
```

A lefedettséget a `coverlet.msbuild` automatikusan gyűjti. A generált kódot (EF Core migrációk, fordító által vagy source generatorral generált fájlok) a [Behind.Tests.csproj](../../../tests/Behind.Tests/Behind.Tests.csproj) fájlban található MSBuild-tulajdonságok zárják ki. Ha a Test Explorer elavult lefedettségi adatokat mutat, futtassa a `dotnet clean` parancsot, és nyissa meg újra a VS Code-ot.

### Kódellenőrzési (code review) ellenőrzőlista

- [ ] Megfelel a CODING_STANDARDS.md fájlnak
- [ ] Nincsenek megjegyzések (a kód önmagát dokumentálja)
- [ ] Bővítő metódusokat használ (C# 12 szintaxis)
- [ ] Megváltoztathatatlan rekordok/objektumok
- [ ] Async/await az I/O-hoz
- [ ] Funkcionális minták (Option, Try, Validation)
- [ ] Tesztek a nyilvános metódusokhoz
- [ ] Nincs kódduplikáció
- [ ] Egyetlen felelősség
- [ ] SOLID elvek alkalmazása

## Gyakori feladatok

### Új végpont hozzáadása

1. **DTO létrehozása** (a Behind.Core.WebApi-ban):
```csharp
public record CreateUserDto(string Email, string DisplayName);
```

2. **Domain entitás létrehozása** (a Behind.Core.Domain-ben):
```csharp
public sealed record HumanUser : User { }
```

3. **Repository létrehozása** (a Behind.Core.Database-ben):
```csharp
public class UserRepository : IRepository<User> { }
```

4. **Validátor létrehozása** (a Behind.Core.Database-ben):
```csharp
public static class UserValidation
{
    public static Validation<HumanUser> ValidateNew(...) { }
}
```

5. **Vezérlőművelet létrehozása** (a Behind.Core.WebApi-ban):
```csharp
[HttpPost]
public async Task<IActionResult> CreateAsync(CreateUserDto dto) { }
```

6. **Tesztek hozzáadása**:
```csharp
public class UserControllerTests { }
```

### Adatbázis-migráció hozzáadása

```bash
cd Behind

dotnet ef migrations add "AddUserTable" \
    --project src/Core/Behind.Core.Database.Sqlite \
    --startup-project src/Core/Behind.Core.Database.Migrator \
    --context CoreContextSqlite \
    --output-dir Migrations
```

A migráció alkalmazásához futtassa a `Behind.Core.Database.Migrator` programot.

### Szolgáltatásfüggőség hozzáadása

**Regisztráció a Startupban**:
```csharp
extension(IServiceCollection)
{
    public IServiceCollection AddMyService(this IServiceCollection services)
    {
        services.AddScoped<IMyService, MyService>();
        return services;
    }
}
```

**Injektálás a felhasználó osztályban**:
```csharp
public class MyController
{
    private readonly IMyService service;
    
    public MyController(IMyService service)
    {
        this.service = service;
    }
}
```

## Hibaelhárítás

### A fordítás sikertelen

```bash
# Clean build
dotnet clean Behind.slnx
dotnet restore Behind.slnx
dotnet build Behind.slnx

# Check SDK version
dotnet --version

# Verify no corrupted packages
rm -r ~/.nuget/packages
dotnet restore
```

### A tesztek sikertelenek

```bash
# Run with verbose output
dotnet test tests/Behind.Tests/Behind.Tests.csproj --verbosity normal

# Run single test
dotnet test tests/Behind.Tests/Behind.Tests.csproj \
    --filter "TestClassName.TestMethodName"

# Check test database
# SQLite in-memory used for tests
```

### Adatbázis-problémák

```bash
# View current migrations
dotnet ef migrations list \
    --project src/Core/Behind.Core.Database.Sqlite \
    --startup-project src/Core/Behind.Core.Database.Migrator \
    --context CoreContextSqlite

# Revert to previous migration
dotnet ef database update "LastGoodMigration" \
    --project src/Core/Behind.Core.Database.Sqlite \
    --startup-project src/Core/Behind.Core.Database.Migrator \
    --context CoreContextSqlite

# Start from scratch: delete the SQLite file (see database.json) and run the migrator
dotnet run --project src/Core/Behind.Core.Database.Migrator
```

### A port már használatban van

```bash
# Specify different port
ASPNETCORE_URLS="https://localhost:5002" dotnet run

# Or in launchSettings.json
{
  "applicationUrl": "https://localhost:5002"
}
```

## Teljesítménytippek

### A buildidők optimalizálása
```bash
# Use incremental builds
dotnet build --incremental

# Parallel build
dotnet build -m -j 4

# Skip unnecessary projects
dotnet build src/Core/Behind.Core.WebApi
```

### A tesztvégrehajtás optimalizálása
```bash
# Run tests in parallel
dotnet test --parallel

# Filter to specific category
dotnet test --filter "Category=UnitTest"

# Run only tests in specific file
dotnet test tests/Behind.Tests/UserRepositoryTests.cs
```

## Környezeti változók

**Fejlesztés**:
```bash
export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS="https://localhost:5001"
```

**Éles környezet**:
```bash
export ASPNETCORE_ENVIRONMENT=Production
export ASPNETCORE_URLS="https://0.0.0.0:80"
export ConnectionStrings__DefaultConnection="Host=db;Database=behind"
```

## CI/CD integráció

### GitHub Actions példa

```yaml
name: Build and Test

on: [push, pull_request]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '10.0.x'
      - run: dotnet restore
      - run: dotnet build Behind.slnx
      - run: dotnet test tests/Behind.Tests/Behind.Tests.csproj
```

## Dokumentáció

- [PROJECT_OVERVIEW.md](./PROJECT_OVERVIEW.md) - Architektúra és stack
- [CODING_STANDARDS.md](./CODING_STANDARDS.md) - Kötelező szabványok
- [DESIGN_PATTERNS.md](./DESIGN_PATTERNS.md) - Architekturális minták
- [FUNCTIONAL_PROGRAMMING.md](./FUNCTIONAL_PROGRAMMING.md) - Monádminták
- [DATABASE_ARCHITECTURE.md](./DATABASE_ARCHITECTURE.md) - Adatréteg
- [WEBAPI_CONVENTIONS.md](./WEBAPI_CONVENTIONS.md) - API-tervezés
- [DOMAIN_MODEL.md](./DOMAIN_MODEL.md) - Entitástervezés
- [LIMITATIONS_AND_ISSUES.md](./LIMITATIONS_AND_ISSUES.md) - Ismert hibák

## Segítségkérés

1. **Nézze meg a dokumentációt** - Kezdje a fenti megfelelő útmutatóval
2. **Keressen a hibajegyek között** - Keressen hasonló problémákat
3. **Tegyen fel kérdéseket** - Nyisson vitaindító hibajegyet
4. **Hibakeresés helyben** - Használja a VS Code hibakeresőjét
5. **Ellenőrizze a naplókat** - Tekintse át a részletes hibaüzeneteket

## Gyors referencia

```bash
# Build and Test
dotnet build Behind.slnx
dotnet test tests/Behind.Tests/Behind.Tests.csproj

# Run Application
dotnet run --project src/Core/Behind.Core.WebApi/Behind.Core.WebApi.csproj

# Database
dotnet ef migrations add "MigrationName" --project src/Core/Behind.Core.Database.Sqlite --startup-project src/Core/Behind.Core.Database.Migrator --context CoreContextSqlite --output-dir Migrations
dotnet run --project src/Core/Behind.Core.Database.Migrator

# Format Code
dotnet format Behind.slnx

# View API Docs
# Open https://localhost:5001/scalar/v1
```

---

**Következő lépések**:
1. Olvassa el a CODING_STANDARDS.md fájlt a projekt konvenciók megismeréséhez
2. Tekintse át a FUNCTIONAL_PROGRAMMING.md fájlt a monádok megértéséhez
3. Ismerje meg a kódbázis szerkezetét
4. Hozzon létre feature ágat az első hozzájárulásához
5. Nyújtson be pull requestet az irányelvek szerint

Jó kódolást! 🚀
