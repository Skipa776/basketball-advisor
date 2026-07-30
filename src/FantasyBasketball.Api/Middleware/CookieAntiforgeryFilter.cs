using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;

namespace FantasyBasketball.Api.Middleware;

public sealed class CookieAntiforgeryFilter(IAntiforgery antiforgery)
    : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var request = context.HttpContext.Request;
        if (!HttpMethods.IsGet(request.Method)
            && !HttpMethods.IsHead(request.Method)
            && !HttpMethods.IsOptions(request.Method)
            && !HttpMethods.IsTrace(request.Method)
            && (context.HttpContext.User.Identity?.IsAuthenticated is not true
                || context.HttpContext.User.Identity.AuthenticationType
                    == IdentityConstants.ApplicationScheme))
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }

        return await next(context);
    }
}
