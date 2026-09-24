using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Pegadas.Api.Middleware;

namespace Pegadas.Api.Endpoints;

/// <param name="MinimumVersion">Apps below this version must update (the API answers them with 426).</param>
/// <param name="LatestVersion">Latest published app version.</param>
internal sealed record MinVersionResponse(string MinimumVersion, string LatestVersion);

internal static class MetaEndpoints
{
    public static IEndpointRouteBuilder MapMetaEndpoints(this IEndpointRouteBuilder v1)
    {
        var meta = v1.MapGroup("/meta")
            .WithTags("Meta")
            .AllowAnonymous();

        meta.MapGet("/min-version", (IOptions<AppVersionOptions> options, HttpContext context) =>
            {
                context.Response.Headers.CacheControl = "public, max-age=300";
                return TypedResults.Ok(new MinVersionResponse(options.Value.MinimumSupported, options.Value.Latest));
            })
            .WithName("GetMinVersion")
            .WithSummary("Minimum supported and latest app versions")
            .WithDescription("Called by the app at start-up to decide whether an update is mandatory.");

        return v1;
    }
}

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(MinVersionResponse))]
internal sealed partial class ApiJsonContext : JsonSerializerContext;
