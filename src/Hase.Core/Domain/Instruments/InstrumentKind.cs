namespace Hase.Core.Domain.Instruments;

public sealed record InstrumentKind
{
    public InstrumentKind(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Instrument kind must not be empty.", nameof(name));

        Name = name.Trim();
    }

    public string Name { get; }

    public override string ToString() => Name;
}