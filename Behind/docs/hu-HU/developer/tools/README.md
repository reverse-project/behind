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

Az alapértelmezett adatkönyvtár a `data`, a konfigurációs könyvtár pedig a `.`. A konfigurációs könyvtárba a `security.json`, `database.json`, `backend.json` és `frontend.json` fájlok kerülnek. A `--help` megmutatja a JWT-kibocsátó és -közönség, az e-mail-megerősítés, a tanúsítványjelszó bekérése, a meglévő fájlok felülírása és a meglévő konfigurációk `--skip-existing` kapcsolóval történő megtartásának lehetőségeit.

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
dotnet run --project src/Core/Behind.Core.Tools -- secret uri --value BASE32_TITOK --account admin
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
