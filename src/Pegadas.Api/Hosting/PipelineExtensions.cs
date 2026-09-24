using Hangfire;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Pegadas.Api.Endpoints;
using Pegadas.Api.Middleware;
using Pegadas.BuildingBlocks.Jobs;
using Scalar.AspNetCore;

namespace Pegadas.Api.Hosting;

internal static class PipelineExtensions
{
    /// <summary>
    /// Middleware order matters: forwarded headers first (client IP/scheme for everything else),
    /// then security headers and error handling, then the app-version gate, authentication,
    /// rate limiting (which partitions on the device claim) and authorization.
    /// HTTPS redirection is not needed: App Service enforces HTTPS-only in front of the container.
    /// </summary>
    public static WebApplication UsePegadasPipeline(this WebApplication app)
    {
        app.UseForwardedHeaders();

        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
        }

        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseResponseCompression();
        app.UseMiddleware<AppVersionMiddleware>();
        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseAuthorization();

        return app;
    }

    public static WebApplication MapPegadasEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false })
            .AllowAnonymous()
            .DisableRateLimiting();
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") })
            .AllowAnonymous()
            .DisableRateLimiting();

        var v1 = app.MapGroup("/v1");
        v1.MapMetaEndpoints();
        foreach (var module in PegadasModules.All)
        {
            module.MapEndpoints(v1);
        }

        if (app.Configuration.GetValue<bool>("OpenApi:Enabled"))
        {
            app.MapOpenApi().AllowAnonymous();
            app.MapScalarApiReference(options => options.WithTitle("Pegadas API")).AllowAnonymous();
        }

        if (app.Configuration.GetValue<bool>("Ops:HangfireDashboard"))
        {
            // Hangfire authorizes the dashboard itself (see HangfireDashboardAuthorizationFilter).
            var dashboard = new DashboardOptions
            {
                Authorization = [app.Services.GetRequiredService<HangfireDashboardAuthorizationFilter>()],
                DisplayStorageConnectionString = false,
            };
            app.MapHangfireDashboard("/ops/hangfire", dashboard).AllowAnonymous();
        }

        return app;
    }
}
