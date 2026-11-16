namespace Behind.Common.Modules;

public sealed record ModuleMenuItem(
    Guid Id,
    string Title,
    string Url,
    int Position,
    ModuleMenuKind Kind = ModuleMenuKind.Link,
    ModuleAudience Audience = ModuleAudience.Everyone);
