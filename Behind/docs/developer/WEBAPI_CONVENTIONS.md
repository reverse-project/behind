# WebApi Conventions

This document outlines the conventions, patterns, and best practices for building REST APIs in the Behind project.

## Overview

The Behind WebApi layer is built on:
- **ASP.NET Core 10.0.8**
- **OpenAPI 10.0.10** with Scalar UI
- **JWT Bearer Authentication**
- **Structured Error Handling**
- **Railway-Oriented Programming** (monadic returns)

## Startup Configuration

### WebApplicationBuilder Extensions

The `Behind.Common.WebApi.WebApplicationBuilderExtensions` provides fluent configuration:

```csharp
extension(WebApplicationBuilder builder)
{
    public WebApplicationBuilder AddOpenApi()
    {
        builder.Services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
        });
        return builder;
    }

    public WebApplicationBuilder AddOpenTelemetry()
    {
        // Configure distributed tracing, metrics, and logging
        // Via configuration file (appsettings.json)
    }
}
```

### Live Configuration

Every JSON configuration file is loaded with `IHostApplicationBuilder.AddReloadableJsonFile(path, optional)` (`Behind.Common.Configuration`), a JSON source that watches the file. When a file becomes invalid JSON after it loaded once, the last good values are kept and the failure is reported through `ConfigurationReloadFailures` (logged by `ConfigurationReloadFailureLogger`).

Settings that must follow the files are registered with `AddReloadableOptions<T>(...)`: one `ReloadableOption<T>` answers `IOptions<T>`, `IOptionsSnapshot<T>` and `IOptionsMonitor<T>` with the current value, notifies `OnChange` only when the bound value changed and keeps the previous value when the new one cannot be bound. `SecurityConfiguration`, `EmailConfiguration`, `DatabaseConfiguration`, `BackendConfiguration` and `FrontendOptions` use it, so injected `IOptions<T>` is live. Consumers must read `.Value` per use and never keep it in a field or capture it at startup. Related live pieces:

- `AddOptionsCacheInvalidation<TSource, TTarget>()` clears the cached named options of `TTarget` (the `JwtBearerOptions` of the Bearer scheme) when `TSource` changes.
- `UseReloadable<T>(configure)` (`Behind.Common.WebApi`) builds a middleware branch from the current options and rebuilds it when they change (`UseStaticFrontend`).
- `AddClientPolicy(name, Func<HttpContext, (int, TimeSpan)>)` reads the rate limit per request (the limits are part of the partition key); the magic link policies read `IMagicLinkSettingsStore`.
- Two-factor services (`AddTwoFactor()`): `TwoFactorTickets` and `PasskeyCeremonies` are singletons, the others scoped; second step endpoints share the `two-factor` client rate limit and answer `{ code }` failures (`TwoFactorFailure`). The `Fido2` package declares a type named `None`, so its types are imported through the global aliases in `TwoFactor/Fido2Aliases.cs` instead of `using Fido2NetLib;`.
- Email senders are transient and created from the current `EmailConfiguration`; `MagicLinkMailService` creates its sender per mail.
- The database connection string and the VFS content store root are resolved per scope / per resolution.

Only the data protection certificate and keys, the OpenTelemetry settings and the database provider are read once at startup.

### Static Frontend

`UseCore()` serves the JavaScript frontend embedded from `Behind.Core.WebApi/Frontend` (RequireJS, jQuery, Knockout, Sammy; controllers/models/viewmodels/views layout). `index.html` is the default file. JS, CSS and HTML are minified in memory (once per file version) and responses are Brotli/Gzip compressed by default, with ETag revalidation.

Configure through the `Frontend:StaticFiles` section (for example in `frontend.json`):

```json
{ "Frontend": { "StaticFiles": { "Minify": true, "Compress": true, "DefaultFile": "index.html", "CacheControl": "no-cache" } } }
```

Files named `*.min.*` are served as-is. Third-party libraries live in `Frontend/libraries`.

Images (png, jpg, gif, webp, avif, ico, svg) are served unchanged with the correct content type and the same cache/security headers; only SVG is compressed. If a `wwwroot` directory exists in the content root, its files are served in addition to the embedded frontend and take precedence over embedded files with the same path.

### Localization

The frontend texts come from JSON files in `Frontend/locales/<code>.json` (`en-US` and `hu-HU`): `{ "name": "Magyar", "messages": { "key": "text {0}" } }`. `name` is the official name of the locale shown by the Settings page, `{0}` style placeholders are filled by position. Files in `wwwroot/locales` override the embedded ones and add new locales (the `wwwroot` provider takes precedence, see Static Frontend); a code must look like `hu-HU` and the file must be valid JSON, other files are ignored.

- `LocaleCatalog` (`Behind.Common.WebApi.Infrastructure.Localization`, registered by `AddFrontend` over the embedded provider plus `wwwroot`) lists the available `LocaleInfo(Code, Name)` sorted by name and checks `ContainsAsync`.
- The default locale is `Frontend:Site:DefaultLocale` of `frontend.json`; `GET /about` returns it as `defaultLocale` (`en-US`, `LocaleCatalog.Fallback`, when empty).
- `GET /settings` gives the field `Frontend:Site:DefaultLocale` of `frontend.json` an `options` list (`SettingsOption(Value, Label)`, built by `SettingsLocaleExtensions.OptionsFor`); the Settings page shows a field with options as a drop-down, and `PUT /settings/frontend.json` answers `400` for a locale without a file (`HasKnownLocale`).
- `models/i18n.js` loads `en-US` as the fallback and then the requested locale before `app.js` binds the shell, and exposes `i18n.t(key, args)` plus the Knockout bindings `t: 'key'` (or `t: { key: 'key', args: [...] }`) for the text and `tAttr: { attribute: 'key' }` for attributes. A key missing from the locales keeps the English text written in the HTML. Every key of `en-US` must exist in the other locales (tested).
- Rules for frontend code: no hardcoded user-visible text. Views use `t` / `tAttr` (no HTML in messages, mixed markup is split or passed as `{0}` arguments, the English text stays in the HTML as fallback), scripts call `i18n.t("key", [args])` at use time (never at module load) and write each key as a plain string literal. Message tables hold keys; `models/results.js` translates them (`results.sessionExpired` is a key). Dates and numbers use `i18n.dateTime`, `i18n.date` and `i18n.locale()`. Built-in menu item names are mapped to `menu.item.*` keys in `models/menus.js`.
- `LocalizationCompletenessTests` enforces it: every key used in a `.js` or `.html` file exists in `en-US`, every `en-US` key is used, `hu-HU` has the same keys and placeholders, and views contain no text outside a `t`/`text`/`html`/`value` binding (`<code>`, `<option>`, `<textarea>` and `<script>` are exempt). A new text therefore needs its key in both `locales/en-US.json` and `locales/hu-HU.json`.
- A user's own locale is the per-user setting `locale` (see below); `GET /locales` (anonymous) lists the available `LocaleInfo(Code, Name)`. The language is the account's locale, else the one remembered in the browser (`localStorage` key `behind.locale`), else the site default; a blank value means "use the default".

