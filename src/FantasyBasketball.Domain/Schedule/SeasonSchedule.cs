using FantasyBasketball.Domain.Players;

namespace FantasyBasketball.Domain.Schedule;

/// <summary>
/// Which teams play on each game day of a season (US Eastern dates), indexed for the lineup
/// optimizer: <see cref="Plays"/>[team][day].
/// </summary>
public sealed class SeasonSchedule
{
    private static readonly TimeZoneInfo Eastern = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
    private readonly Dictionary<NbaTeamId, int> teamIndex;

    public SeasonSchedule(IReadOnlyList<NbaGame> games)
    {
        ArgumentNullException.ThrowIfNull(games);
        if (games.Count == 0)
        {
            throw new ArgumentException("A schedule needs games.", nameof(games));
        }

        var dated = games.Select(game => (Game: game, Date: DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(game.StartsAt, Eastern).DateTime))).ToArray();
        var days = dated.Select(item => item.Date).Distinct().Order().ToArray();
        var dayIndex = days.Select((date, index) => (date, index)).ToDictionary(pair => pair.date, pair => pair.index);
        teamIndex = dated.SelectMany(item => new[] { item.Game.HomeTeamId, item.Game.AwayTeamId }).Distinct()
            .Select((team, index) => (team, index)).ToDictionary(pair => pair.team, pair => pair.index);
        Plays = Enumerable.Range(0, teamIndex.Count).Select(_ => new bool[days.Length]).ToArray();
        foreach (var (game, date) in dated)
        {
            Plays[teamIndex[game.HomeTeamId]][dayIndex[date]] = true;
            Plays[teamIndex[game.AwayTeamId]][dayIndex[date]] = true;
        }

        WeekOfDay = days.Select(date => (date.DayNumber - days[0].DayNumber) / 7).ToArray();
        Weeks = WeekOfDay[^1] + 1;
        GamesByWeek = Plays.Select(row =>
        {
            var weeks = new int[Weeks];
            for (var day = 0; day < row.Length; day++)
            {
                weeks[WeekOfDay[day]] += row[day] ? 1 : 0;
            }

            return weeks;
        }).ToArray();
        var counts = Plays.Select(row => row.Count(plays => plays)).ToArray();
        // A player with no current team gets the schedule of the team with the median game count.
        TypicalTeam = Array.IndexOf(counts, counts.Order().ElementAt(counts.Length / 2));
    }

    public bool[][] Plays { get; }

    public int[] WeekOfDay { get; }

    public int[][] GamesByWeek { get; }

    public int Days => WeekOfDay.Length;

    public int Weeks { get; }

    public int Teams => Plays.Length;

    public int TypicalTeam { get; }

    public int TeamIndex(NbaTeamId? team) =>
        team is { } id && teamIndex.TryGetValue(id, out var index) ? index : TypicalTeam;

    public int Games(int team) => Plays[team].Count(plays => plays);
}
