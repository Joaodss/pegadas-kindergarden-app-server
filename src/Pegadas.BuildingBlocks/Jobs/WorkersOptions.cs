using System.ComponentModel.DataAnnotations;

namespace Pegadas.BuildingBlocks.Jobs;

/// <summary>
/// Background processing switch. The same image runs as API + workers (<c>Enabled=true</c>,
/// the default), or as separate API (<c>false</c>) and worker (<c>true</c>) processes.
/// </summary>
public sealed class WorkersOptions
{
    public const string SectionName = "Workers";

    /// <summary>Runs the Hangfire server and the outbox processors in this process.</summary>
    public bool Enabled { get; set; } = true;

    [Range(1, 20)]
    public int WorkerCount { get; set; } = 2;

    /// <summary>Lets Hangfire create/upgrade its <c>hangfire</c> schema at startup (ADR-0008).</summary>
    public bool PrepareSchema { get; set; } = true;
}

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    [Range(typeof(TimeSpan), "00:00:00.100", "00:05:00")]
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(2);

    [Range(1, 1000)]
    public int BatchSize { get; set; } = 50;

    [Range(1, 100)]
    public int MaxAttempts { get; set; } = 10;
}