### User Settings

Per-user key/value pairs live in the `UserSettings` table (`UserId`, `Key`, `Value`; composite key `UserId` + `Key`, cascade delete with the user), mapped as a shared-type join table like `UserGrants` (`UserConfiguration.ApplyUserSettings`, `UserSettingStore`) and read and written through `IUserSettingQueries` and `IUserSettingCommands` (an empty value removes the pair). It is not an audited entity, so it has no audit label.

- `GET /profile/settings` returns the settings of the signed in user as `{ key: value }`; `PUT /profile/settings/{key}` takes `{ value }` (`204`; `401` without a valid session; `404` for a key that is not in `UserSettingKeys.Known`; `400` when `UserSettingExtensions.AcceptsAsync` refuses the value, for `locale` a locale without a file). Both use `IgnoreETag`.
- A new setting is one constant plus its entry in `UserSettingKeys.Known` and, when its value needs checking, a case in `AcceptsAsync`. The frontend reads them through `models/usersettings.js` (`load`, `clear`, `save`, `locale`), which `app.js` reloads when the session signs in, signs out or impersonates.

### Sites Settings

The Settings page (`#/settings`, `SettingsEndpoints`) manages the configuration files in the content root. `ConfigurationFileCatalog` (registered as a singleton by `AddSecurity`) lists the only files it may touch: `appsettings.json`, `backend.json`, `database.json`, `email.json`, `frontend.json` and `security.json`; any other name answers `404`.

- `GET /settings` (`Configuration.Read`) returns `{ readOnly, files: [{ name, fields: [{ path, type, value, sensitive, editable, action }] }] }`. Every leaf of a file is a field whose `path` is the colon separated configuration path (array items by index). `value` of a sensitive field is `MaskSensitiveValue()` of the real value; a path is sensitive when it is under `ConnectionStrings` or its last segment contains `password`, `secret`, `signingkey`, `apikey`, `token` or `credential` (`SensitiveConfigurationPathExtensions.IsSensitiveConfigurationPath`).
- `PUT /settings/{file}` (`Configuration.Manage`) takes `{ values: { path: value } }`. Only existing leaves of the same JSON type can be changed (`SettingsChangeExtensions.PlanChanges`); a blank value of a sensitive field and a field that has a Generate or Rotate action are refused, and one invalid change refuses the whole request (`400`). The values are written with `JsonConfigurationFileExtensions.SetJsonNodesAsync` (atomic replace, types kept).
- `POST /settings/{file}/generate` and `POST /settings/{file}/rotate` (`Configuration.Manage`) take `{ path }`. The actions are listed in `SettingsAction.All`: Generate for `Security:SecretKey` and `Security:Elevation:DeveloperSecretKey` (a new `Totp`, the response carries its `otpUri` once, and the key is created when missing), Generate for `Security:Jwt:SigningKey` (`JwtSettings.GenerateSigningKey`, never returned), Rotate for `Security:MagicLink:ActualSecret` (`MagicLinkSecretRotator.RotateNowAsync`, applied live and persisted). A new action is one entry of that list. The elevation keys and the JWT key are read live, so they apply immediately.
- `email.json` is a typed file: `ConfigurationFileCatalog` marks it with the `email` editor, so `GET /settings` lists it with `editor: "email"` and no fields and the generic `PUT /settings/{file}` answers `404` for it. `GET /settings/email` (`Configuration.Read`) reads the file into the `EmailConfiguration` record with the option aware binder and answers `EmailSettingsResponse` (provider, the providers, the authentication types, the built-in templates and the SMTP values with a masked password). `PUT /settings/email` (`Configuration.Manage`) takes `EmailSettingsRequest`; `EmailSettingsExtensions.Apply` validates it and creates the `LoggerEmailConfiguration` or `SmtpEmailConfiguration` record (a blank password keeps the stored one; an invalid request is `400`), which is serialized with the JSON converters of the records (`EmailConfigurationJsonConverter`, `EmailAuthenticationJsonConverter`, `TemplatesJsonConverter`, `Option<T>` nulls removed by `JsonNodeExtensions.WithoutNulls`) into the `Email` section of the file; other sections of the file are kept. A new email provider is a new `EmailConfiguration` record, a case in the converters and in `EmailSettingsExtensions`.
- `appsettings.json` is typed with the `logging` editor: `GET /settings/{file}/logging` (`Configuration.Read`) reads `Serilog:MinimumLevel` into the `MinimumLevelConfiguration` record (`Option<LogEventLevel>` default plus the `Override` dictionary, converted by `MinimumLevelJsonConverter`) and answers `LoggingSettingsResponse` (default, the level names, the overrides sorted by source). `PUT /settings/{file}/logging` (`Configuration.Manage`) takes `LoggingSettingsRequest`; `LoggingSettingsExtensions.ToConfiguration` validates it (known level names, non blank and unique sources, `400` otherwise) and only the `Serilog:MinimumLevel` node is replaced (removed when empty), the rest of the file is kept. Serilog follows the configuration changes, so a change applies immediately. Other files answer `404`.
- `OptionConfigurationBinder` binds an abstract type by its discriminator: the single string parameter of its protected constructor (`Provider` of `EmailConfiguration`, `Type` of `Authentication`) is converted by the type's `TypeConverter` to the concrete type, which is then bound normally. A type whose `TypeConverter` converts from `IReadOnlyDictionary<string, string?>` (`Templates`) is bound from the child values of its section.
- While `Security:ReadOnlyConfiguration` is true every change answers `403` with `ConfigurationForbiddenResponse.ConfigurationReadOnly`.
- `Configuration.Manage` is part of the built-in `Developers` role (`BuiltInAccess`); existing databases keep their role grants, so it has to be granted on the Roles page.

### Sites Settings

The `Frontend:Sites` section (`SitesConfiguration`, for example in `frontend.json`) configures the sites feature:

```json
{ "Frontend": { "Sites": { "IndexSlug": "home", "MaxImageBytes": 5242880 } } }
```

