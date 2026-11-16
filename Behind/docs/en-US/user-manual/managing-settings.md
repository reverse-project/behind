# Settings

**Settings** shows the configuration files of the installation, one tab per file, and lets a developer change them without opening the server. It is a tool of the [Administration](./administration.md) page.

## Who can use it

| You can | You need |
|---------|----------|
| open the page and read the settings | the `Configuration.Read` grant |
| save changes, generate and rotate secrets | the `Configuration.Manage` grant |

Both grants belong to the `Developers` role. Administrators never have these grants.

## Tabs

The **caching.json** tab controls HybridCache. **Provider** is a dropdown containing
**Memory** (default) and **RedisCompatible** (Valkey). **Key** names a connection
under **ConnectionStrings**; the generated name is **CachingConnection**.
Edit its masked password field to replace the endpoint/credentials; leaving the
password input blank preserves the stored secret. Clear Key to disable distributed
caching. A missing key or empty/missing connection uses memory only. Save and
restart the application for changes to take effect. Existing installations can
generate this file with `startup create --skip-existing`.

Every configuration file that exists next to the application gets a tab: `appsettings.json`, `backend.json`, `database.json`, `email.json`, `frontend.json` and `security.json`, plus the file of every module that has one (for example `markdownviewer.json` of the [Documentation](./managing-markdown-viewer.md) module). Except for `email.json` and the `appsettings` files, which have their own forms (see below), a tab lists the settings of the file grouped by section, for example `Security › Jwt`. Text settings are text boxes, on and off settings are switches and numbers are number boxes.

The limits of the site image carousels are the numbers of `frontend.json` (`Frontend › Carousels`): **MaxSlides** (images per carousel), **MinIntervalSeconds** and **MaxIntervalSeconds** (the allowed seconds per image), **IntervalSeconds** (what a new carousel starts with), **MaxCaptionLength** and **MaxImageBytes**. See [Managing sites](./managing-sites.md).

The **DefaultLocale** setting of `frontend.json` (`Frontend › Site`) is a drop-down of the available languages by their official names, for example **English (International)** and **Magyar**. The language applies to every visitor; a locale without a file is refused.

## Age restrictions

In **backend.json > Backend > ContentRestrictions**, **Enabled** turns global age filtering on or off. It is **off by default** and reloads without restarting. While disabled, stored age rules do not limit access. When enabled, readers need a private birthday meeting each item's minimum age; unknown birthdays cannot access restricted items or their related content and files.

Set each item's minimum age in its editor. This requires `ContentRestriction.Write` and the item's existing editing permission, including ownership checks where applicable. Choose **Unrestricted** to remove its age requirement.

The **Content ages** administration page requires `ContentAge.Manage` and manages the selectable ages. Existing ages and 12, 16 and 18 are initially available. Removing an age only removes it from the available choices; existing items retain and enforce their numeric minimum age. An editor can retain that age or explicitly change it. Listing, restoring and permanently deleting removed ages also requires `RawDatabase.Manage`.

Give each age a display name, such as **Adult**, when adding it. The name can be changed later without changing the age or affecting existing restrictions. Existing ages initially use their number as their name.

## Account sign-in methods

In **backend.json > Backend > Accounts**, **Password**, **MagicLink**, **Oidc**, **TwoFactor**, **Passkey** and **EmailTwoFactor** control sign-in and second-factor methods. All default to **true**, and changes reload without restarting.

- **Password** enables or disables password sign-in.
- **MagicLink** enables or disables email sign-in links. It does **not** disable **LostPassword** recovery links, which permit a password reset only and do not grant a full signed-in session.
- **Oidc** enables or disables all OpenID Connect providers. This global switch lives in `backend.json` under `Backend:Accounts`, defaults to true and reloads without restarting. Turning it off blocks new OIDC sign-ins and pending callbacks, ticket redemption and second-factor continuations; existing sessions remain active.
- **TwoFactor** enables or disables two-factor authentication. Turning it off does not bypass protection: users who have enrolled in two-factor authentication cannot sign in while it is disabled, and users cannot sign in when two-factor authentication is required but disabled.
- **Passkey** enables or disables passkeys both for sign-in and as a second factor. A passkey used to sign in cannot also satisfy that sign-in's second-factor challenge. When disabled, recovery codes remain usable.
- **EmailTwoFactor** enables or disables email codes as a second factor. Users choose a confirmed account address after primary authentication. Email codes are not offered after an email sign-in or password recovery link, because both use the same channel.

After a password recovery, all existing sessions and outstanding sign-in and recovery links are revoked.

If a method is disabled, previously issued links and active challenges for that method are rejected.

## OpenID Connect providers

The **security.json** tab includes an **OpenID Connect providers** editor alongside the existing security fields and Generate/Rotate controls. Use **Add provider** to add one, edit its fields, scopes and role mappings, or remove it, then use the provider editor's **Save** button. Leave a client secret empty to keep it, enter a new one to replace it, or explicitly clear it. Secrets are never returned to the browser. Existing provider IDs should remain unchanged. The editor follows Configuration.Read/Manage permissions and read-only configuration.

The editor writes `Security:Oidc` in `security.json`; see the complete [OIDC sign-in specification](../specs/oidc-sign-in-1.1.0.md) for a configuration example and endpoint contract. `Providers` defaults to an empty array, so existing installations are unchanged. Each provider has a stable `Id`, `DisplayName`, HTTPS `Authority`, `ClientId`, optional secret, scopes and independent account/role settings. Register `https://<backend>/sign/in/oidc/callback/<Id>` as its exact redirect URI. The backend and `Frontend:Url` must use HTTPS; same-origin hosting is recommended.

`LinkByVerifiedEmail` is opt-in. When enabled, first sign-in can link an existing account only when the provider asserts `email_verified=true` and that exact local address is already confirmed. Subsequent sign-ins use the persisted provider/issuer/subject binding, not email. `AutoCreateUsers` defaults to true but also requires global sign-up to be enabled; moderated registration remains pending approval.

`RoleMappings` maps external roles to arrays of existing local role names. With `AutoCreateRoles=true`, unmapped roles create ordinary roles without grants or developer access, using `AutoCreateRolePrefix` (default `oidc:<Id>:`). Automatic reuse of privileged roles is refused. Role synchronization removes only this provider's obsolete assignments, preserving manual roles and roles supplied by another provider. Changes take effect at the next successful OIDC account resolution; provider removal alone does not revoke existing roles or sessions.

Provider configuration is read at startup and requires a restart after editing. The global on/off switch is separate: **backend.json > Backend > Accounts > Oidc** reloads without restarting. `TicketLifetime` (default two minutes), `AuthenticationTimeout` (ten minutes) and `MaxPendingTickets` (1000) bound transient browser handoffs. A restart invalidates pending handoff tickets; start sign-in again.

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

Except for the startup-only settings listed below, changes apply immediately without restarting. The same is true when you edit a configuration file by hand: the application watches `database.json`, `backend.json`, `email.json`, `security.json`, `frontend.json` and `appsettings.json` and reloads them when they change. A file that is not valid JSON, or values that cannot be used, are refused and the previous values stay active (the problem is written to the log). In a container with a mounted volume the file watcher needs `DOTNET_USE_POLLING_FILE_WATCHER=true`; a change is then picked up within a few seconds.

The values read once at startup are OpenID Connect (`Security:Oidc`), the data protection certificate and keys (`Security:DataProtection`), the OpenTelemetry settings and the database provider.

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
