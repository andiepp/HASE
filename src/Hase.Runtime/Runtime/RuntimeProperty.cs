using Hase.Core.Domain.Properties;

namespace Hase.Runtime.Runtime;

/// <summary>
/// Runtime representation of a property.
/// It references the immutable property descriptor and holds the latest value.
/// </summary>
public sealed class RuntimeProperty
{
    public RuntimeProperty(PropertyDescriptor descriptor)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
    }

    /// <summary>
    /// Static descriptor of this property.
    /// </summary>
    public PropertyDescriptor Descriptor { get; }

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