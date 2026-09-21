using System.Diagnostics;
using FantasyBasketball.Domain.Draft;
using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Recommendations;
using FantasyBasketball.Domain.Stats;
using Shouldly;

namespace FantasyBasketball.Domain.Tests.Draft;

public sealed class DraftEngineTests
{
    [Fact]
    public void C02_unverified_context_is_visible_risk_evidence_without_an_extra_score_penalty()
    {
        var league = LeagueCatalog.CreateSeedPointsLeague(Guid.NewGuid());
        var candidate = Candidate(1000m, ["PG"], null);
        var board = new DraftBoard(new DraftValueCalculator(new DraftWeightOptions()));
        var plain = board.Rank(CreateSession(), league, [candidate], []).Rankings[0];
        var flagged = board.Rank(CreateSession(), league, [candidate with { HasUnverifiedContext = true }], []).Rankings[0];
        flagged.Total.ShouldBe(plain.Total);
        flagged.Evidence.ShouldContain(item => item.Kind == EvidenceKind.Context
            && item.Polarity == EvidencePolarity.Risk && item.Statement.Contains("unverified", StringComparison.Ordinal));
    }

    [Fact]
    public void Completed_draft_rejects_extra_picks_and_undo_reopens_it()
    {
        var session = new DraftSession(Guid.NewGuid(), 1, 1, 1);
        session.MakePick(new PlayerId(Guid.NewGuid()));
        Should.Throw<InvalidOperationException>(() => session.MakePick(new PlayerId(Guid.NewGuid())))
            .Message.ShouldBe("The draft is complete.");
        session.Picks.Count.ShouldBe(1);
        session.UndoLastPick();
        session.MakePick(new PlayerId(Guid.NewGuid())).PickNumber.ShouldBe(1);
    }

    [Fact]
    public void D01_D02_total_uses_only_five_addends()
    {
        var calculator = new DraftValueCalculator(new DraftWeightOptions());
        var evidence = Supporting("value");

        var first = calculator.Calculate(
            new PlayerId(Guid.NewGuid()),
            projectedSeasonValue: 100m,
            valueAboveReplacement: 10m,
            positionalScarcity: 2m,
            rosterFit: -1m,
            marketValue: 4m,
            contextAdjustment: 20m,
            injuryRisk: 0m,
            roleRisk: 0m,
            evidence);
        var second = calculator.Calculate(
            first.PlayerId,
            projectedSeasonValue: 999m,
            valueAboveReplacement: 10m,
            positionalScarcity: 2m,
            rosterFit: -1m,
            marketValue: 4m,
            contextAdjustment: 20m,
            injuryRisk: 0m,
            roleRisk: 0m,
            evidence);

        first.Total.ShouldBe(13m);
        second.Total.ShouldBe(first.Total);
    }

    [Fact]
    public void D03_open_eligible_slot_has_zero_roster_fit()
    {
        var board = CreateBoard();
        var candidate = Candidate(100m, ["C"], 5m);

        var result = board.Rank(
            CreateSession(),
            LeagueCatalog.CreateSeedPointsLeague(Guid.NewGuid()),
            [candidate],
            []);

        result.Rankings.Single().RosterFit.ShouldBe(0m);
    }

    [Fact]
    public void D04_replacement_level_recomputes_after_unrelated_pick()
    {
        var candidates = Enumerable.Range(1, 92)
            .Select(rank => Candidate(200m - rank, ["PG"], rank))
            .ToArray();
        var target = candidates[20];
        var picked = candidates[0];
        var session = CreateSession();
        var board = CreateBoard();

        var before = board.Rank(
            session,
            LeagueCatalog.CreateSeedPointsLeague(Guid.NewGuid()),
            candidates,
            []);
        session.MakePick(picked.PlayerId);
        var after = board.Rank(
            session,
            LeagueCatalog.CreateSeedPointsLeague(Guid.NewGuid()),
            candidates,
            []);

        ValueFor(before, target.PlayerId).ValueAboveReplacement.ShouldNotBe(
            ValueFor(after, target.PlayerId).ValueAboveReplacement);
    }

