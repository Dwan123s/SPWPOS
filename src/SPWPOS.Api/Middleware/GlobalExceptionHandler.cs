using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Authentication;
namespace SPWPOS.Api.Middleware;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested) return true;
        if (context.Response.HasStarted) { context.Abort(); return true; }
        var auth = exception as AuthException;
        var concurrency = exception is DbUpdateConcurrencyException;
        int status = auth?.StatusCode ?? (concurrency ? 409 : exception is BadHttpRequestException bad ? bad.StatusCode : 500);
        string code = auth?.Code ?? (concurrency ? "DATA_STALE" : status == 413 ? "REQUEST_TOO_LARGE" : status == 400 ? "VALIDATION_ERROR" : "INTERNAL_ERROR");
        string message = auth?.Message ?? (concurrency ? "Dữ liệu đã thay đổi ở nơi khác. Tải lại bản mới trước khi chỉnh sửa." : status >= 500 ? "An unexpected error occurred." : "Invalid request.");
        if (status >= 500) logger.LogError("Request failed with {FailureType}; trace {TraceId}", exception.GetType().Name, context.TraceIdentifier);
        await ApiProblems.WriteAsync(context, status, code, message, ct); return true;
    }
}
