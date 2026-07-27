namespace Hase.Client;

/// <summary>
/// Represents one normalized remote client failure without exposing a
/// transport-specific exception type to application code.
/// </summary>
public sealed class RuntimeHostClientException
    : Exception
{
    public RuntimeHostClientException(
        RuntimeHostClientFailureCategory category,
        string message,
        Exception? innerException = null)
        : base(
            RequireMessage(
                message),
            innerException)
    {
        if (!Enum.IsDefined(
                category)
            || category == RuntimeHostClientFailureCategory.Unspecified)
        {
            throw new ArgumentOutOfRangeException(
                nameof(category),
                category,
                "A specified runtime-host client failure category is "
                + "required.");
        }

        Category =
            category;
    }

    public RuntimeHostClientFailureCategory Category
    {
        get;
    }

    private static string RequireMessage(
        string message)
    {
        if (string.IsNullOrWhiteSpace(
                message))
        {
            throw new ArgumentException(
                "The runtime-host client failure message must not be empty.",
                nameof(message));
        }

        return message.Trim();
    }
}