- `IndexSlug`: when a site with this slug (normalized like any slug) is live, `GET /sites/index` serves it and the frontend shows it at `#/` instead of the built-in welcome page. Empty or missing means no custom index.
- `MaxImageBytes`: the largest accepted site image upload (default 5 MB, zero or negative falls back to the default). Larger uploads answer `413` with `{ "maxBytes": ... }`. The Kestrel request body limit (30 MB by default) still applies on top of it.

`GET /sites/settings` (`Site.Read`) returns both values and the largest number of sections of a site (`maxSections`) to the editor.

### Site Sections

`POST /sites` and `POST /sites/{id}/versions` (`Site.Write`) accept an optional `sections` array next to `title`, `slug`, `content`, `publish` and `hideTitle`: `[{ theme: "Normal" | "Accented", title, content, imageId, imageAlt, imagePosition: "Left" | "Right" }]`. The order of the array is the order on the page and every save stores the complete list for the new version (an omitted or empty list means no sections).

- `title`, `content` (sanitized like the site content) and `imageAlt` are trimmed and limited (200, 100000 and 300 characters). A section needs a title, a text or an `imageId`; `imageAlt` is dropped without an image.
- More than 30 sections, an unknown theme or image position, an empty section, an empty `imageId` or too long texts answer `400`, and so does an `imageId` that is not an existing site image (`SiteSaveResult.UnknownImage`).
- `imageId` is the `id` answered by `POST /sites/images`; the section responses carry `imageUrl` (`/sites/images/<id>`, `null` without a picture).
- `GET /sites/slug/{slug}`, `GET /sites/index` and `GET /sites/{id}/versions/{version}` answer `sections: [{ id, theme, title, content, imageId, imageUrl, imageAlt, imagePosition }]`.
- The web client renders every section as `<section class="behind-section behind-section-{theme}">` (plus `behind-section-with-image behind-section-image-{left|right}` with a picture): the picture and the text share a two column grid above 768 px and stack below it. Accented sections use `--behind-section-accent-bg` (default `--bs-primary`) and `--behind-section-accent-color` (default `--bs-white`), so a site theme can restyle them.

### Backend Settings

`backend.json` (required, section `Backend`, `BackendConfiguration`) holds the settings that are not part of the frontend:

```json
{ "Backend": { "DataDirectory": "/app/data", "Downloads": { "MaxFileBytes": 104857600 } } }
```

- `DataDirectory`: the directory of the data of the server. The content of every uploaded file (site and gallery images, downloads) is stored below `<DataDirectory>/vfs`, the database only keeps the metadata (see "Virtual file system" in the database architecture). The application and the migrator fail at startup when it is missing, and both must use the same directory.

- `Downloads:MaxFileBytes` (`DownloadsConfiguration`, section `Backend:Downloads`): the largest accepted file of a download upload (default 100 MB, zero or negative or missing falls back to the default). A larger file is reported per file as `too-large`. The web client reads the limit from `GET /downloads/folders` and refuses larger files before uploading them. The request limits of the upload endpoint are derived from this value for every request by `UseDownloadUploadLimit` (the endpoint is marked with `DownloadUploadLimitMetadata`; the limit times `DownloadEndpoints.MaxUploadFiles` plus 1 MB for the request, the limit plus 1 MB for one file), so a change applies without a restart; a request above the request limit is answered with `413`.

- `Accounts` (`AccountsConfiguration`, section `Backend:Accounts`): `SignIn` and `SignUp` (both default `true`) show or hide the **Sign in** and **Sign up** buttons of the header of the web client, for example `{ "Backend": { "Accounts": { "SignUp": false } } }`. It is only a user interface switch: the sign in and sign up endpoints and pages keep working. `GET /accounts/settings` (public) returns `{ "signIn": ..., "signUp": ... }` and the client reads it at startup (`models/accounts.js`).
- `Comments` (`CommentsConfiguration`, section `Backend:Comments`): switches the commenting feature on or off per entity, one boolean per commentable entity (`News`, default `true`), for example `{ "Backend": { "Comments": { "News": false } } }`. While an entity's comments are off, every comment endpoint of it (`GET`/`POST /news/{id}/comments`, `POST /news/comments/{id}` and `.../delete`) answers `404` (`CommentsEnabledExtensions.RequireCommentsOf`, checked after the authorization), existing comments are kept and come back when the setting is turned on again. `GET /comments/settings` (public) returns `{ "news": true|false }`; the web client reads it and hides the comment count of the news list and of the news manager and the comments section of the news page. A new commentable entity adds its own property and `IsEnabled` case and uses `RequireCommentsOf` on its comment routes.

### Carousel Endpoints

A site has at most one carousel, and using it is optional per site: a site without a carousel is shown as before (see "Site carousels" in the database architecture). The limits are configuration, not constants, see "Carousels Settings".

| Endpoint | Access |
|----------|--------|
| `GET /carousels/settings` | public, the effective limits `{ maxSlides, minIntervalSeconds, maxIntervalSeconds, defaultIntervalSeconds, maxCaptionLength, maxLinkUrlLength, maxImageBytes }` |
| `GET /carousels/sites/{siteId}` | public, `404` unless the site is live and has a carousel |
| `GET /carousels/images/{id}` | public, only images of the `Carousels` VFS type |
| `POST /carousels/images` | `Site.Write` (multipart `file`, `413` over `MaxImageBytes`, `415` for other than PNG, JPEG, GIF, WebP) |
| `GET /sites/{siteId}/carousel` | `Site.Read` (`?deleted=true` needs `RawDatabase.Manage`) |
| `POST /sites/{siteId}/carousel` | `Site.Write`, body `{ intervalSeconds, slides: [{ imageId, caption, linkUrl }] }` (1 to `MaxSlides` slides, an interval inside the configured range, a caption up to `MaxCaptionLength`, a link that is empty, `#/...`, `/...` or an `http`, `https` or `mailto` URL), answers `{ siteId, version }`, `404` for an unknown site, `400` for an invalid draft or an unknown image, `409` on a version conflict |
| `POST /sites/{siteId}/carousel/delete` | `Site.Delete` (soft delete) |
| `POST /sites/{siteId}/carousel/restore` | `RawDatabase.Manage` |
| `POST /sites/{siteId}/carousel/hard/delete` | `RawDatabase.Manage` |

The public site responses (`GET /sites/slug/{slug}`, `GET /sites/index`) include the `id` of the site, the web client uses it to read the carousel. Stored links that are not safe are answered as empty links.

### Carousels Settings

