namespace Hase.Core.Domain.Identity;

/// <summary>
/// Represents a stable, non-empty identifier used by HASE domain objects.
/// </summary>
public abstract record HaseId
{
    protected HaseId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "An identifier must not be null, empty, or whitespace.",
                nameof(value));
        }

        Value = value.Trim();
    }

    /// <summary>
    /// Gets the textual identifier value.
    /// </summary>
    public string Value { get; }

    public sealed override string ToString() => Value;
}