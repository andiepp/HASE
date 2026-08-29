namespace Hase.Core.Domain.Data;

/// <summary>
/// Represents one scalar value together with the unit it is expressed in.
/// </summary>
/// <remarks>
/// This is a measured or declared coordinate, not a data descriptor. It
/// describes a single concrete value, where <see cref="NumericDataDescriptor"/>
/// describes the shape every value of a Property takes.
/// </remarks>
public sealed record QuantityValue
{
    public QuantityValue(double value, Unit unit)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "A quantity value must be finite.");
        }

        Value = value;
        Unit = unit ?? throw new ArgumentNullException(nameof(unit));
    }

    public double Value { get; }

    public Unit Unit { get; }

    public override string ToString() => $"{Value} {Unit.Symbol}";
}
