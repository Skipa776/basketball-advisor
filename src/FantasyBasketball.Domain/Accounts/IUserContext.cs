namespace FantasyBasketball.Domain.Accounts;

public interface IUserContext
{
    Guid CurrentUserId { get; }
}
