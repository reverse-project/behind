---
name: csharp-code-standards
description: 'Coding rules for every C# change in the Behind repo (no comments, extension(...) blocks, records, braces, functional style, shared projects, tests, bug-fix confirmation). USE FOR: writing, reviewing or refactoring any C# code in Behind. Load before any other Behind skill.'
---

# Behind C# code standards

Source of truth: `/AGENTS.md`. Apply to all new or changed code.

## Style
- No comments in code. Code that needs a comment must be rewritten.
- Extension methods use the C# `extension(...)` block syntax, in files named `*Extensions.cs` (see `Behind.Core.Database/AuditLabelExtensions.cs`).
- Prefer records, immutable objects, `async`/`await` and thread-safe solutions.
- Prefer functional tools (monads, result types); create new ones when needed.
- Always use braces for `if`, `else`, `for`, `foreach`, `while`, `using`, with the body on its own lines. Never single-line bodies.
- Follow DRY, KISS, SOLID and Clean Code; reuse existing code, never duplicate it.

## Where code goes
- Generic code: `src/Common/Behind.Common`.
- Generic database code: `src/Common/Behind.Common.Database`.
- Generic web API code: `src/Common/Behind.Common.WebApi`.
- Core domain, database, web API: `src/Core/Behind.Core.*`.
- JSON serialization: `TypeDescriptor` + `JsonConverterFactory`, as in `Behind.Common.Converters` and `Behind.Core.WebApi/WebApiExtensions.cs`.

## Tests
- Every new functionality gets tests in `tests/Behind.Tests` (folders mirror projects: `Common`, `CommonDatabase`, `CommonWebApi`, `CoreCommon`, `CoreDatabase`, `CoreWebApi`, `Domain`). Reuse `Helpers/` test beds.
- Run from `Behind/`: `dotnet test tests/Behind.Tests/Behind.Tests.csproj` (narrow with `--filter`).

## Bugs found while working
List the identified bugs and use the `ask_user` tool to ask whether to fix them. Do exactly what the user answers.

## Always-attached chores
Changes also trigger: `update-changelog` (every feature/relevant change), `add-entity` (audit labels), `add-raw-database-editor` (every entity is editable in the raw database editor), `update-licenses` (package/library changes).
