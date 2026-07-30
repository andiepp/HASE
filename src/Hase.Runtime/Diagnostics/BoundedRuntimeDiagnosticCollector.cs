namespace Hase.Runtime.Diagnostics;

/// <summary>
/// Retains the most recent structured diagnostics in process memory.
/// </summary>
public sealed class BoundedRuntimeDiagnosticCollector :
    IRuntimeDiagnosticSink
{
    private readonly int capacity;
    private readonly object gate =
        new();
    private readonly Queue<RuntimeDiagnosticRecord> records =
        new();

    public BoundedRuntimeDiagnosticCollector(
        int capacity,
        RuntimeDiagnosticLevel maximumLevel =
            RuntimeDiagnosticLevel.Operational)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capacity),
                capacity,
                "Capacity must be positive.");
        }

        if (!Enum.IsDefined(
                maximumLevel))
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumLevel),
                maximumLevel,
                "Maximum level is not defined.");
        }

        this.capacity = capacity;
        MaximumLevel = maximumLevel;
    }

    public RuntimeDiagnosticLevel MaximumLevel { get; }

    public bool IsEnabled(
        RuntimeDiagnosticLevel level)
    {
        return Enum.IsDefined(
                   level) &&
               level <= MaximumLevel;
    }

    public void Publish(
        RuntimeDiagnosticRecord record)
    {
        ArgumentNullException.ThrowIfNull(
            record);

        if (!IsEnabled(
                record.Level))
        {
            return;
        }

        lock (gate)
        {
            records.Enqueue(
                record);

            while (records.Count > capacity)
            {
                records.Dequeue();
            }
        }
    }

    public IReadOnlyList<RuntimeDiagnosticRecord> GetSnapshot(
        RuntimeDiagnosticLevel? level = null,
        RuntimeDiagnosticCategory? category = null)
    {
        lock (gate)
        {
            return records
                .Where(
                    record =>
                        (level is null ||
                         record.Level == level) &&
                        (category is null ||
                         record.Category == category))
                .OrderBy(
                    record =>
                        record.Sequence)
                .ToArray();
        }
    }

    public void Clear()
    {
        lock (gate)
        {
            records.Clear();
        }
    }
}
