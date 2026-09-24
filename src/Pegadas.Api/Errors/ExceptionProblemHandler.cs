using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Pegadas.BuildingBlocks.Web;

namespace Pegadas.Api.Errors;

/// <summary>
/// Turns unhandled exceptions into RFC 9457 problem responses with a stable <c>code</c>.
/// Internal details (messages, stack traces) never reach the client; the <c>traceId</c>
/// extension links the response to the server-side logs.
/// </summary>
internal sealed partial class ExceptionProblemHandler(IProblemDetailsService problems, ILogger<ExceptionProblemHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, code, title) = exception switch
        {
            DbUpdateConcurrencyException =>
                (StatusCodes.Status409Conflict, "concurrency_conflict", "The resource was changed by another request."),
            BadHttpRequestException badRequest =>
                (badRequest.StatusCode, "bad_request", "The request could not be read."),
            _ =>
                (StatusCodes.Status500InternalServerError, "unexpected_error", "An unexpected error occurred."),
        };

        if (status >= StatusCodes.Status500InternalServerError)
        {
            LogUnhandled(logger, httpContext.Request.Method, httpContext.GetEndpoint()?.DisplayName, exception);
        }

        httpContext.Response.StatusCode = status;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Status = status,
                Title = title,
                Type = ProblemResults.TypeFor(code),
                Extensions = { [ProblemResults.CodeExtension] = code },
            },
        });
    }

    // Route template only (never the raw path): paths contain ids but query strings could carry more.
    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception on {Method} {Endpoint}")]
    private static partial void LogUnhandled(ILogger logger, string method, string? endpoint, Exception exception);
}
