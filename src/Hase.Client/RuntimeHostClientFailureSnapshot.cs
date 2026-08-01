namespace Hase.Client;

/// <summary>
/// Represents one immutable transport-independent client-session failure.
/// </summary>
public sealed record RuntimeHostClientFailureSnapshot
{
    public RuntimeHostClientFailureSnapshot(
        RuntimeHostClientFailureCategory category,
        string message)
    {
        if (!Enum.IsDefined(category)
            || category == RuntimeHostClientFailureCategory.Unspecified)
        {
            throw new ArgumentOutOfRangeException(
                nameof(category),
                category,
                "A specified runtime-host client failure category is required.");
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException(
                "The runtime-host client failure message must not be empty.",
                nameof(message));
        }

        Category = category;
        Message = message.Trim();
    }

    public RuntimeHostClientFailureCategory Category
    {
        get;
    }

    public string Message
    {
        get;
    }
}
