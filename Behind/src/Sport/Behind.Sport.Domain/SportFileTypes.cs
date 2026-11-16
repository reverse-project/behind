namespace Behind.Sport.Domain;

public static class SportFileTypes
{
    public const string Teams = "SportTeams";
    public const string Players = "SportPlayers";
    public const string TeamImageUrl = "/sport/teams/images";
    public const string PlayerImageUrl = "/sport/players/images";

    public static string TeamImage(Guid id) => $"{TeamImageUrl}/{id}";

    public static string PlayerImage(Guid id) => $"{PlayerImageUrl}/{id}";
}
