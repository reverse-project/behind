# Behind Core Tools

Behind Core Tools is a command-line application for generating startup and data protection configuration, managing TOTP secrets, and rendering release notes. The project is in [`src/Core/Behind.Core.Tools`](../../../../src/Core/Behind.Core.Tools).

## Run from the repository

The tool targets .NET 11. From the repository root, run a command with `dotnet run`:

```bash
dotnet run --project src/Core/Behind.Core.Tools -- --help
```

The `--` separates `dotnet run` options from arguments passed to `behind-core`. If no command is provided, the tool displays help.

## Commands

### Startup configuration

Generate startup configuration files, a data protection certificate, and keys:

```bash
dotnet run --project src/Core/Behind.Core.Tools -- startup create
```

Defaults are `data` for the data directory and `.` for the configuration directory. The configuration directory receives `security.json`, `database.json`, `backend.json`, and `frontend.json`. Use `--help` to see options for the JWT issuer and audience, email confirmation, certificate password prompting, overwriting files, and preserving existing files with `--skip-existing`.

### Data protection certificate

Create a data protection certificate and its `security.json` configuration:

```bash
dotnet run --project src/Core/Behind.Core.Tools -- dataprotection create
```

The defaults are `dataprotection.pfx`, `DataProtection-Keys`, and `security.json`. Use `--help` to see options for custom paths, prompting for a certificate password, and overwriting existing files.

### TOTP secrets

Generate a TOTP secret and authenticator URI (the default secret size is 20 bytes):

```bash
dotnet run --project src/Core/Behind.Core.Tools -- secret generate --type totp
```

Print an authenticator URI for an existing TOTP secret:

```bash
dotnet run --project src/Core/Behind.Core.Tools -- secret uri --value YOUR_BASE32_SECRET --account admin
```

The `--account` option defaults to `admin`. The output includes a configuration value suitable for `security.json` and the full URI to add to an authenticator app.

### Release notes

Render Markdown release notes from the changelog:

```bash
dotnet run --project src/Core/Behind.Core.Tools -- release-notes
```

By default, the command selects the first (latest) release in the repository changelog. Select another version or changelog file with `--version` and `--changelog`:

```bash
dotnet run --project src/Core/Behind.Core.Tools -- release-notes --version 1.0.0 --changelog path/to/changelog.json
```

The generated Markdown is written to standard output.
