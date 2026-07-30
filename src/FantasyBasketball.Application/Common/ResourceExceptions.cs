namespace FantasyBasketball.Application.Common;

public sealed class ResourceNotFoundException(string message) : Exception(message);

public sealed class ResourceConflictException(string message) : Exception(message);

public sealed class SourceUnavailableException(string message) : Exception(message);
