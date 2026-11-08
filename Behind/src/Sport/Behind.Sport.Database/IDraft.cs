namespace Behind.Sport.Database;

public interface IDraft<TDraft>
    where TDraft : IDraft<TDraft>
{
    TDraft Normalized();
}
