namespace Hase.Core.Domain.Data;

public sealed record Unit
{
    public Unit(string id, string displayName, string symbol, Quantity quantity)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Unit id must not be empty.", nameof(id));

        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Unit display name must not be empty.", nameof(displayName));

        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Unit symbol must not be empty.", nameof(symbol));

        Id = id.Trim();
        DisplayName = displayName.Trim();
        Symbol = symbol.Trim();
        Quantity = quantity ?? throw new ArgumentNullException(nameof(quantity));
    }

    public string Id { get; }

    public string DisplayName { get; }

    public string Symbol { get; }

    public Quantity Quantity { get; }

    public override string ToString() => Symbol;
}