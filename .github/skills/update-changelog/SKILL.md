---
name: update-changelog
description: 'Record a new feature or relevant modification in the Behind changelog. USE FOR: every feature, behavior change, new endpoint, new page or version bump in Behind.'
---

# Update the changelog

File: `Behind/src/Core/Behind.Core.WebApi/Frontend/changelog.json` (tested by `tests/Behind.Tests/CoreWebApi/ChangelogFrontendTests.cs`).

## Steps
1. Read the current version in `Behind/Directory.Build.props` (`<Version>`).
2. Find the release entry with that `version` (`{ "version", "date", "changes": [...] }`). If the version was bumped and there is no entry yet, add a new release entry at the **top** of the array with today's date (`yyyy-MM-dd`).
3. Add entries to `changes` as `{ "type": "added" | ..., "text": "..." }`. Match the types already used in the file. Texts are user-facing sentences describing the feature, mention new endpoints and permissions where relevant.
4. Keep the JSON valid and run `dotnet test Behind/tests/Behind.Tests --filter "FullyQualifiedName~ChangelogFrontendTests"`.
