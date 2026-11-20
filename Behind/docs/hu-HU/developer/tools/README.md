# Behind Core Tools

A Behind Core Tools egy parancssori alkalmazás, amellyel indítási és adatvédelmi konfiguráció hozható létre, TOTP titkok kezelhetők, valamint kiadási jegyzetek generálhatók. A projekt a [`src/Core/Behind.Core.Tools`](../../../../src/Core/Behind.Core.Tools) mappában található.

## Futtatás a repositoryból

Az eszköz .NET 11-et használ. A repository gyökeréből a `dotnet run` paranccsal futtatható:

```bash
dotnet run --project src/Core/Behind.Core.Tools -- --help
```

A `--` választja el a `dotnet run` kapcsolóit a `behind-core` parancsnak átadott argumentumoktól. Ha nem ad meg parancsot, az eszköz megjeleníti a súgót.

## Parancsok

### Indítási konfiguráció

Indítási konfigurációs fájlok, adatvédelmi tanúsítvány és kulcsok létrehozása:

```bash
dotnet run --project src/Core/Behind.Core.Tools -- startup create
```

Az alapértelmezett adatkönyvtár a `data`, a konfigurációs könyvtár pedig a `.`. A konfigurációs könyvtárba a webes kiszolgáló minden konfigurációs fájlja bekerül, az összes alapértelmezett értékkel kitöltve: `appsettings.json` (naplózás és kikapcsolt OpenTelemetry), `security.json`, `database.json`, `backend.json`, `frontend.json`, `email.json` (naplózó szolgáltató), `forum.json` és `markdownviewer.json` (dokumentumok a `<data>/docs` alatt). A `--help` megmutatja a JWT-kibocsátó és -közönség, a webhely nyilvános címe (`--frontend-url`, alapértelmezés: `http://localhost:8080/`), az e-mail-megerősítés, a tanúsítványjelszó bekérése, a meglévő fájlok felülírása és a meglévő konfigurációk `--skip-existing` kapcsolóval történő megtartásának lehetőségeit.

### Adatvédelmi tanúsítvány

Adatvédelmi tanúsítvány és a hozzá tartozó `security.json` konfiguráció létrehozása:

```bash
dotnet run --project src/Core/Behind.Core.Tools -- dataprotection create
```

Az alapértelmezett fájlnevek és útvonalak: `dataprotection.pfx`, `DataProtection-Keys` és `security.json`. A `--help` megmutatja az egyéni útvonalak, a tanúsítványjelszó bekérése és a meglévő fájlok felülírása kapcsolóit.

### TOTP titkok

TOTP titok és hitelesítőalkalmazás-URI generálása (az alapértelmezett titokméret 20 bájt):

```bash
dotnet run --project src/Core/Behind.Core.Tools -- secret generate --type totp
```

Hitelesítőalkalmazás-URI kiírása meglévő TOTP titokhoz:

```bash
<!-- coderabbit-ai-generated:v1 -->

<!-- coderabbit-ai-generated-section:v1:8c7ed70ebb3b9495961fccd784b5d56b2e4614a31045352f01e9f9ce7a2ace72 -->

dotnet run --project src/Core/Behind.Core.Tools -- secret uri --account admin

<!-- /coderabbit-ai-generated:v1 -->
```

Az `--account` alapértelmezett értéke `admin`. A kimenet tartalmaz egy `security.json` konfigurációhoz használható értéket és a hitelesítőalkalmazáshoz hozzáadható teljes URI-t.

### Kiadási jegyzetek

Markdown kiadási jegyzetek generálása a változásnaplóból:

```bash
dotnet run --project src/Core/Behind.Core.Tools -- release-notes
```

Alapértelmezés szerint a parancs a repository változásnaplójának első (legújabb) kiadását választja ki. A `--version` és `--changelog` kapcsolóval másik verzió vagy változásnapló-fájl választható:

```bash
dotnet run --project src/Core/Behind.Core.Tools -- release-notes --version 1.0.0 --changelog path/to/changelog.json
```

A generált Markdown a szabványos kimenetre kerül.
