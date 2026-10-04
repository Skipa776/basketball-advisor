using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Players;

namespace FantasyBasketball.Domain.Draft;

public enum RiskMode
{
    /// <summary>Highest expected final-roster value.</summary>
    Mean,

    /// <summary>Mean minus κ standard deviations: prefers the surer roster.</summary>
    Cautious,

    /// <summary>The 90th percentile: prefers the roster with the higher ceiling.</summary>
    Upside,
}

public sealed class SimulationOptions
{
    public const string SectionName = "DraftSimulation";

    public int Candidates { get; init; } = 15;

    public int Rollouts { get; init; } = 500;

    public decimal Kappa { get; init; } = 0.5m;

    public bool IsValid() => Candidates is >= 1 and <= 30 && Rollouts is >= 20 and <= 5000 && Kappa >= 0m;
}

/// <summary>One candidate pick, read from its rollouts: the user's final-roster season points.</summary>
public sealed record SimulatedCandidate(
    PlayerId PlayerId,
    decimal Mean,
    decimal Sd,
    decimal P10,
    decimal P90,
    decimal Score,
    decimal Edge,
    decimal EdgeSe,
    decimal? SurvivalToNextPick);

/// <summary>Candidates ranked by <see cref="Mode"/>; <see cref="SimulatedCandidate.Edge"/> is against the top one.</summary>
public sealed record SimulatedBoard(IReadOnlyList<SimulatedCandidate> Candidates, RiskMode Mode, int Rollouts, int? NextUserPick);

/// <summary>
/// Monte Carlo lookahead (draft_simulation). For each of the best K available players and each
/// of N rollouts, the user takes the candidate, the other teams pick as simulated opponents do
/// (the fitted choice model, or the ADP-jitter heuristic), the user's later picks take the best
/// expected value that fills an open starting slot, and the final roster is scored by the lineup
/// optimizer on that rollout's draws of per-game value and games played. Every candidate sees
/// the same draws and opponent random stream in rollout n (common random numbers), so the edge
/// between two candidates has a small standard error. Seeded by the caller per draft and pick.
/// </summary>
public sealed class DraftSimulator(SimulationOptions options)
{
    private const int JitterPool = 12;
    private const int BenchCap = 3;

    public SimulatedBoard Simulate(
        DraftSession session,
        LineupOptimizer lineup,
        IReadOnlyList<DraftCandidate> pool,
        int seed,
        RiskMode mode,
        OpponentChoiceModel? opponents = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(lineup);
        ArgumentNullException.ThrowIfNull(pool);
        if (!options.IsValid())
        {
            throw new ArgumentException("Simulation options are invalid.", nameof(options));
        }

        var state = new Setup(session, lineup, pool);
        var available = Enumerable.Range(0, pool.Count).Where(i => !state.InitiallyTaken[i]).ToArray();
        var candidates = available.OrderByDescending(i => state.Mean[i]).ThenBy(i => pool[i].PlayerId.Value)
            .Take(options.Candidates).ToArray();
        if (candidates.Length == 0)
        {
            return new SimulatedBoard([], mode, options.Rollouts, session.NextUserPickAfterCurrent);
        }

        var draws = Draws(state, seed);
        // Survival is read at the user's next pick: the one after this when on the clock, else
        // the upcoming one, where the candidate would be taken if he lasts.
        var surviveAt = session.IsUserPick(session.CurrentPick) ? 1 : 0;
        var values = new decimal[candidates.Length][];
        var survival = new int[candidates.Length][];
        for (var k = 0; k < candidates.Length; k++)
        {
            values[k] = new decimal[options.Rollouts];
            survival[k] = new int[options.Rollouts];
        }

        Parallel.For(0, options.Rollouts, n =>
        {
            for (var k = 0; k < candidates.Length; k++)
            {
                (values[k][n], survival[k][n]) = Rollout(state, lineup, candidates, k, draws[n], unchecked((seed * 397) ^ (n * 7919)), opponents, surviveAt);
            }
        });

        return Summarize(state, candidates, values, survival, mode, session.NextUserPickAfterCurrent);
    }

