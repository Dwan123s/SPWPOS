using Microsoft.AspNetCore.Mvc;
namespace SPWPOS.Api.Middleware;

public static class ApiProblems
{
    public static ProblemDetails Create(HttpContext context, int status, string code, string message)
    {
        var result = new ProblemDetails
        {
            Status = status,
            Title = message,
            Type = $"https://httpstatuses.com/{status}",
            Instance = context.Request.Path
        };
        result.Extensions["success"] = false;
        result.Extensions["error"] = new { code, message };
        result.Extensions["traceId"] = context.TraceIdentifier;
        return result;
    }
    public static async Task WriteAsync(HttpContext context, int status, string code, string message, CancellationToken ct)
    {
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(Create(context, status, code, message), options: null, contentType: "application/problem+json", cancellationToken: ct);
    }
}
