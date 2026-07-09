namespace Hase.Core.Domain.Data;

public sealed record NumericDataDescriptor : DataDescriptor
{
    public NumericDataDescriptor(
        Quantity quantity,
        Unit nativeUnit,
        ValueRange? range = null,
        Resolution? resolution = null)
    {
        Quantity = quantity ?? throw new ArgumentNullException(nameof(quantity));
        NativeUnit = nativeUnit ?? throw new ArgumentNullException(nameof(nativeUnit));
        Range = range;
        Resolution = resolution;
    }

    public Quantity Quantity { get; }

    public Unit NativeUnit { get; }

    public ValueRange? Range { get; }

    public Resolution? Resolution { get; }
}