    [Fact]
    public void D05_D06_missing_adp_is_zero_with_evidence()
    {
        var candidate = Candidate(100m, ["PG"], null);

        var value = CreateBoard().Rank(
                CreateSession(),
                LeagueCatalog.CreateSeedPointsLeague(Guid.NewGuid()),
                [candidate],
                [])
            .Rankings.Single();

        value.MarketValue.ShouldBe(0m);
        value.Evidence.ShouldContain(item =>
            item.Kind == EvidenceKind.Market
            && item.Statement.Contains("No ADP", StringComparison.Ordinal));
        value.Evidence.ShouldNotBeEmpty();
    }

    [Fact]
    public void D07_category_board_uses_category_totals_and_banner()
    {
        var league = new FantasyLeague(
            Guid.NewGuid(),
            "Categories",
            LeagueType.Categories,
            10,
            [],
            [StatKey.PTS, StatKey.AST],
            [new RosterSlot(RosterSlotKind.UTIL)],
            LineupCadence.Daily);
        var lowerPoints = Candidate(
            100m,
            ["PG"],
            5m,
            new Dictionary<StatKey, decimal>
            {
                [StatKey.PTS] = 10m,
                [StatKey.AST] = 20m,
            });
        var higherPoints = Candidate(
            200m,
            ["PG"],
            6m,
            new Dictionary<StatKey, decimal>
            {
                [StatKey.PTS] = 20m,
                [StatKey.AST] = 20m,
            });

        var result = CreateBoard().Rank(
            CreateSession(),
            league,
            [lowerPoints, higherPoints],
            []);

        result.Banner.ShouldNotBeNull();
        result.Rankings[0].PlayerId.ShouldBe(higherPoints.PlayerId);
    }

    [Fact]
    public void D08_D09_pick_and_undo_restore_board_and_availability()
    {
        var candidates = new[]
        {
            Candidate(100m, ["PG"], 3m),
            Candidate(90m, ["C"], 4m),
        };
        var session = CreateSession();
        var board = CreateBoard();
        var before = board.Rank(
            session,
            LeagueCatalog.CreateSeedPointsLeague(Guid.NewGuid()),
            candidates,
            []);

        session.MakePick(candidates[0].PlayerId);
        var picked = board.Rank(
            session,
            LeagueCatalog.CreateSeedPointsLeague(Guid.NewGuid()),
            candidates,
            []);
        picked.Rankings.Select(value => value.PlayerId)
            .ShouldNotContain(candidates[0].PlayerId);

        session.UndoLastPick();
        var restored = board.Rank(
            session,
            LeagueCatalog.CreateSeedPointsLeague(Guid.NewGuid()),
            candidates,
            []);
        restored.Rankings.Select(value => value.PlayerId)
            .ShouldBe(before.Rankings.Select(value => value.PlayerId));
        restored.Rankings.Select(value => value.Total)
            .ShouldBe(before.Rankings.Select(value => value.Total));
    }

    [Fact]
    public void E01_E02_recommendations_require_structured_evidence()
    {
        Should.Throw<ArgumentException>(() => new Recommendation(
            Guid.NewGuid(),
            "Draft",
            null,
            1m,
            Confidence.High,
            []));

        var risk = new RecommendationEvidence(
            EvidenceKind.Injury,
            EvidencePolarity.Risk,
            "Active injury risk",
            0.5m);
        new Recommendation(
            Guid.NewGuid(),
            "Draft",
            null,
            1m,
            Confidence.Low,
            [risk])
            .Evidence.Single().Polarity.ShouldBe(EvidencePolarity.Risk);
    }

