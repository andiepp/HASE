namespace Hase.Client.Wpf.AppHost.Media;

/// <summary>
/// Signals a Client media presentation-boundary failure with a normalized,
/// sanitized failure category suitable for diagnostics.
/// </summary>
public sealed class ClientMediaPresentationException : InvalidOperationException
{
    public ClientMediaPresentationException(
        string failureCategory,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(failureCategory);
        FailureCategory = failureCategory;
    }

    /// <summary>
    /// Gets the normalized failure category. Categories are fixed ASCII
    /// tokens and never carry device, address, or payload values.
    /// </summary>
    public string FailureCategory { get; }
}
