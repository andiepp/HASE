namespace Hase.Core.Domain.Data;

public sealed record Quantity
{
    public Quantity(string id, string displayName)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Quantity id must not be empty.", nameof(id));

        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Quantity display name must not be empty.", nameof(displayName));

        Id = id.Trim();
        DisplayName = displayName.Trim();
    }

    public string Id { get; }

    public string DisplayName { get; }

    public override string ToString() => DisplayName;
}