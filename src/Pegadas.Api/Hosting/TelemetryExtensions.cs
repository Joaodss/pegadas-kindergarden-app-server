using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.Extensions.Compliance.Redaction;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Pegadas.BuildingBlocks.Observability;

namespace Pegadas.Api.Hosting;

internal static class TelemetryExtensions
{
    /// <summary>
    /// OpenTelemetry traces, metrics and logs (ADR-0009). Exports to Azure Monitor when
    /// <c>APPLICATIONINSIGHTS_CONNECTION_STRING</c> is set, otherwise to OTLP when
    /// <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is set (the Aspire dashboard locally).
    /// Log parameters classified as personal or sensitive data are erased.
    /// </summary>
    public static WebApplicationBuilder AddPegadasTelemetry(this WebApplicationBuilder builder)
    {
        builder.Logging.EnableRedaction();
        builder.Services.AddRedaction(redaction =>
            redaction.SetRedactor<ErasingRedactor>(PegadasTaxonomy.PersonalData, PegadasTaxonomy.SensitiveData));

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        var telemetry = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(
                PegadasTelemetry.ServiceName,
                serviceVersion: typeof(TelemetryExtensions).Assembly.GetName().Version?.ToString()))
            .WithTracing(tracing => tracing
                .AddSource(PegadasTelemetry.SourceName)
                .AddAspNetCoreInstrumentation(options =>
                    options.Filter = context => !context.Request.Path.StartsWithSegments("/health"))
                .AddHttpClientInstrumentation()
                .AddNpgsql()
                .AddProcessor(new DropUnparentedDatabaseSpansProcessor()))
            .WithMetrics(metrics => metrics
                .AddMeter(PegadasTelemetry.SourceName)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter("Npgsql"));

        if (!string.IsNullOrWhiteSpace(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        {
            telemetry.UseAzureMonitor();
        }
        else if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            telemetry.UseOtlpExporter();
        }

        return builder;
    }
}
