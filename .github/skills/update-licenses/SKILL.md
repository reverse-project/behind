---
name: update-licenses
description: 'Keep the Behind Licenses page up to date when a NuGet package (incl. transitive) or frontend library, css or webfont is added, removed or upgraded. USE FOR: any change to Directory.Packages.props or Frontend/libraries, css, webfonts.'
---

# Update the Licenses page

Trigger: a NuGet package (`Behind/Directory.Packages.props`, including transitive ones) or a frontend library (`Behind/src/Core/Behind.Core.WebApi/Frontend/libraries`, `css`, `webfonts`) is added, removed or upgraded.

## Steps
1. Edit `Behind/src/Core/Behind.Core.WebApi/Frontend/licenses.json`: update `name`, `version`, `license`, `copyright`, `url`, `scope` and `text` for each affected entry; remove entries for removed items. Follow the shape of existing entries.
2. If the license is new, add its text file under `Behind/src/Core/Behind.Core.WebApi/Frontend/licenses/`.
3. Only accept licenses compatible with GPL-2.0-or-later. If one is not, stop and tell the user instead of adding it.
4. Run from the repository root and make sure it passes:
   `dotnet test Behind/tests/Behind.Tests --filter "FullyQualifiedName~LicensesFrontendTests"`
5. Add a changelog entry (`update-changelog`) when the change is user-relevant.