    private SimulatedBoard Summarize(Setup state, int[] candidates, decimal[][] values, int[][] survival, RiskMode mode, int? nextUserPick)
    {
        var n = options.Rollouts;
        var stats = candidates.Select((_, k) =>
        {
            var sorted = values[k].Order().ToArray();
            var mean = sorted.Average();
            var sd = Sd(sorted, mean);
            var p10 = sorted[(int)(0.1 * (n - 1))];
            var p90 = sorted[(int)(0.9 * (n - 1))];
            var score = mode switch
            {
                RiskMode.Cautious => mean - (options.Kappa * sd),
                RiskMode.Upside => p90,
                _ => mean,
            };
            return (k, mean, sd, p10, p90, score);
        }).OrderByDescending(entry => entry.score).ThenBy(entry => state.Pool[candidates[entry.k]].PlayerId.Value).ToArray();
        var best = stats[0].k;
        var runnerUp = stats.Length > 1 ? stats[1].k : best;
        return new SimulatedBoard(stats.Select(entry =>
        {
            var differences = values[entry.k].Zip(values[best], (value, top) => value - top).ToArray();
            var edge = differences.Average();
            var edgeSe = Sd(differences, edge) / (decimal)Math.Sqrt(n);
            var reference = entry.k == best ? runnerUp : best;
            decimal? survives = nextUserPick is null || reference == entry.k
                ? null
                : (decimal)survival[reference].Count(bits => (bits & (1 << entry.k)) != 0) / n;
            return new SimulatedCandidate(state.Pool[candidates[entry.k]].PlayerId, entry.mean, entry.sd, entry.p10, entry.p90,
                entry.score, edge, edgeSe, survives);
        }).ToArray(), mode, n, nextUserPick);
    }

    /// <summary>Per rollout, each player's value per scheduled game: a per-game draw times the share of games played.</summary>
    private double[][] Draws(Setup state, int seed)
    {
        var draws = new double[options.Rollouts][];
        for (var n = 0; n < options.Rollouts; n++)
        {
            var random = new Random(unchecked((seed * 31) + n));
            var row = new double[state.Pool.Count];
            for (var i = 0; i < row.Length; i++)
            {
                var candidate = state.Pool[i];
                var scheduled = Math.Max(1, state.ScheduledGames[i]);
                if (candidate.Distribution is { } distribution)
                {
                    var perGame = Math.Max(0, (double)distribution.PerGameMean + ((double)distribution.PerGameSd * Normal(random)));
                    var games = distribution.Games.Quantile((decimal)random.NextDouble());
                    row[i] = perGame * Math.Min(1.0, games / (double)scheduled);
                }
                else
                {
                    // ponytail: no distribution published — the point value spread over the schedule, no variance;
                    // publish distributions (all four M2 models active) to give these players their spread.
                    row[i] = (double)candidate.ProjectedSeasonValue / scheduled;
                }
            }

            draws[n] = row;
        }

        return draws;
    }