The `Frontend:Carousels` section (`CarouselsConfiguration`, in `frontend.json`) holds the limits of the site carousels. Like every field of the configuration files it is edited on the Settings page and picked up while the application runs (the endpoints read `IOptionsSnapshot<CarouselsConfiguration>`), and the startup create command of the behind-core tool writes the defaults:

```json
{ "Frontend": { "Carousels": { "MaxSlides": 20, "MinIntervalSeconds": 2, "MaxIntervalSeconds": 60, "IntervalSeconds": 5, "MaxCaptionLength": 300, "MaxImageBytes": 5242880 } } }
```

- `MaxSlides`: the most images of one carousel (default 20, zero or negative falls back to the default).
- `MinIntervalSeconds` and `MaxIntervalSeconds`: the allowed range of the seconds an image is shown (default 2 to 60). A minimum below 1 or a maximum below the minimum falls back to the default range.
- `IntervalSeconds`: the interval a new carousel starts with (default 5), kept inside the range.
- `MaxCaptionLength`: the longest caption (default 300, never more than `CarouselsConfiguration.CaptionCeiling`, 1000, the size of the column).
- `MaxImageBytes`: the largest accepted carousel image upload (default 5 MB, zero or negative falls back to the default); larger uploads answer `413` with `{ "maxBytes": ... }`.

The values of existing carousels are kept when a limit changes, only new saves are checked against the current limits. `GET /carousels/settings` returns the effective values and the web client reads them before showing the carousel section of the site editor.

### Galleries Settings

The `Frontend:Galleries` section (`GalleriesConfiguration`) has `MaxImageBytes`, the largest accepted gallery image or thumbnail upload (default 10 MB, zero or negative falls back to the default). Larger files are reported per file as `too-large` in a batch upload, and answer `413` for the thumbnail upload. The gallery upload endpoints accept request bodies up to `GalleryImageEndpoints.MaxRequestBytes` (128 MB), so the web client sends five files per request.

### News Endpoints

| Endpoint | Access |
|----------|--------|
| `GET /news?page&pageSize&category&letter` | public, the live version of the news that is running now, and the active categories; `letter` keeps the news whose title starts with it (`A`-`Z`, `#`, otherwise `400`) and `letters` in the response are the first letters of the news of the selected category |
| `GET /news/{id}` | public, the live version, only while the news is running |
| `GET /news/categories` | public, active categories |
| `GET /news/{id}/comments?page&pageSize` | public, `canDelete` is set for the author and for `News.Delete` holders, `canEdit` for the author and for `News.Manage` holders, `updatedAt` comes from the audit log |
| `POST /news/{id}/comments` | any signed in user |
| `POST /news/comments/{id}` | edit: the comment author or `News.Manage`, body `{ "content": ... }`, inserts a new comment version, `204` |
| `POST /news/comments/{id}/delete` | the comment author or `News.Delete` |
| `GET /news/manage`, `GET /news/categories/manage` | `News.Read`, with `search`, `letter`, `deleted` (`RawDatabase.Manage` for `deleted`) |
| `GET /news/{id}/versions`, `GET /news/{id}/versions/{version}` | `News.Read`, the version list and one version in full |
| `POST /news`, `POST /news/{id}/versions` | `News.Write`, create / save a new version, `publish: true` also makes it live |
| `POST /news/{id}/versions/{version}/activate` | `News.Write`, make a version live |
| `POST /news/categories`, `POST /news/categories/{id}`, `POST /news/images` | `News.Write`, saving a category inserts a new version; an image upload answers `{ id, url }` with the URL `/news/images/{id}` |
| `GET /news/images/{id}` | public, an image of the news or of a news category with `immutable` caching and `nosniff` |
| `POST /news/{id}/delete`, `POST /news/categories/{id}/delete` | `News.Delete` |
| `POST .../restore`, `POST .../hard/delete` | `RawDatabase.Manage` |

Ids in the URLs are the key of the entity, responses report the `version` that was saved or shown. The managed lists share `WithManagedList` (`ManagedListExtensions`), which validates the letter filter and the deleted switch for `Sites` and `News`. Saving answers `409` with `{ "reason": ... }` (`unknown-category`, `invalid-schedule`, `version-conflict`, `name-taken`, `unknown-image`).

### Articles Endpoints

Articles follow the news endpoints under `/articles` (`ArticleRoutesExtensions.MapArticleEndpoints`), without comments, schedule and highlight, and with their own categories.

| Endpoint | Access |
|----------|--------|
| `GET /articles?page&pageSize&category&search` | public, the live version of the published articles and the active article categories; `search` matches the title or the content of the live version, ignoring case |
| `GET /articles/{id}` | public, the live version |
| `GET /articles/categories` | public, active article categories |
| `GET /articles/manage`, `GET /articles/categories/manage` | `Article.Read`, with `search` (title or content for articles), `letter`, `deleted` (`RawDatabase.Manage` for `deleted`); the article status is `draft` or `published` |
| `GET /articles/{id}/versions`, `GET /articles/{id}/versions/{version}` | `Article.Read`, the version list and one version in full |
| `POST /articles`, `POST /articles/{id}/versions` | `Article.Write`, create / save a new version, `publish: true` also makes it live |
| `POST /articles/{id}/versions/{version}/activate` | `Article.Write`, make a version live |
| `POST /articles/categories`, `POST /articles/categories/{id}`, `POST /articles/images` | `Article.Write`, saving a category inserts a new version; an image upload answers `{ id, url }` with the URL `/articles/images/{id}` |
| `GET /articles/images/{id}` | public, an image of an article or of an article category with `immutable` caching and `nosniff` |
| `POST /articles/{id}/delete`, `POST /articles/categories/{id}/delete` | `Article.Delete` |
| `POST .../restore`, `POST .../hard/delete` | `RawDatabase.Manage` |

The responses reuse `NewsCategoryItem`, `NewsSavedResponse` and `NewsConflictResponse` (reasons `unknown-category`, `version-conflict`, `name-taken`, `unknown-image`).

### Galleries Endpoints

Galleries are versioned like the articles and live under `/galleries` (`GalleryRoutesExtensions.MapGalleryEndpoints`). A gallery version owns its items: saving a version inserts the gallery row and one `GalleryItem` row per image with the same version number, so older versions keep their own images, order and descriptions.

