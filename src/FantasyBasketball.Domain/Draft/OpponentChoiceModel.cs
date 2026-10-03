using System.Text.Json;

namespace FantasyBasketball.Domain.Draft;

/// <summary>Fitted by tools/modeling/choice_model.py on public draft logs (model_params_contract).</summary>
public sealed record OpponentChoiceParameters(IReadOnlyList<int> RoundGroups, IReadOnlyList<decimal> Lambdas, decimal Eta, int Candidates)
{
    public const string ModelName = "opponent-choice";

    public static OpponentChoiceParameters Parse(string json)
    {
        var parameters = JsonSerializer.Deserialize<OpponentChoiceParameters>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new ArgumentException("Opponent choice parameters are empty.", nameof(json));
        return parameters.RoundGroups.Count > 0 && parameters.RoundGroups.Count == parameters.Lambdas.Count
            && parameters.RoundGroups[0] == 1 && parameters.Candidates > 0
            ? parameters
            : throw new ArgumentException("Opponent choice parameters need one lambda per round group, starting at round 1.", nameof(json));
    }
}

/// <summary>
/// Plackett–Luce pick model: a drafter takes candidate j with probability softmax(u), where
/// u_j = −λ_round · log(ADP_j) + η · (j fills an open starting slot).
/// </summary>
public sealed class OpponentChoiceModel(OpponentChoiceParameters parameters)
{
    public int Candidates => parameters.Candidates;

    public decimal[] Probabilities(int round, IReadOnlyList<decimal> adp, IReadOnlyList<bool> fillsOpenSlot)
    {
        ArgumentNullException.ThrowIfNull(adp);
        ArgumentNullException.ThrowIfNull(fillsOpenSlot);
        if (adp.Count == 0 || adp.Count != fillsOpenSlot.Count || adp.Any(value => value <= 0m))
        {
            throw new ArgumentException("Each candidate needs a positive ADP and a need flag.", nameof(adp));
        }

        var group = parameters.RoundGroups.Select((start, index) => (start, index)).Last(pair => round >= pair.start).index;
        var lambda = (double)parameters.Lambdas[group];
        var eta = (double)parameters.Eta;
        var utility = adp.Select((value, index) => (-lambda * Math.Log((double)value)) + (fillsOpenSlot[index] ? eta : 0)).ToArray();
        var max = utility.Max();
        var weights = utility.Select(value => Math.Exp(value - max)).ToArray();
        var total = weights.Sum();
        return weights.Select(weight => (decimal)(weight / total)).ToArray();
    }

    /// <summary>The candidate a uniform draw in [0, 1) lands on, by inverse CDF.</summary>
    public int Choose(int round, IReadOnlyList<decimal> adp, IReadOnlyList<bool> fillsOpenSlot, decimal uniform)
    {
        var probabilities = Probabilities(round, adp, fillsOpenSlot);
        var running = 0m;
        for (var index = 0; index < probabilities.Length; index++)
        {
            running += probabilities[index];
            if (uniform < running)
            {
                return index;
            }
        }

        return probabilities.Length - 1;
    }
}