    [Fact]
    public void D06_ranked_draft_recommendations_all_carry_evidence()
    {
        var board = CreateBoard().Rank(
            CreateSession(),
            LeagueCatalog.CreateSeedPointsLeague(Guid.NewGuid()),
            [
                Candidate(100m, ["PG"], 5m),
                Candidate(90m, ["C"], null),
            ],
            []);

        var recommendations = new DraftRecommendationEngine(
                new ConfidenceCalculator())
            .Recommend(
                board,
                new ConfidenceFactors(1m, 1m, 1m, 1m, 1m));

        recommendations.Count.ShouldBe(2);
        recommendations.ShouldAllBe(item => item.Evidence.Count > 0);
        recommendations[0].Score.ShouldBeGreaterThanOrEqualTo(
            recommendations[1].Score);
    }

    [Fact]
    public void E03_E05_confidence_and_evidence_order_are_deterministic()
    {
        var calculator = new ConfidenceCalculator();
        calculator.Calculate(new ConfidenceFactors(1m, 1m, 1m, 1m, 1m))
            .ShouldBe(Confidence.High);
        calculator.Calculate(new ConfidenceFactors(0m, 0m, 0m, 0m, 0m))
            .ShouldBe(Confidence.Speculative);

        var ordered = EvidenceOrderer.Order(
        [
            new(EvidenceKind.Injury, EvidencePolarity.Risk, "risk", 1m),
            new(EvidenceKind.Market, EvidencePolarity.Supporting, "small", 1m),
            new(EvidenceKind.Scarcity, EvidencePolarity.Supporting, "large", 3m),
        ]);
        ordered.Select(item => item.Statement)
            .ShouldBe(["large", "small", "risk"]);
    }

    [Fact]
    public void Full_pool_rerank_completes_under_500ms()
    {
        var candidates = Enumerable.Range(1, 300)
            .Select(rank => Candidate(400m - rank, ["PG", "SG"], rank))
            .ToArray();
        var timer = Stopwatch.StartNew();

        var result = CreateBoard().Rank(
            CreateSession(),
            LeagueCatalog.CreateSeedPointsLeague(Guid.NewGuid()),
            candidates,
            []);

        timer.Stop();
        result.Rankings.Count.ShouldBe(300);
        timer.Elapsed.ShouldBeLessThan(TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public void Full_manual_mock_draft_completes_with_explained_recommendations()
    {
        var candidates = Enumerable.Range(1, 140)
            .Select(rank => Candidate(300m - rank, ["PG", "SG"], rank))
            .ToArray();
        var session = CreateSession();
        var board = CreateBoard();
        var recommender = new DraftRecommendationEngine(
            new ConfidenceCalculator());
        var league = LeagueCatalog.CreateSeedPointsLeague(Guid.NewGuid());

        while (session.CurrentPick <= session.TeamCount * session.RoundCount)
        {
            var ranked = board.Rank(session, league, candidates, []);
            var recommendations = recommender.Recommend(
                ranked,
                new ConfidenceFactors(1m, 1m, 1m, 1m, 1m));
            recommendations.ShouldNotBeEmpty();
            recommendations[0].Evidence.ShouldNotBeEmpty();
            session.MakePick(recommendations[0].SubjectPlayerId!.Value);
        }

        session.Picks.Count.ShouldBe(130);
        board.Rank(session, league, candidates, []).Rankings.Count.ShouldBe(10);
    }

    private static DraftBoard CreateBoard() =>
        new(new DraftValueCalculator(new DraftWeightOptions()));

    private static DraftSession CreateSession() =>
        new(Guid.NewGuid(), 10, 13, 1);

    private static DraftCandidate Candidate(
        decimal value,
        IReadOnlyList<string> positions,
        decimal? adp,
        IReadOnlyDictionary<StatKey, decimal>? categories = null) =>
        new(
            new PlayerId(Guid.NewGuid()),
            value,
            positions,
            adp,
            0m,
            0m,
            0m,
            categories ?? new Dictionary<StatKey, decimal>());

    private static DraftValue ValueFor(DraftBoardResult board, PlayerId playerId) =>
        board.Rankings.Single(value => value.PlayerId == playerId);

    private static IReadOnlyList<RecommendationEvidence> Supporting(string statement) =>
        [
            new(
                EvidenceKind.Opportunity,
                EvidencePolarity.Supporting,
                statement,
                1m),
        ];
}
