using System.Collections.ObjectModel;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Recommendations;

namespace FantasyBasketball.Domain.Draft;

public sealed class DraftWeightOptions
{
    public const string SectionName = "DraftWeights";

    public decimal Scarcity { get; init; } = 1m;

    public decimal Fit { get; init; } = 1m;

    public decimal Market { get; init; } = 0.5m;

    public decimal Risk { get; init; } = 1m;

    public decimal Urgency { get; init; } = 0.5m;

    public bool IsValid() =>
        Scarcity >= 0m && Fit >= 0m && Market >= 0m && Risk >= 0m && Urgency >= 0m;
}

public sealed record DraftValue
{
    public DraftValue(
        PlayerId playerId,
        decimal total,
        decimal projectedSeasonValue,
        decimal valueAboveReplacement,
        decimal positionalScarcity,
        decimal rosterFit,
        decimal marketValue,
        decimal contextAdjustment,
        decimal injuryRisk,
        decimal roleRisk,
        IReadOnlyList<RecommendationEvidence> evidence,
        decimal urgency = 0m,
        decimal? availableAtNextPick = null)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (evidence.Count == 0)
        {
            throw new ArgumentException(
                "Draft value requires at least one evidence item.",
                nameof(evidence));
        }

        PlayerId = playerId;
        Total = total;
        ProjectedSeasonValue = projectedSeasonValue;
        ValueAboveReplacement = valueAboveReplacement;
        PositionalScarcity = positionalScarcity;
        RosterFit = rosterFit;
        MarketValue = marketValue;
        ContextAdjustment = contextAdjustment;
        InjuryRisk = injuryRisk;
        RoleRisk = roleRisk;
        Urgency = urgency;
        AvailableAtNextPick = availableAtNextPick;
        Evidence = new ReadOnlyCollection<RecommendationEvidence>(
            EvidenceOrderer.Order(evidence).ToArray());
    }

    public PlayerId PlayerId { get; }

    public decimal Total { get; }

    public decimal ProjectedSeasonValue { get; }

    public decimal ValueAboveReplacement { get; }

    public decimal PositionalScarcity { get; }

    public decimal RosterFit { get; }

    public decimal MarketValue { get; }

    public decimal ContextAdjustment { get; }

    public decimal InjuryRisk { get; }

    public decimal RoleRisk { get; }

    public decimal Urgency { get; }

    public decimal? AvailableAtNextPick { get; }

    public IReadOnlyList<RecommendationEvidence> Evidence { get; }
}

public sealed class DraftValueCalculator(DraftWeightOptions options)
{
    public DraftValue Calculate(
        PlayerId playerId,
        decimal projectedSeasonValue,
        decimal valueAboveReplacement,
        decimal positionalScarcity,
        decimal rosterFit,
        decimal marketValue,
        decimal contextAdjustment,
        decimal injuryRisk,
        decimal roleRisk,
        IReadOnlyList<RecommendationEvidence> evidence,
        decimal urgency = 0m,
        decimal? availableAtNextPick = null)
    {
        if (!options.IsValid())
        {
            throw new ArgumentException("Draft weights are invalid.", nameof(options));
        }

        var riskPenalty = projectedSeasonValue
            * Math.Clamp((roleRisk + injuryRisk) / 2m, 0m, 1m);
        var total = valueAboveReplacement
            + (options.Scarcity * positionalScarcity)
            + (options.Fit * rosterFit)
            + (options.Market * marketValue)
            - (options.Risk * riskPenalty)
            + (options.Urgency * urgency);
        return new DraftValue(
            playerId,
            total,
            projectedSeasonValue,
            valueAboveReplacement,
            positionalScarcity,
            rosterFit,
            marketValue,
            contextAdjustment,
            injuryRisk,
            roleRisk,
            evidence,
            urgency,
            availableAtNextPick);
    }
}
