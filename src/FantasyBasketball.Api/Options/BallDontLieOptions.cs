using System.ComponentModel.DataAnnotations;

namespace FantasyBasketball.Api.Options;

public sealed class BallDontLieOptions
{
    public const string SectionName = "BallDontLie";

    [Required]
    public string ApiKey { get; init; } = string.Empty;
}
