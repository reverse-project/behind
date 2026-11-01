MUST NOT made comments in the code, if a code need comment it's bad code
MUST use the new extension(...) C# feature for extension methods
MUST use extension method files with the filename ending with Extensions
MUST use records, immutable objects and async await, thread safe solutions
MUST use functinal tools in the code or create new tools, monads if needed
MUST implement common functions and objects in Behind.Common project
MUST implement common database functions and objects in Behind.Common.Database project
MUST implement common webapi functions and objects in Behind.Common.WebApi project
MUST keep the code base clean, reuse existing code do not duplicate any of the code
MUST use DRY, KISS and SOLID principles
MUST follow Clean Code principles
MUST create tests for all the new functionalities
MUST NOT use shortened if statements or blocks (if, else, for, foreach, while, using) without braces, always use braces and put the body on its own lines, to avoid bugs like Apple's "goto fail"
MUST use TypeDescriptor and JsonConverterFactory for JSON related serialization tasks like in Behind.Common.Converters and Behind.Core.WebApi.WebApiExtensions file
MUST ask the user whether to fix the identified bugs or not (list them and use the ask_user tool), and do exactly as answered
MUST fill up the changelog (Behind/src/Core/Behind.Core.WebApi/Frontend/changelog.json) with every new feature or relevant modification, under the current version from Behind/Directory.Build.props (add a new release entry at the top when the version is bumped)
MUST update the audit log label resolution (Behind/src/Core/Behind.Core.Database/AuditLabelExtensions.cs) and its tests whenever a new entity is created or an entity is used anywhere in the code, so the audit log UI shows human readable names instead of UUIDs
MUST give every management page (users, roles, sites and any new one) soft delete, hard delete and restore functionality: deleting soft deletes, developers can list deleted records, restore them and delete them permanently
MUST keep the Licenses page up to date whenever a NuGet package (Behind/Directory.Packages.props, including transitive ones) or a frontend library (Behind/src/Core/Behind.Core.WebApi/Frontend/libraries, css, webfonts) is added, removed or upgraded: update name, version, license, copyright, url, scope and text in Behind/src/Core/Behind.Core.WebApi/Frontend/licenses.json (and add the license text under Frontend/licenses when it is new), only accept licenses compatible with GPL-2.0-or-later, then run `dotnet test Behind/tests/Behind.Tests --filter "FullyQualifiedName~LicensesFrontendTests"` and make sure it passes