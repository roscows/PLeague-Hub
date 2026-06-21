using System.Security.Claims;
using PLeagueHub.Api.Services;

namespace PLeagueHub.Api.Middleware;

public sealed class ActiveSuspensionMiddleware
{
    private readonly RequestDelegate _next;

    public ActiveSuspensionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IModerationService moderationService)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            var access = await moderationService.CheckLoginByUserIdAsync(userId, context.RequestAborted);
            if (!access.Allowed && access.State?.Tip == "suspenzija")
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new
                {
                    message = access.Message,
                    tip = access.State.Tip,
                    razlog = access.State.Razlog,
                    isticeAt = access.State.IsticeAt
                }, context.RequestAborted);
                return;
            }
        }

        await _next(context);
    }
}
