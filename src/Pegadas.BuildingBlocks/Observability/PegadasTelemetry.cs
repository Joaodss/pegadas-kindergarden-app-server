using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Pegadas.BuildingBlocks.Observability;

/// <summary>
/// The application's ActivitySource and Meter. Tags and measurements carry ids only, never
/// personal data (ADR-0009).
/// </summary>
public static class PegadasTelemetry
{
    public const string ServiceName = "pegadas-api";

    public const string SourceName = "Pegadas";

    public static readonly ActivitySource ActivitySource = new(SourceName);

    public static readonly Meter Meter = new(SourceName);

    internal static readonly Counter<long> OutboxProcessed =
        Meter.CreateCounter<long>("pegadas.outbox.processed", unit: "{message}", description: "Outbox messages processed.");

    internal static readonly Counter<long> OutboxFailed =
        Meter.CreateCounter<long>("pegadas.outbox.failed", unit: "{message}", description: "Outbox message processing failures.");
}
