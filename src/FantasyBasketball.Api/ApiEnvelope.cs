namespace FantasyBasketball.Api;

public sealed record ApiError(
    string Code,
    string Message,
    IReadOnlyDictionary<string, string[]> Fields);

public sealed record ApiMeta(int Total, int Page, int Limit);

public sealed record ApiEnvelope<T>(
    bool Success,
    T? Data,
    ApiError? Error,
    ApiMeta? Meta);

public sealed class RequestValidationException(
    IReadOnlyDictionary<string, string[]> fields)
    : Exception("One or more fields are invalid.")
{
    public IReadOnlyDictionary<string, string[]> Fields { get; } = fields;
}

public static class ApiResults
{
    public static IResult Success<T>(
        T data,
        int statusCode = StatusCodes.Status200OK,
        ApiMeta? meta = null) =>
        Results.Json(
            new ApiEnvelope<T>(true, data, null, meta),
            statusCode: statusCode);

    public static IResult Failure(
        int statusCode,
        string code,
        string message,
        IReadOnlyDictionary<string, string[]>? fields = null) =>
        Results.Json(
            new ApiEnvelope<object?>(
                false,
                null,
                new ApiError(
                    code,
                    message,
                    fields ?? new Dictionary<string, string[]>()),
                null),
            statusCode: statusCode);
}
