using API.Filters;
using Entities.DTOs.Common;

namespace API.Middlewares;

public sealed class RestrictedSessionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true && context.User.FindFirst("access_scope")?.Value == "appeals"
            && context.GetEndpoint()?.Metadata.GetMetadata<AllowRestrictedSessionAttribute>() == null)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(ApiResponse.ErrorResponse("Tài khoản chỉ được xem quyết định và gửi khiếu nại.", "account_restricted"));
            return;
        }
        await next(context);
    }
}
