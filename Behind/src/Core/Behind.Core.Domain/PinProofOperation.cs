namespace Behind.Core.Domain;

public enum PinProofOperation
{
    Create,
    Change,
    Remove
}

public static class PinProofOperations
{
    public static PinProofOperation? Parse(string? value) =>
        value switch
        {
            "create" => PinProofOperation.Create,
            "change" => PinProofOperation.Change,
            "remove" => PinProofOperation.Remove,
            _ => null
        };

    public static string Value(PinProofOperation operation) =>
        operation switch
        {
            PinProofOperation.Create => "create",
            PinProofOperation.Change => "change",
            PinProofOperation.Remove => "remove",
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
}