| Endpoint | Access |
|----------|--------|
| `GET /galleries?page&pageSize&search` | public, the live version of the published galleries; `search` matches the title or the description |
| `GET /galleries/{id}` | public, the live version with its items (`imageUrl`, `thumbnailUrl`, `description`) |
| `GET /galleries/manage` | `Gallery.Read`, with `search`, `letter`, `deleted` (`RawDatabase.Manage` for `deleted`); the status is `draft` or `published` |
| `GET /galleries/{id}/versions`, `GET /galleries/{id}/versions/{version}` | `Gallery.Read`, the version list and one version in full |
| `POST /galleries`, `POST /galleries/{id}/versions` | `Gallery.Write`, create / save a new version; `publish: true` also makes it live; item descriptions are optional |
| `POST /galleries/{id}/versions/{version}/activate` | `Gallery.Write`, make a version live |
| `GET /galleries/images/{id}` | public, a file of the `Galleries` VFS type with `immutable` caching and `nosniff` |
| `POST /galleries/images` | `Gallery.Write`, multipart `files` (at most `GalleryImageEndpoints.MaxUploadFiles`, request limit `MaxRequestBytes`), stores every original and its server generated WebP thumbnail (`ThumbnailSide`) as VFS files and returns one entry per file, with an `error` (`unsupported`, `unreadable`, `too-large`) for the files that were skipped |
| `POST /galleries/thumbnail` | `Gallery.Write`, one `file`, stored scaled to `CoverSide` as a VFS file, answers `{ id, url }`, `413` with `{ maxBytes }` above the limit and `415` for other files |
| `POST /galleries/{id}/delete` | `Gallery.Delete` |
| `POST .../restore`, `POST .../hard/delete` | `RawDatabase.Manage` |

The gallery images do not use the site image endpoints or the `Frontend:Sites` limit; they are files of the `Galleries` VFS type, the site images are files of the `Sites`, `News`, `Articles`, `NewsCategories` or `ArticleCategories` types. A draft sends `thumbnailImageId: null` for the generated gallery thumbnail: `GalleryThumbnails` then composes a collage of the first four item thumbnails (`ImageThumbnailExtensions.ToMosaic`) and stores it as a VFS file; the response tells with `thumbnailGenerated` which kind it is. The responses reuse `NewsSavedResponse` and `NewsConflictResponse` (reasons `unknown-image`, `version-conflict`).

### Downloads Endpoints

The downloads are folders (`DownloadFolder`, a `Slug` and a `DisplayName`, any depth) with files (`Download`, a `DisplayName`) under `/downloads` (`DownloadRoutesExtensions.MapDownloadEndpoints`). Browsing is public. The web client shows `#/downloads/<folder path>` where the path is the slugs of the folders from the top, for example `#/downloads/manuals/drivers`, resolved from the folder tree on the client (`#/downloads` alone is the top). The management page is separate, `#/manage/downloads/<folder path>` (`downloadmanager` controller, view and viewmodel); both pages share the tree and file loading in `models/downloadbrowser.js` and the `download-tree` component.

| Endpoint | Access |
|----------|--------|
| `GET /downloads/folders` | public, every folder that is reachable (not deleted and without a deleted ancestor) as a flat list (`id`, `parentId`, `slug`, `displayName`) together with `maxFileBytes` |
| `GET /downloads/folders/{id}/files` | public, the files of a folder (`id`, `displayName`, `fileName`, `contentType`, `length`, `uploadedBy`, `uploadedAt` from the audit log, `url`), `404` for an unknown, deleted or hidden folder |
| `GET /downloads/files/{id}` | public, the content of a download as an attachment with its original file name, `nosniff` and revalidation; `{id}` is the id of the download, not of the VFS file |
| `GET /downloads/deleted` | `RawDatabase.Manage`, the deleted folders and files |
| `POST /downloads/folders`, `POST /downloads/folders/{id}/folders` | `Download.Write`, create a top level folder / a subfolder from `displayName` and an optional `slug` (default: the slug of the display name); `409` with `slug-taken` when a sibling uses the slug |
| `POST /downloads/folders/{id}` | `Download.Write`, save the next version of the folder (display name and slug) |
| `POST /downloads/folders/{id}/files` | `Download.Write`, multipart `files` (at most `DownloadEndpoints.MaxUploadFiles`) and a repeated `displayNames` field in the same order (blank or missing: the file name); every file is reported as `{ fileName, id, error }` with the error `empty`, `too-large` or `version-conflict` |
| `POST /downloads/files/{id}` | `Download.Write`, rename a download (`displayName`) |
| `POST /downloads/folders/{id}/delete`, `POST /downloads/files/{id}/delete` | `Download.Delete`, soft delete; a deleted folder hides its subfolders and files |
| `POST .../restore`, `POST .../hard/delete` | `RawDatabase.Manage`; restoring answers `409` with `parent-deleted`, `folder-deleted` or `slug-taken`; hard deleting a folder removes its subfolders, files and their content |

The files are files of the `Downloads` VFS type stored as `vfs/Downloads/<download uuid>/<file uuid>`; they are served by the download endpoint only (never through another kind of content) and never inline, so an uploaded HTML file cannot run in the origin of the application.

### Menus Endpoints

Menus are under `/menus` (`MenuRoutesExtensions.MapMenuEndpoints`). The page builds its navigation from them: the header shows the `main` menu (the `Link` items as tabs, the `User` items in the user menu) and the `menu-nav` frontend component shows any other menu by name, for example `footer`.

| Endpoint | Access |
|----------|--------|
| `GET /menus?name=main&name=footer` | public, up to 10 menus by name in one request (`400` without a name or with too many); unknown names are left out. The items are filtered for the signed in user: an item without roles is shown to everybody, an item with roles only to the users holding one of them. Sub items are nested under their parent, `kind` is `Link` or `User`, `target` is `CurrentPage`, `NewTab` or `NewWindow`. Not cached |
| `GET /menus/manage`, `GET /menus/{id}/items/manage` | `Menu.Read`, both paged (`page`, `pageSize`) with `search`, `letter` (first letter of the title) and `deleted` (`RawDatabase.Manage` for `deleted`) and the available `letters`; the items response also lists the possible `parents` (active top level links), the roles to choose from and whether `User` items are allowed |
| `POST /menus`, `POST /menus/{id}` | `Menu.Write`, saving inserts a new version; the name is turned into a slug |
| `POST /menus/{id}/items`, `POST /menus/items/{itemId}` | `Menu.Write`, saving inserts a new version with its own roles; `400` for a blank title or an unsafe URL (only relative URLs and `http`, `https`, `mailto`, `tel`) |
| `POST /menus/{id}/delete`, `POST /menus/items/{itemId}/delete` | `Menu.Delete` |
| `POST .../restore`, `POST .../hard/delete` | `RawDatabase.Manage` |

Saving answers `409` with `{ "reason": ... }`: `name-taken`, `built-in` (renaming or deleting the main menu), `unknown-menu`, `invalid-parent`, `kind-not-allowed`, `unknown-role`, `version-conflict`.

