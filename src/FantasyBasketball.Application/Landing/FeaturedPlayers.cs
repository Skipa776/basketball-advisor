namespace FantasyBasketball.Application.Landing;

/// <summary>
/// The landing page's headshot strip: 30 consensus top fantasy players for the
/// 2025-26 season, owner-approved 2026-09-23. Names match Basketball-Reference
/// spellings after <see cref="Domain.Players.PlayerName.Normalize"/>.
/// </summary>
public static class FeaturedPlayers
{
    public static IReadOnlyList<string> Names { get; } =
    [
        "Nikola Jokic", "Shai Gilgeous-Alexander", "Victor Wembanyama", "Luka Doncic",
        "Giannis Antetokounmpo", "Anthony Davis", "Cade Cunningham", "Anthony Edwards",
        "Karl-Anthony Towns", "Domantas Sabonis", "Trae Young", "LeBron James",
        "Stephen Curry", "Kevin Durant", "Devin Booker", "Donovan Mitchell",
        "Jalen Brunson", "Tyrese Maxey", "James Harden", "Jayson Tatum",
        "Tyrese Haliburton", "Jalen Johnson", "Scottie Barnes", "Amen Thompson",
        "Alperen Sengun", "Evan Mobley", "Chet Holmgren", "Jamal Murray",
        "Josh Giddey", "Paolo Banchero",
    ];
}
