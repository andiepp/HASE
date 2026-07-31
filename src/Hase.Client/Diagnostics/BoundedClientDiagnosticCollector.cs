namespace Hase.Client.Diagnostics;

/// <summary>
/// Retains the newest client diagnostics in process memory.
/// </summary>
public sealed class BoundedClientDiagnosticCollector : IClientDiagnosticSink
{
    private readonly int capacity;
    private readonly object gate = new();
    private readonly SortedDictionary<long, ClientDiagnosticRecord> records = new();
    private long evictedRecordCount;

    public BoundedClientDiagnosticCollector(
        int capacity,
        ClientDiagnosticLevel maximumLevel = ClientDiagnosticLevel.Operational)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capacity), capacity, "Capacity must be positive.");
        }

        if (!Enum.IsDefined(maximumLevel))
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumLevel), maximumLevel, "Maximum level is not defined.");
        }

        this.capacity = capacity;
        MaximumLevel = maximumLevel;
    }

    public ClientDiagnosticLevel MaximumLevel { get; }

    public bool IsEnabled(ClientDiagnosticLevel level) =>
        Enum.IsDefined(level) && level <= MaximumLevel;

    public void Publish(ClientDiagnosticRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (!IsEnabled(record.Level))
        {
            return;
        }

        lock (gate)
        {
            records[record.Sequence] = record;

            while (records.Count > capacity)
            {
                records.Remove(records.Keys.First());
                evictedRecordCount++;
            }
        }
    }

    public ClientDiagnosticSnapshot GetSnapshot(
        ClientDiagnosticLevel? level = null,
        ClientDiagnosticCategory? category = null)
    {
        ValidateNullableEnum(level, nameof(level));
        ValidateNullableEnum(category, nameof(category));

        lock (gate)
        {
            ClientDiagnosticRecord[] retained = records.Values
                .Where(record =>
                    (level is null || record.Level == level) &&
                    (category is null || record.Category == category))
                .ToArray();

            return new ClientDiagnosticSnapshot(
                Array.AsReadOnly(retained),
                evictedRecordCount);
        }
    }

    public void Clear()
    {
        lock (gate)
        {
            records.Clear();
            evictedRecordCount = 0;
        }
    }

    private static void ValidateNullableEnum<TEnum>(
        TEnum? value,
        string parameterName)
        where TEnum : struct, Enum
    {
        if (value is not null && !Enum.IsDefined(value.Value))
        {
            throw new ArgumentOutOfRangeException(
                parameterName, value, "Value is not defined.");
        }
    }
}
