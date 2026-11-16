namespace Behind.Core.Database;

public sealed record AuditLabels(IReadOnlyDictionary<Guid, string> Values)
{
    public const char KeySeparator = ':';
    public const string KeyLabelSeparator = ": ";

    public static AuditLabels Empty { get; } = new(new Dictionary<Guid, string>());

    public string Label(string? text) =>
        text is not null && Guid.TryParse(text, out var id) && Values.TryGetValue(id, out var label)
            ? label
            : text ?? string.Empty;

    public string? LabelOrNull(string? text) => text is null ? null : Label(text);

    public string LabelKey(string key) =>
        string.Join(KeyLabelSeparator, key.Split(KeySeparator).Select(Label));
}