### Route Alias Endpoints

Route aliases are under `/route-aliases` (`RouteAliasRoutesExtensions.MapRouteAliasEndpoints`). The frontend router asks for an alias before it shows a site for an unknown `#/<slug>`; when the alias exists the router runs the route of the target without changing the address.

| Endpoint | Access |
|----------|--------|
| `GET /route-aliases/resolve/{alias}` | public, answers `{ "alias", "target" }` for an active alias (the alias is trimmed and lowercased) or `404`. Not cached |
| `GET /route-aliases/manage` | `RouteAlias.Read`, paged (`page`, `pageSize`) with `search` (alias or target), `letter` (first letter of the alias) and `deleted` (`RawDatabase.Manage` for `deleted`) and the available `letters` |
| `POST /route-aliases`, `POST /route-aliases/{id}` | `RouteAlias.Write`, saving inserts a new version; the alias is turned into a slug, `400` for a blank alias or a target that does not start with `#/`, is longer than 2000 characters or holds whitespace, control characters or backslashes |
| `POST /route-aliases/{id}/delete` | `RouteAlias.Delete` |
| `POST /route-aliases/{id}/restore`, `POST /route-aliases/{id}/hard/delete` | `RawDatabase.Manage` |

Saving answers `409` with `{ "reason": ... }`: `alias-taken`, `version-conflict`.

### Raw Database Endpoints

The raw database editor is under `/raw-database` (`RawDatabaseRoutesExtensions.MapRawDatabaseEndpoints`). Every route needs `RawDatabase.Manage`. The tables come from the EF Core relational model (`DbContext.RawTables`, `Behind.Common.Database.Raw`), so every entity, join table and versioning table with a primary key is covered without any per entity code.

| Endpoint | Description |
|----------|-------------|
| `GET /raw-database/tables` | every table with `name`, `entityName` (the audit entity name), `isReadOnly`, `rows` and the `columns` (`name`, `storeType`, `kind`: `text`, `integer`, `number`, `boolean`, `binary`, `guid`, `datetime`, `isNullable`, `isKey`, `isSensitive`) |
| `GET /raw-database/tables/{table}/rows?page&pageSize&search&orderBy&descending` | a page of rows as objects of column name and value (`404` for an unknown table), binary values are Base64, `[Sensible]` columns are `***`, `search` looks at every searchable column |
| `POST /raw-database/tables/{table}/rows` | body `{ "values": { column: value } }`, inserts a row and answers it; a missing Guid key is generated |
| `POST /raw-database/tables/{table}/rows/update` | body `{ "key": { column: value }, "values": { column: value } }`, writes only the given non key columns; a `***` value of a sensitive column is ignored |
| `POST /raw-database/tables/{table}/rows/delete` | body `{ "key": { column: value } }`, deletes the row permanently, `204` |

Answers: `400` with `{ "errors": [...] }` for unknown columns, key columns in `values`, an incomplete key or values that do not fit their column; `404` for an unknown table or row; `409` with `{ "reason": "read-only" }` for a read-only table (`AuditLogEntries`) or `{ "reason": "rejected", "message": ... }` when the database refuses the change. Every write runs in one transaction with its audit entry (`Created`, `Updated` with the differences, `HardDeleted`, audit entity name and key like the EF audit).

### Startup Example

```csharp
var builder = WebApplication.CreateBuilder(args);

builder
    .AddOpenApi()
    .AddOpenTelemetry()
    .Services
        .AddDatabaseServices(builder.Configuration)
        .AddAuthenticationServices(builder.Configuration)
        .AddApiServices();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app
    .UseHttpsRedirection()
    .UseAuthentication()
    .UseAuthorization()
    .MapControllers()
    .Run();
```

## Controller Structure

### Base Pattern

```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUnitOfWork unitOfWork;
    private readonly IMapper mapper;
    private readonly ILogger<UsersController> logger;

    public UsersController(
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<UsersController> logger)
    {
        this.unitOfWork = unitOfWork;
        this.mapper = mapper;
        this.logger = logger;
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UserDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserAsync(Guid id)
    {
        var userOption = await unitOfWork.Users.TryFindAsync(id);

        return userOption.Match(
            none: () =>
            {
                logger.LogWarning("User {UserId} not found", id);
                return NotFound(new ApiErrorResponse("USER_NOT_FOUND", "User not found"));
            },
            some: user =>
            {
                var dto = mapper.Map<UserDto>(user);
                return Ok(dto);
            }
        );
    }

    [HttpPost]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(UserDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiValidationResponse))]
    public async Task<IActionResult> CreateUserAsync(CreateUserDto dto)
    {
        var validation = ValidateCreateUser(dto);

        return await validation.Match(
            invalid: errors =>
            {
                logger.LogWarning("User creation validation failed: {@Errors}", errors);
                return BadRequest(new ApiValidationResponse(errors));
            },
            valid: async registration =>
            {
                var user = new HumanUser(
                    displayName: registration.DisplayName,
                    emails: new[] { registration.Email },
                    roles: None
                );

                await unitOfWork.Users.AddAsync(user);
                await unitOfWork.CommitAsync();

                var responseDto = mapper.Map<UserDto>(user);
                return CreatedAtAction(nameof(GetUserAsync), new { id = user.Id }, responseDto);
            }
        );
    }
}
```

## Request/Response Models

### DTO Pattern

**Request DTOs** - Input validation constraints:

```csharp
public record CreateUserDto(
    [Required]
    [EmailAddress]
    string Email,

    [Required]
    [StringLength(100, MinimumLength = 1)]
    string DisplayName,

    [Required]
    [StringLength(128, MinimumLength = 8)]
    string Password
);

public record UpdateUserDto(
    [StringLength(100)]
    string? DisplayName,

    [StringLength(128, MinimumLength = 8)]
    string? NewPassword
);
```

**Response DTOs** - Serializable projections:

```csharp
public record UserDto(
    Guid Id,
    string Email,
    string DisplayName,
    IEnumerable<RoleDto> Roles,
    DateTime CreatedAt
);

public record RoleDto(
    Guid Id,
    string Name,
    IEnumerable<GrantDto> Grants
);

public record GrantDto(
    Guid Id,
    string Name,
    string Description
);
```

### Error Response Models