    private static (decimal Value, int Survivors) Rollout(
        Setup state, LineupOptimizer lineup, int[] candidates, int k, double[] draw, int streamSeed, OpponentChoiceModel? opponents, int surviveAt)
    {
        var random = new Random(streamSeed);
        var taken = (bool[])state.InitiallyTaken.Clone();
        var open = (int[])state.InitialOpen.Clone();
        var held = state.InitialHeld.Select(list => new List<int>(list)).ToArray();
        var user = state.UserTeam;
        var placedCandidate = false;
        var survivors = 0;
        var userPicksMade = 0;
        foreach (var (pick, team) in state.RemainingPicks)
        {
            int chosen;
            if (team == user)
            {
                if (userPicksMade == surviveAt)
                {
                    for (var j = 0; j < candidates.Length; j++)
                    {
                        survivors |= taken[candidates[j]] ? 0 : 1 << j;
                    }
                }

                chosen = !placedCandidate && !taken[candidates[k]] ? candidates[k] : UserPolicy(state, taken, open[user]);
                placedCandidate = true;
                userPicksMade++;
            }
            else
            {
                chosen = OpponentPick(state, taken, open[team], held[team], pick, random, opponents);
            }

            if (chosen < 0)
            {
                break;
            }

            taken[chosen] = true;
            held[team].Add(chosen);
            var fits = state.SlotMask[chosen] & open[team];
            open[team] &= ~(fits & -fits);
        }

        var roster = held[user];
        Span<double> perGame = stackalloc double[roster.Count];
        Span<int> teams = stackalloc int[roster.Count];
        Span<int> masks = stackalloc int[roster.Count];
        for (var i = 0; i < roster.Count; i++)
        {
            perGame[i] = draw[roster[i]];
            teams[i] = state.Team[roster[i]];
            masks[i] = state.SlotMask[roster[i]];
        }

        return (lineup.Score(perGame, teams, masks), survivors);
    }

    /// <summary>The user's later picks: best expected value that fills an open starting slot, else best value.</summary>
    private static int UserPolicy(Setup state, bool[] taken, int open)
    {
        var fallback = -1;
        foreach (var i in state.ByMean)
        {
            if (taken[i])
            {
                continue;
            }

            if (open == 0 || (state.SlotMask[i] & open) != 0)
            {
                return i;
            }

            fallback = fallback < 0 ? i : fallback;
        }

        return fallback;
    }

    /// <summary>Mirrors <see cref="SimulatedOpponent"/> on arrays: the choice model, else ADP jitter with the slot and bench rules.</summary>
    private static int OpponentPick(Setup state, bool[] taken, int open, List<int> held, int pick, Random random, OpponentChoiceModel? model)
    {
        Span<int> pool = stackalloc int[model is null ? JitterPool : Math.Min(model.Candidates, 128)];
        var count = 0;
        foreach (var i in state.ByAdp)
        {
            if (!taken[i])
            {
                pool[count++] = i;
                if (count == pool.Length)
                {
                    break;
                }
            }
        }

        if (count == 0)
        {
            foreach (var i in state.ByMean)
            {
                if (!taken[i])
                {
                    return i;
                }
            }

            return -1;
        }

        if (model is not null)
        {
            var adp = new decimal[count];
            var need = new bool[count];
            for (var j = 0; j < count; j++)
            {
                adp[j] = state.Adp[pool[j]];
                need[j] = (state.SlotMask[pool[j]] & open) != 0;
            }

            return pool[model.Choose(((pick - 1) / state.TeamCount) + 1, adp, need, (decimal)random.NextDouble())];
        }

        Span<double> jittered = stackalloc double[count];
        for (var j = 0; j < count; j++)
        {
            var adp = state.Adp[pool[j]];
            jittered[j] = (double)adp + ((double)DraftBoard.AdpSigma(adp, state.AdpSd[pool[j]]) * Normal(random));
        }

        var best = -1;
        var bestValue = 0.0;
        var fallback = pool[0];
        for (var j = 0; j < count; j++)
        {
            if ((best < 0 || jittered[j] < bestValue) && !Blocked(state, pool[j], open, held))
            {
                best = pool[j];
                bestValue = jittered[j];
            }
        }

        return best >= 0 ? best : fallback;
    }

    private static bool Blocked(Setup state, int candidate, int open, List<int> held)
    {
        if (open != 0)
        {
            return state.SlotMask[candidate] != 0 && (state.SlotMask[candidate] & open) == 0;
        }

        var positions = state.PositionMask[candidate];
        var stacked = 0;
        foreach (var player in held)
        {
            var mine = state.PositionMask[player];
            stacked += mine != 0 && (mine & ~positions) == 0 ? 1 : 0;
        }

        return stacked >= BenchCap;
    }

