# Settings

**Settings** shows the configuration files of the installation, one tab per file, and lets a developer change them without opening the server. It is a tool of the [Administration](./administration.md) page.

## Who can use it

| You can | You need |
|---------|----------|
| open the page and read the settings | the `Configuration.Read` grant |
| save changes, generate and rotate secrets | the `Configuration.Manage` grant |

Both grants belong to the `Developers` role. `Configuration.Manage` is given to the role when the database is created; on an installation that already existed, add it to the `Developers` role (or to a role of your own) on the [Roles](./managing-roles-and-access.md) page. Administrators never have these grants.

## Tabs

Every configuration file that exists next to the application gets a tab: `appsettings.json`, `backend.json`, `database.json`, `email.json`, `frontend.json` and `security.json`. Except for `email.json` and the `appsettings` files, which have their own forms (see below), a tab lists the settings of the file grouped by section, for example `Security › Jwt`. Text settings are text boxes, on and off settings are switches and numbers are number boxes.

The **DefaultLocale** setting of `frontend.json` (`Frontend › Site`) is a drop-down of the available languages by their official names, for example **English (International)** and **Magyar**. The language applies to every visitor; a locale without a file is refused.

## Email settings

The `email.json` tab is a form instead of a list of values:

1. Choose the **Email provider**. **Logger** only writes the emails to the application log (good for development). **Smtp** sends them through a mail server.
2. For **Smtp** fill in the **Server**, the **Port** (1-65535), whether to **Use SSL** and, optionally, the **Sender address**. Without a sender address the user name of the basic authentication is used.
3. Choose the **Authentication**: **No** or **Basic** with a user name and a password. The password is masked; leave it empty to keep the stored one.
4. Optionally write your own **email templates** (`ConfirmationEmail`, `PasswordResetCodeEmail`, `PasswordResetLinkEmail`, `MagicLinkEmail`). An empty template uses the built-in one, which is shown as a hint.
5. Choose **Save**. The values are checked and refused when they are not valid. The new provider applies immediately.

## Log levels

The `appsettings.json` tab is a form for the log levels instead of a list of values:

1. Choose the **Default level**: the lowest level that is written to the log (Verbose, Debug, Information, Warning, Error or Fatal). **Not set** leaves the built-in default (Information).
2. Under **Levels per source** add a source, a namespace such as `Microsoft.AspNetCore` or `Behind`, and choose its level. A source with its own level ignores the default level. Every source needs a different name; **Remove** deletes one.
3. Choose **Save**. Only the levels are written to the file; everything else in it stays as it was. The new levels apply immediately.

## Secrets are masked

Passwords, secret keys, signing keys, tokens and connection strings are never shown. Only the first and last characters are visible, for example `to...........UL`. To replace such a value type the new one in its box; leaving the box empty keeps the current value.

## Changing settings

1. Change the values you want on a tab.
2. Choose **Save**. Only the changed values are written to the file; everything else in the file stays as it was.
3. **Discard** puts the original values back in the boxes.

Every change applies immediately, the application does not need to be restarted. The same is true when you edit a configuration file by hand: the application watches `database.json`, `backend.json`, `email.json`, `security.json`, `frontend.json` and `appsettings.json` and reloads them when they change. A file that is not valid JSON, or values that cannot be used, are refused and the previous values stay active (the problem is written to the log). In a container with a mounted volume the file watcher needs `DOTNET_USE_POLLING_FILE_WATCHER=true`; a change is then picked up within a few seconds.

The only values that are still read once at startup are the data protection certificate and keys (`Security:DataProtection`), the OpenTelemetry settings and the database provider.

## Generate and Rotate

Some secrets cannot be typed; they have a button instead:

| Setting | Button | What it does |
|---------|--------|--------------|
| `Security:SecretKey` (administrator elevation) | **Generate** | Creates a new authenticator secret. |
| `Security:Elevation:DeveloperSecretKey` (developer elevation) | **Generate** | Creates a new authenticator secret, also when none is set yet. |
| `Security:Jwt:SigningKey` | **Generate** | Creates a new random signing key. Everybody has to sign in again. |
| `Security:MagicLink:ActualSecret` | **Rotate** | The current magic link secret becomes the old secret, which is still accepted for the configured overlap, and a new secret is used from now on. It applies immediately. |

Generating an authenticator secret shows its authenticator address and a QR code **once**. Add it to your authenticator at once; the page never shows it again. The new secret applies immediately. Every button asks for confirmation.

## When nothing can be changed

If the installation runs with a read-only configuration (`Security:ReadOnlyConfiguration`), the page shows the masked values but refuses every change, generation and rotation. Change such an installation through its external configuration provider.

Changes are written to the files next to the application. In a container the file must be writable by the user the container runs as, so mount `frontend.json` (and any other file you want to edit) from a writable host path, like `security.json` in the example compose file. Take care with settings such as the database connection: a wrong value can stop the application from starting.
