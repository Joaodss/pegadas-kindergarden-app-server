using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;
using Pegadas.BuildingBlocks.Web;

namespace Pegadas.Api.Middleware;

internal sealed class AppVersionOptions
{
    public const string SectionName = "AppVersion";

    /// <summary>Oldest app version the API still serves. Older apps get 426 and must update.</summary>
    [Required]
    public string MinimumSupported { get; set; } = "1.0.0";

    /// <summary>Latest published app version (informational, for the "update available" hint).</summary>
    [Required]
    public string Latest { get; set; } = "1.0.0";

    public bool TryGetMinimumSupported(out Version version) =>
        Version.TryParse(MinimumSupported, out version!);
}

/// <summary>
/// Rejects requests from app versions below <see cref="AppVersionOptions.MinimumSupported"/>
/// with <c>426 Upgrade Required</c>. Requests without a parseable <c>X-App-Version</c> header
/// (tools, the future web portal) pass through. Health and meta endpoints are always served,
/// so an outdated app can still discover that it must update.
/// </summary>
internal sealed class AppVersionMiddleware(RequestDelegate next, IOptions<AppVersionOptions> options, IProblemDetailsService problems)
{
    public const string HeaderName = "X-App-Version";

    public const string UpgradeRequiredCode = "app_upgrade_required";

    private readonly Version _minimum = options.Value.TryGetMinimumSupported(out var minimum) ? minimum : new Version(0, 0);

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var header)
            && Version.TryParse(header.ToString(), out var appVersion)
            && appVersion < _minimum
            && !IsExempt(context.Request.Path))
        {
            context.Response.StatusCode = StatusCodes.Status426UpgradeRequired;
            await problems.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails =
                {
                    Status = StatusCodes.Status426UpgradeRequired,
                    Title = "This app version is no longer supported.",
                    Type = ProblemResults.TypeFor(UpgradeRequiredCode),
                    Extensions =
                    {
                        [ProblemResults.CodeExtension] = UpgradeRequiredCode,
                        ["minimumVersion"] = options.Value.MinimumSupported,
                    },
                },
            });
            return;
        }

        await next(context);
    }

    private static bool IsExempt(PathString path) =>
        path.StartsWithSegments("/health") || path.StartsWithSegments("/v1/meta");
}
