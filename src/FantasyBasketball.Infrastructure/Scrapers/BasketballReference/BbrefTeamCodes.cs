namespace FantasyBasketball.Infrastructure.Scrapers.BasketballReference;

/// <summary>balldontlie and Basketball-Reference differ on three current team codes.</summary>
public static class BbrefTeamCodes
{
    private static readonly Dictionary<string, string> ToBbref = new(StringComparer.Ordinal)
    {
        ["BKN"] = "BRK",
        ["CHA"] = "CHO",
        ["PHX"] = "PHO",
    };

    private static readonly Dictionary<string, string> FromBbref =
        ToBbref.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.Ordinal);

    public static string FromBallDontLie(string abbreviation) =>
        ToBbref.GetValueOrDefault(abbreviation, abbreviation);

    public static string ToBallDontLie(string bbrefCode) =>
        FromBbref.GetValueOrDefault(bbrefCode, bbrefCode);
}
