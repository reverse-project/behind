---
name: increase-version
description: 'Start a new Behind version on its own development branch with the version bump and an empty changelog entry as its first commit. USE FOR: every new version (patch, minor or major), starting work on the next release, bumping Behind/Directory.Build.props.'
---

# Increase the version

Every new version is developed on its own `development/v<version>` branch. The first commit of that branch only changes `<Version>` in `Behind/Directory.Build.props` and adds an empty release entry for the new version to `Behind/src/Core/Behind.Core.WebApi/Frontend/changelog.json`. `master` only contains `Release <version>` squash commits.

## Steps
1. Make sure the working tree is clean and fetch the latest `master` (`git switch master`, `git pull --ff-only` when a remote is configured).
2. Read the current version from `<Version>` in `Behind/Directory.Build.props` on `master`.
3. Compute the new semantic version. Increase the patch part by default (`1.0.2` → `1.0.3`). Increase the minor part (`1.0.2` → `1.1.0`) or the major part (`1.0.2` → `2.0.0`) only when the user asks for it. Stop and ask the user if a `development/v<new>` branch or a `v<new>` tag already exists.
4. Create the branch from `master`: `git switch -c development/v<new> master`.
5. Change only `<Version>` in `Behind/Directory.Build.props` to the new version.
6. Add a new release entry without details at the **top** of the array in `Behind/src/Core/Behind.Core.WebApi/Frontend/changelog.json`, with today's date (`yyyy-MM-dd`) and no changes, keeping the file's formatting:
   ```json
   {
     "version": "<new>",
     "date": "<yyyy-MM-dd>",
     "changes": []
   },
   ```
7. Do not touch any other file. Run `dotnet test Behind/tests/Behind.Tests --filter "FullyQualifiedName~ChangelogFrontendTests"`; the newest release may have no changes, every previous release must have at least one.
8. Make this the first commit of the branch, containing only these two files: `git add Behind/Directory.Build.props Behind/src/Core/Behind.Core.WebApi/Frontend/changelog.json` and `git commit -m "Increase version to <new>"`.
9. Continue the work of the version on this branch with further commits, filling the `changes` of the new entry with the [update-changelog skill](../update-changelog/SKILL.md).

## Releasing the version
1. Make sure the new changelog entry lists the changes of the version and its `date` is the release date.
2. Verify the branch: build and run the tests (`dotnet test Behind/tests/Behind.Tests`) and the Node tests (`node --test Behind/tests/tools/*.test.cjs Behind/tests/frontend-runtime-tests/*.test.cjs`).
3. Squash the whole branch into a single commit on `master`: `git switch master`, `git merge --squash development/v<new>`, `git commit -m "Release <new>"`.
4. Tag the release commit: `git tag v<new>`.
5. Never commit anything else to `master`; every change reaches `master` only through a `Release <version>` squash commit.
