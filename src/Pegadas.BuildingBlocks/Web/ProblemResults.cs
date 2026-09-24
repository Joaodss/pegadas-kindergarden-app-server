using Microsoft.AspNetCore.Http;
using Pegadas.SharedKernel.Results;

namespace Pegadas.BuildingBlocks.Web;

/// <summary>Maps domain <see cref="Error"/>s to RFC 9457 problem responses with a stable <c>code</c> extension.</summary>
public static class ProblemResults
{
    public const string CodeExtension = "code";

    /// <summary>Problem <c>type</c> URI for an error code.</summary>
    public static string TypeFor(string code) => $"https://pegadas.pt/problems/{code}";

    public static IResult ToProblem(this Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var extensions = new Dictionary<string, object?> { [CodeExtension] = error.Code };

        if (error.Type == ErrorType.Validation && error.ValidationErrors is { Count: > 0 } errors)
        {
            return TypedResults.ValidationProblem(
                errors,
                detail: error.Message,
                type: TypeFor(error.Code),
                extensions: extensions);
        }

        return TypedResults.Problem(
            detail: error.Message,
            statusCode: StatusCodeFor(error.Type),
            type: TypeFor(error.Code),
            extensions: extensions);
    }

    public static int StatusCodeFor(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.TooManyRequests => StatusCodes.Status429TooManyRequests,
        _ => StatusCodes.Status500InternalServerError,
    };
}

/// <summary>Rate-limiting policy names, applied with <c>RequireRateLimiting(...)</c>.</summary>
public static class RateLimitPolicies
{
    /// <summary>Strict, per client IP: enrolment, PIN, refresh.</summary>
    public const string Auth = "auth";

    /// <summary>Token bucket per enrolled device.</summary>
    public const string Device = "device";

    /// <summary>One concurrent sync per device.</summary>
    public const string Sync = "sync";
}
