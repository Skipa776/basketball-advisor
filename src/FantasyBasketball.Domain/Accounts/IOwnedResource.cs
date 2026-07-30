namespace FantasyBasketball.Domain.Accounts;

public interface IOwnedResource
{
    Guid? OwnerId { get; }
}

public interface IGlobalOrOwnedResource : IOwnedResource
{
}
