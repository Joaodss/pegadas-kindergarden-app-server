using System.IO.Compression;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using Pegadas.Api.Endpoints;
using Pegadas.Api.Errors;
using Pegadas.Api.Middleware;
using Pegadas.Api.OpenApi;

namespace Pegadas.Api.Hosting;

internal static class ApiServiceExtensions
{
    /// <summary>Largest accepted request body. Sized for a full sync batch (500 changes).</summary>
    public const long MaxRequestBodyBytes = 1024 * 1024;

    public static WebApplicationBuilder AddPegadasApi(this WebApplicationBuilder builder)
    {
        var services = builder.Services;
        var configuration = builder.Configuration;

        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            kestrel.AddServerHeader = false;
            kestrel.Limits.MaxRequestBodySize = MaxRequestBodyBytes;
        });

        // App Service terminates TLS and forwards the client address and scheme.
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        services.AddProblemDetails();
        services.AddExceptionHandler<ExceptionProblemHandler>();

        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, ApiJsonContext.Default);
        });
        services.AddValidation();

        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
            options.MimeTypes = ["application/json", "application/problem+json"];
        });
        services.Configure<BrotliCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
        services.Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);

        services.AddOptions<AppVersionOptions>()
            .Bind(configuration.GetSection(AppVersionOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(o => o.TryGetMinimumSupported(out _), "AppVersion:MinimumSupported must be a version such as 1.2.0.")
            .ValidateOnStart();

        services.AddPegadasOpenApi();
        builder.AddPegadasAuth();
        builder.AddPegadasRateLimiting();

        return builder;
    }
}