    /// <summary>Standard normal by Box-Muller.</summary>
    private static readonly Func<Random, double> Normal = random =>
        Math.Sqrt(-2.0 * Math.Log(1.0 - random.NextDouble())) * Math.Cos(2.0 * Math.PI * random.NextDouble());

    private static decimal Sd(IReadOnlyList<decimal> values, decimal mean) =>
        values.Count < 2 ? 0m : (decimal)Math.Sqrt((double)(values.Sum(value => (value - mean) * (value - mean)) / (values.Count - 1)));

    /// <summary>Everything a rollout reads, as arrays indexed by pool position.</summary>
    private sealed class Setup
    {
        private static readonly string[] BasePositions = ["PG", "SG", "SF", "PF", "C"];

        public Setup(DraftSession session, LineupOptimizer lineup, IReadOnlyList<DraftCandidate> pool)
        {
            Pool = pool;
            TeamCount = session.TeamCount;
            UserTeam = session.UserSlot;
            var index = pool.Select((candidate, i) => (candidate.PlayerId, i)).ToDictionary(pair => pair.PlayerId, pair => pair.i);
            Mean = pool.Select(candidate => candidate.Distribution?.Mean ?? candidate.ProjectedSeasonValue).ToArray();
            Adp = pool.Select(candidate => candidate.AverageDraftPosition ?? 0m).ToArray();
            AdpSd = pool.Select(candidate => candidate.AdpStandardDeviation).ToArray();
            SlotMask = pool.Select(candidate => lineup.SlotMask(candidate.Positions)).ToArray();
            PositionMask = pool.Select(candidate => BasePositions.Select((position, bit) => candidate.Positions.Contains(position) ? 1 << bit : 0).Sum()).ToArray();
            Team = pool.Select(candidate => lineup.Schedule.TeamIndex(candidate.TeamId)).ToArray();
            ScheduledGames = Team.Select(lineup.Schedule.Games).ToArray();
            ByMean = Enumerable.Range(0, pool.Count).OrderByDescending(i => Mean[i]).ThenBy(i => pool[i].PlayerId.Value).ToArray();
            ByAdp = Enumerable.Range(0, pool.Count).Where(i => Adp[i] > 0m).OrderBy(i => Adp[i]).ThenBy(i => pool[i].PlayerId.Value).ToArray();
            InitiallyTaken = new bool[pool.Count];
            InitialOpen = Enumerable.Repeat((1 << lineup.StarterCount) - 1, session.TeamCount + 1).ToArray();
            InitialHeld = Enumerable.Range(0, session.TeamCount + 1).Select(_ => new List<int>()).ToArray();
            foreach (var pick in session.Picks.OrderBy(pick => pick.PickNumber))
            {
                if (!index.TryGetValue(pick.PlayerId, out var i))
                {
                    continue;
                }

                var team = SimulatedOpponent.SlotOnClock(pick.PickNumber, session.TeamCount);
                InitiallyTaken[i] = true;
                InitialHeld[team].Add(i);
                var fits = SlotMask[i] & InitialOpen[team];
                InitialOpen[team] &= ~(fits & -fits);
            }

            RemainingPicks = Enumerable.Range(session.CurrentPick, Math.Max(0, (session.TeamCount * session.RoundCount) - session.CurrentPick + 1))
                .Select(pick => (pick, SimulatedOpponent.SlotOnClock(pick, session.TeamCount)))
                .ToArray();
        }

        public IReadOnlyList<DraftCandidate> Pool { get; }

        public int TeamCount { get; }

        public int UserTeam { get; }

        public decimal[] Mean { get; }

        public decimal[] Adp { get; }

        public decimal?[] AdpSd { get; }

        public int[] SlotMask { get; }

        public int[] PositionMask { get; }

        public int[] Team { get; }

        public int[] ScheduledGames { get; }

        public int[] ByMean { get; }

        public int[] ByAdp { get; }

        public bool[] InitiallyTaken { get; }

        public int[] InitialOpen { get; }

        public List<int>[] InitialHeld { get; }

        public (int Pick, int Team)[] RemainingPicks { get; }
    }
}