```csharp
public record ApiErrorResponse(
    string Code,
    string Message,
    string? Details = null
);

public record ApiValidationResponse(
    IEnumerable<ValidationError> Errors,
    string Message = "Validation failed")
{
    public ApiValidationResponse(IEnumerable<Error> errors)
        : this(errors.Select(e => new ValidationError(e.Code, e.Message))) { }
}

public record ValidationError(
    string Code,
    string Message
);

public record PaginatedResponse<T>(
    IEnumerable<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages)
{
    public PaginatedResponse(PagedResult<T> result)
        : this(result.Items, result.Page, result.PageSize, result.TotalCount, result.TotalPages) { }
}
```

## HTTP Conventions

### HTTP Methods

| Method | Purpose | Returns |
|--------|---------|---------|
| GET | Retrieve resource(s) | 200 OK or 404 Not Found |
| POST | Create new resource | 201 Created with Location header |
| PUT | Replace entire resource | 200 OK or 204 No Content |
| PATCH | Partial update | 200 OK or 204 No Content |
| DELETE | Remove resource | 204 No Content or 200 OK |

### Status Codes

| Code | Usage |
|------|-------|
| **2xx Success** | |
| 200 OK | Request succeeded |
| 201 Created | Resource created (POST) |
| 204 No Content | Success with no body |
| **4xx Client Error** | |
| 400 Bad Request | Invalid input/validation failure |
| 401 Unauthorized | Authentication required |
| 403 Forbidden | Authorization denied |
| 404 Not Found | Resource not found |
| 409 Conflict | Business logic conflict |
| **5xx Server Error** | |
| 500 Internal Server Error | Unexpected server error |

### Example Endpoints

**GET - Retrieve**
```csharp
[HttpGet("{id:guid}")]
[ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UserDto))]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> GetUserAsync(Guid id)
{
    var userOption = await unitOfWork.Users.TryFindAsync(id);
    return userOption.Match(
        () => NotFound(),
        user => Ok(mapper.Map<UserDto>(user))
    );
}
```

**POST - Create**
```csharp
[HttpPost]
[AllowAnonymous]
[ProducesResponseType(StatusCodes.Status201Created)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
public async Task<IActionResult> CreateUserAsync(CreateUserDto dto)
{
    var validation = ValidateAndCreate(dto);
    
    return await validation.Match(
        errors => BadRequest(new ApiValidationResponse(errors)),
        async user =>
        {
            await unitOfWork.Users.AddAsync(user);
            await unitOfWork.CommitAsync();
            
            var dto = mapper.Map<UserDto>(user);
            return CreatedAtAction(nameof(GetUserAsync), new { id = user.Id }, dto);
        }
    );
}
```

**PUT - Full Replace**
```csharp
[HttpPut("{id:guid}")]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
public async Task<IActionResult> UpdateUserAsync(Guid id, UpdateUserDto dto)
{
    var userOption = await unitOfWork.Users.TryFindAsync(id);
    
    return await userOption.Match(
        async () => NotFound(),
        async user =>
        {
            var updatedUser = user with
            {
                DisplayName = dto.DisplayName ?? user.DisplayName
            };
            
            unitOfWork.Users.Update(updatedUser);
            await unitOfWork.CommitAsync();
            
            return Ok(mapper.Map<UserDto>(updatedUser));
        }
    );
}
```

**DELETE - Remove**
```csharp
[HttpDelete("{id:guid}")]
[ProducesResponseType(StatusCodes.Status204NoContent)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> DeleteUserAsync(Guid id)
{
    var userOption = await unitOfWork.Users.TryFindAsync(id);
    
    return await userOption.Match(
        async () => NotFound(),
        async user =>
        {
            await unitOfWork.Users.DeleteAsync(id);
            await unitOfWork.CommitAsync();
            return NoContent();
        }
    );
}
```

**GET - List with Filtering & Pagination**
```csharp
[HttpGet]
[ProducesResponseType(StatusCodes.Status200OK)]
public async Task<IActionResult> ListUsersAsync(
    [FromQuery] string? email,
    [FromQuery] string? role,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20)
{
    // Build filter
    var filter = BuildUserFilter(email, role);
    
    // Execute paged query
    var pagedResult = await unitOfWork.Users
        .FindPagedAsync(filter, page, pageSize);
    
    var dtos = pagedResult.Items
        .Select(u => mapper.Map<UserDto>(u));
    
    return Ok(new PaginatedResponse<UserDto>(
        Items: dtos,
        Page: pagedResult.Page,
        PageSize: pagedResult.PageSize,
        TotalCount: pagedResult.TotalCount,
        TotalPages: pagedResult.TotalPages
    ));
}
```

## Authentication & Authorization

### JWT Configuration

```csharp
extension(IServiceCollection)
{
    public IServiceCollection AddAuthenticationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtSettings = configuration
            .GetSection("Authentication:Jwt")
            .Get<JwtSettings>()
            ?? throw new InvalidOperationException("JWT settings required");

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtSettings.SecretKey)
                    ),
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtSettings.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("Admin", policy =>
                policy.RequireRole("Admin"));
            
            options.AddPolicy("ModerateContent", policy =>
                policy.RequireClaim("permissions", "moderate_content"));
        });

        return services;
    }
}
```

### Endpoint Protection

```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize]  // Require authentication for entire controller
public class AdminController : ControllerBase
{
    [HttpPost("users/{id}/ban")]
    [Authorize(Policy = "Admin")]  // Require specific authorization
    public async Task<IActionResult> BanUserAsync(Guid id)
    {
        // Only admin can ban users
    }
    
    [HttpGet("settings")]
    [AllowAnonymous]  // Override class-level [Authorize]
    public IActionResult GetPublicSettings()
    {
        // Public endpoint
    }
}
```

### Role & Permission Checks

```csharp
public static class AuthorizationExtensions
{
    extension(User)
    {
        public bool HasPermission(Grant grant) =>
            Roles.Match(
                () => false,
                roles => roles.Any(r => r.Grants.Match(
                    () => false,
                    grants => grants.Contains(grant)
                ))
            );
        
        public bool HasRole(string roleName) =>
            Roles.Match(
                () => false,
                roles => roles.Any(r => r.Name == roleName)
            );
    }
}

// In controller
if (!currentUser.HasPermission(new Grant("moderate_content")))
{
    return Forbid("Insufficient permissions");
}
```

## Error Handling

### Global Exception Handler

