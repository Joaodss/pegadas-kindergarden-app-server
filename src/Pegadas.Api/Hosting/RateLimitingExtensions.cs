using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Pegadas.BuildingBlocks.Security;
using Pegadas.BuildingBlocks.Web;

namespace Pegadas.Api.Hosting;

internal sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Requests per minute per client IP, across the whole API.</summary>
    [Range(1, 100_000)]
    public int GlobalPerMinute { get; set; } = 600;

    /// <summary>Requests per minute per client IP on authentication endpoints.</summary>
    [Range(1, 1_000)]
    public int AuthPerMinute { get; set; } = 10;

    /// <summary>Burst size of the per-device token bucket.</summary>
    [Range(1, 10_000)]
    public int DeviceBurst { get; set; } = 60;

    /// <summary>Tokens added to the per-device bucket every 10 seconds.</summary>
    [Range(1, 10_000)]
    public int DevicePer10Seconds { get; set; } = 30;
}

internal static class RateLimitingExtensions
{
    /// <summary>
    /// Built-in rate limiting. The global limiter is per client IP; named policies are applied
    /// by endpoints (<see cref="RateLimitPolicies"/>). Runs after authentication so device
    /// policies can partition on the <c>device_id</c> claim.
    /// </summary>
    public static WebApplicationBuilder AddPegadasRateLimiting(this WebApplicationBuilder builder)
    {
        var options = builder.Configuration.GetSection(RateLimitingOptions.SectionName).Get<RateLimitingOptions>()
            ?? new RateLimitingOptions();
        Validator.ValidateObject(options, new ValidationContext(options), validateAllProperties: true);

        builder.Services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.OnRejected = WriteRejectionAsync;

            limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(ClientIp(context), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = options.GlobalPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));

            limiter.AddPolicy(RateLimitPolicies.Auth, context =>
                RateLimitPartition.GetFixedWindowLimiter(ClientIp(context), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = options.AuthPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));

            limiter.AddPolicy(RateLimitPolicies.Device, context =>
                RateLimitPartition.GetTokenBucketLimiter(DeviceOrIp(context), _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = options.DeviceBurst,
                    TokensPerPeriod = options.DevicePer10Seconds,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(10),
                    QueueLimit = 0,
                }));

            limiter.AddPolicy(RateLimitPolicies.Sync, context =>
                RateLimitPartition.GetConcurrencyLimiter(DeviceOrIp(context), _ => new ConcurrencyLimiterOptions
                {
                    PermitLimit = 1,
                    QueueLimit = 1,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                }));
        });

        return builder;
    }

    private static string ClientIp(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static string DeviceOrIp(HttpContext context) =>
        context.User.FindFirst(PegadasClaimTypes.DeviceId)?.Value is { Length: > 0 } deviceId
            ? $"device:{deviceId}"
            : $"ip:{ClientIp(context)}";

    private static async ValueTask WriteRejectionAsync(OnRejectedContext rejected, CancellationToken cancellationToken)
    {
        var context = rejected.HttpContext;
        if (rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        var problems = context.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problems.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails =
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Too many requests.",
                Type = ProblemResults.TypeFor("rate_limited"),
                Extensions = { [ProblemResults.CodeExtension] = "rate_limited" },
            },
        });
    }
}
