namespace Hase.Runtime.Runtime;

/// <summary>
/// Root object of a HASE runtime instance.
/// It maintains the live engineering model for one application.
/// </summary>
public sealed class RuntimeContext
{
    private readonly List<RuntimeInstrument> _instruments = [];

    /// <summary>
    /// Gets the runtime instruments currently known to this context.
    /// </summary>
    public IReadOnlyList<RuntimeInstrument> Instruments => _instruments;

    /// <summary>
    /// Adds a runtime instrument to this context.
    /// </summary>
    public void AddInstrument(RuntimeInstrument instrument)
    {
        ArgumentNullException.ThrowIfNull(instrument);

        if (_instruments.Any(existing =>
                existing.Descriptor.Id == instrument.Descriptor.Id))
        {
            throw new InvalidOperationException(
                $"An instrument with id '{instrument.Descriptor.Id}' already exists in this runtime context.");
        }

        _instruments.Add(instrument);
    }

    /// <summary>
    /// Removes a runtime instrument from this context.
    /// </summary>
    public bool RemoveInstrument(RuntimeInstrument instrument)
    {
        ArgumentNullException.ThrowIfNull(instrument);

        return _instruments.Remove(instrument);
    }
}