using System.Diagnostics;
using OpenTelemetry;

namespace Pegadas.Api.Hosting;

/// <summary>
/// Stops exporting database spans that have no parent: the polling queries of Hangfire and
/// of the outbox processors (every few seconds, forever). Those spans carry no request
/// context and would dominate telemetry volume and cost. Database spans inside a request or
/// an outbox handler activity keep their parent and are exported as usual.
/// </summary>
internal sealed class DropUnparentedDatabaseSpansProcessor : BaseProcessor<Activity>
{
    private const string NpgsqlSource = "Npgsql";

    public override void OnStart(Activity data)
    {
        if (data.Source.Name == NpgsqlSource && data.ParentSpanId == default)
        {
            data.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
            data.IsAllDataRequested = false;
        }
    }
}