```csharp
extension(WebApplication)
{
    public WebApplication UseGlobalExceptionHandler(this WebApplication app)
    {
        app.UseExceptionHandler(errorApp =>
        {
            errorApp.Run(async context =>
            {
                var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
                var logger = context.RequestServices.GetRequiredService<ILogger>();
                
                logger.LogError(exception, "Unhandled exception");
                
                context.Response.ContentType = "application/json";
                context.Response.StatusCode = exception switch
                {
                    ValidationException => StatusCodes.Status400BadRequest,
                    NotFoundException => StatusCodes.Status404NotFound,
                    UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
                    _ => StatusCodes.Status500InternalServerError
                };
                
                var response = new ApiErrorResponse(
                    Code: GetErrorCode(exception),
                    Message: exception?.Message ?? "Internal server error",
                    Details: app.Environment.IsDevelopment() ? exception?.StackTrace : null
                );
                
                await context.Response.WriteAsJsonAsync(response);
            });
        });
        
        return app;
    }
}
```

### Functional Error Handling

```csharp
public async Task<IActionResult> ProcessAsync(UserCommandDto dto)
{
    var result = await ValidateCommand(dto)
        .Bind(cmd => ExecuteCommand(cmd))
        .Run();  // Execute and catch any exceptions
    
    return result.Match(
        exception => HandleException(exception),
        user => Ok(mapper.Map<UserDto>(user))
    );
}

private Exceptional<ExecutedCommand> ExecuteCommand(ValidatedCommand cmd)
{
    try
    {
        // Business logic
        return new ExecutedCommand(/* ... */);
    }
    catch (Exception ex)
    {
        return ex;  // Implicit conversion Exception -> Exceptional<T>
    }
}
```

## OpenAPI Documentation

### Documenting Endpoints

```csharp
[HttpPost("register")]
[AllowAnonymous]
[Tags("Authentication")]
[Summary("Register a new user")]
[Description("Creates a new user account with the provided details")]
[ProducesResponseType(StatusCodes.Status201Created, Type = typeof(UserDto))]
[ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiValidationResponse))]
[ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ApiErrorResponse))]
public async Task<IActionResult> RegisterAsync(
    [FromBody]
    [Description("User registration data")]
    CreateUserDto dto)
{
    // Implementation
}
```

### Security Scheme

```csharp
public class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components = new OpenApiComponents
        {
            SecuritySchemes = new Dictionary<string, OpenApiSecurityScheme>
            {
                ["bearer"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "JWT Authorization header using the Bearer scheme"
                }
            }
        };
        
        return Task.CompletedTask;
    }
}
```

### Scalar UI Setup

```csharp
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    
    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("Behind API")
            .WithTheme(ScalarTheme.BluePlanet)
            .WithDarkMode(true)
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
}
```

## Request/Response Flow

### Typical Flow

```
1. HTTP Request arrives
   ↓
2. Route matching (Controller/Action)
   ↓
3. Model binding (DTO deserialization)
   ↓
4. Validation filters (DataAnnotations)
   ↓
5. Authorization checks
   ↓
6. Action execution
   ├─ Business logic (monadic)
   ├─ Data access
   └─ Error handling (Railway-Oriented)
   ↓
7. Result mapping (to response DTO)
   ↓
8. HTTP Response serialization (JSON)
   ↓
9. Response sent to client
```

## Middleware Configuration

### Standard Order

```csharp
var app = builder.Build();

// Error handling (must be first)
app.UseGlobalExceptionHandler();

// HTTPS/Security
app.UseHttpsRedirection();
app.UseHsts();

// CORS
app.UseCors("AllowFrontend");

// Authentication/Authorization
app.UseAuthentication();
app.UseAuthorization();

// Logging
app.UseStructuredLogging();

// API Documentation
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// Routing
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
```

## Content Negotiation

### JSON-Only APIs

```csharp
services.AddControllers(options =>
{
    options.OutputFormatters.RemoveType<XmlDataContractSerializerOutputFormatter>();
});

services.AddMvc(options =>
{
    options.FormatterMappings.SetMediaTypeMappingForFormat(
        "json",
        MediaTypeHeaderValue.Parse("application/json")
    );
});
```

### Typed JSON Handling

```csharp
services.Configure<JsonOptions>(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.JsonSerializerOptions.Converters.Add(new OptionJsonConverterFactory());
    options.JsonSerializerOptions.Converters.Add(new ExceptionalJsonConverterFactory());
});
```

## Best Practices

### ✓ DO

- Use DTOs for request/response
- Return monadic types from services
- Document endpoints with OpenAPI attributes
- Use consistent error response format
- Implement pagination for list endpoints
- Use ILogger for structured logging
- Handle exceptions at boundary (controller)
- Use CancellationToken throughout

### ✗ DON'T

- Return domain entities directly
- Mix validation errors with HTTP errors
- Use bare exceptions for control flow
- Skip authorization checks
- Expose internal exception details in production
- Use bare `HttpContext.Request` without helpers
- Mix concerns (business logic in controller)

## Common Patterns

### Result-Based Response

```csharp
public async Task<IActionResult> GetAsync(Guid id)
{
    var result = await service.GetAsync(id);
    
    return result.Match<IActionResult>(
        ex => StatusCode(StatusCodes.Status500InternalServerError,
            new ApiErrorResponse("ERROR", ex.Message)),
        user => Ok(mapper.Map<UserDto>(user))
    );
}
```

### Validation-Based Response

```csharp
public async Task<IActionResult> CreateAsync(CreateDto dto)
{
    var result = await service.ValidateAndCreateAsync(dto);
    
    return await result.Match<Task<IActionResult>>(
        errors => Task.FromResult<IActionResult>(
            BadRequest(new ApiValidationResponse(errors))
        ),
        async entity =>
        {
            await service.SaveAsync(entity);
            var dto = mapper.Map<EntityDto>(entity);
            return CreatedAtAction(nameof(GetAsync), new { id = entity.Id }, dto);
        }
    );
}
```

## Summary

| Aspect | Convention |
|--------|-----------|
| Base Route | `/api/[controller]` |
| Method Naming | Verb + ResourceAsync (GetUserAsync, CreateUserAsync) |
| Response DTOs | ProjectedData, not domain models |
| Errors | ApiErrorResponse with Code and Message |
| Validation | Validation<T> with accumulated errors |
| Authentication | JWT Bearer with OpenAPI documentation |
| Pagination | PagedResult with Items, Page, TotalCount |
| Status Codes | 2xx success, 4xx client error, 5xx server error |
| Documentation | OpenAPI with Scalar UI |

---

**Next Steps**:
- Review DOMAIN_MODEL.md for entity design
- Check FUNCTIONAL_PROGRAMMING.md for error handling patterns
- See DATABASE_ARCHITECTURE.md for data access
- Configure Email Services with [EMAIL_SENDER_IMPLEMENTATION.md](./EMAIL_SENDER_IMPLEMENTATION.md) for user notifications and account management
