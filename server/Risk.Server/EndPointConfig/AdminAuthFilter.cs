using System.Security.Cryptography;
using System.Text;

namespace Risk.Server.EndPointConfig;

/// <summary>
/// Shared-secret auth for the /admin endpoint group. The token is read from
/// configuration ("AdminToken") and supplied either as an "X-Admin-Token"
/// header or a "?key=" query parameter. Fails closed if no token is configured.
/// </summary>
public sealed class AdminAuthFilter : IEndpointFilter
{
    private readonly string? _token;

    public AdminAuthFilter(IConfiguration config) => _token = config["AdminToken"];

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        // Not configured → deny everything rather than silently allow.
        if (string.IsNullOrEmpty(_token))
            return Results.StatusCode(503);

        var req = ctx.HttpContext.Request;
        var provided = req.Headers["X-Admin-Token"].FirstOrDefault()
                       ?? req.Query["key"].FirstOrDefault();

        var ok = provided is not null && CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(provided),
            Encoding.UTF8.GetBytes(_token));

        return ok ? await next(ctx) : Results.Unauthorized();
    }
}
