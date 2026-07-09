namespace Hase.Core.Domain.Data;

public sealed record Resolution
{
    public Resolution(double value)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(nameof(value), "Resolution must be greater than zero.");

        Value = value;
    }

    public double Value { get; }

    public override string ToString() => Value.ToString();
}
