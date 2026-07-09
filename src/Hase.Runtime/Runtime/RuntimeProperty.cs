using Hase.Core.Domain.Properties;
using System.Diagnostics.Metrics;

namespace Hase.Runtime.Runtime;

/// <summary>
/// Runtime representation of a property.
/// It references the immutable property descriptor and holds the latest value.
/// </summary>
public sealed class RuntimeProperty
{
    public RuntimeProperty(RuntimeInstrument instrument, PropertyDescriptor descriptor)
    {
        Instrument = instrument ?? throw new ArgumentNullException(nameof(instrument));
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
    }

    /// <summary>
    /// Static descriptor of this property.
    /// </summary>
    public PropertyDescriptor Descriptor { get; }

    public RuntimeInstrument Instrument { get; }

    /// <summary>
    /// Latest known value of the property.
    /// Null until the first value has been received.
    /// </summary>
    public PropertyValue? CurrentValue { get; private set; }

    /// <summary>
    /// Updates the current value.
    /// </summary>
    public void UpdateValue(PropertyValue value)
    {
        ArgumentNullException.ThrowIfNull(value);

        CurrentValue = value;
    }
}