using Hase.Core.Domain.Properties;

namespace Hase.Runtime.Runtime;

/// <summary>
/// Describes a property value change.
/// </summary>
public sealed record PropertyValueChanged(
    RuntimeProperty Property,
    PropertyValue? PreviousValue,
    PropertyValue CurrentValue);