namespace Hase.Core.Domain.Data;

public sealed record ValueRange(double Minimum, double Maximum)
{
    public bool Contains(double value) =>
        value >= Minimum && value <= Maximum;
}