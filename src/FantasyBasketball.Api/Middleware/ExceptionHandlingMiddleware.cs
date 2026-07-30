using FantasyBasketball.Application.Common;
using FantasyBasketball.Infrastructure.Identity;
using Microsoft.AspNetCore.Antiforgery;

namespace FantasyBasketball.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            logger.LogInformation(
                "Request {TraceId} was canceled by the caller",
                context.TraceIdentifier);
        }
        catch (Exception exception)
        {
            await WriteFailureAsync(context, exception);
        }
    }

    private async Task WriteFailureAsync(
        HttpContext context,
        Exception exception)
    {
        var (status, code, message, fields) = exception switch
        {
            RequestValidationException validation => (
                StatusCodes.Status400BadRequest,
                "validation_failed",
                validation.Message,
                validation.Fields),
            InvalidAccountArchiveException archive => (
                StatusCodes.Status400BadRequest,
                "validation_failed",
                archive.Message,
                new Dictionary<string, string[]>
                {
                    ["archive"] = [archive.Message],
                }),
            AntiforgeryValidationException => (
                StatusCodes.Status400BadRequest,
                "validation_failed",
                "The anti-forgery token is invalid or missing.",
                EmptyFields()),
            ResourceNotFoundException => (
                StatusCodes.Status404NotFound,
                "not_found",
                exception.Message,
                EmptyFields()),
            ResourceConflictException => (
                StatusCodes.Status409Conflict,
                "conflict",
                exception.Message,
                EmptyFields()),
            SourceUnavailableException => (
                StatusCodes.Status503ServiceUnavailable,
                "source_unavailable",
                exception.Message,
                EmptyFields()),
            BadHttpRequestException => (
                StatusCodes.Status400BadRequest,
                "validation_failed",
                "The request body or parameters are invalid.",
                EmptyFields()),
            ArgumentException argument => (
                StatusCodes.Status400BadRequest,
                "validation_failed",
                "One or more fields are invalid.",
                new Dictionary<string, string[]>
                {
                    [argument.ParamName ?? "request"] = [argument.Message],
                }),
            _ => (
                StatusCodes.Status500InternalServerError,
                "internal_error",
                "An unexpected error occurred.",
                EmptyFields()),
        };

        if (status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(
                "Unhandled {ExceptionType} for request {TraceId}",
                exception.GetType().Name,
                context.TraceIdentifier);
        }
        else
        {
            logger.LogWarning(
                "Request {TraceId} failed with {ErrorCode}",
                context.TraceIdentifier,
                code);
        }

        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(
            new ApiEnvelope<object?>(
                false,
                null,
                new ApiError(code, message, fields),
                null),
            context.RequestAborted);
    }

    private static IReadOnlyDictionary<string, string[]> EmptyFields() =>
        new Dictionary<string, string[]>();
}
