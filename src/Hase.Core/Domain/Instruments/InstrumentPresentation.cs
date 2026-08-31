namespace Hase.Core.Domain.Instruments;

/// <summary>
/// Declares instrument-level presentation metadata, so that a presentation
/// layer can offer a dedicated operating surface for an instrument without
/// knowing the device.
/// </summary>
/// <remarks>
/// This carries capability, not appearance. A declared panel identifier
/// states that a dedicated surface exists for this instrument and names it;
/// it does not prescribe a window, a layout, or a control set. A presentation
/// layer that hosts no panel of that name, or hosts none at all, presents the
/// instrument exactly as before.
///
/// Every member is optional. An instrument without presentation metadata is
/// unchanged.
/// </remarks>
public sealed record InstrumentPresentation
{
    /// <summary>
    /// The greatest supported panel-identifier length.
    /// </summary>
    public const int MaximumPanelIdLength = 64;

    private readonly string? panelId;

    /// <summary>
    /// Gets the identifier of the dedicated operating surface this instrument
    /// declares, for example a signal-generator panel.
    /// </summary>
    /// <remarks>
    /// The identifier is a bounded token of ASCII letters, digits, hyphens,
    /// and full stops. It crosses the northbound boundary and selects a
    /// presentation surface, so it is deliberately restricted: a descriptor
    /// can name a panel, and can neither carry arbitrary text into a
    /// presentation layer nor grow without bound.
    /// </remarks>
    public string? PanelId
    {
        get => panelId;
        init
        {
            if (value is null)
            {
                panelId = null;
                return;
            }

            string candidate = value.Trim();

            if (candidate.Length == 0)
            {
                throw new ArgumentException(
                    "A panel identifier must not be empty or whitespace.",
                    nameof(value));
            }

            if (candidate.Length > MaximumPanelIdLength)
            {
                throw new ArgumentException(
                    "A panel identifier must not exceed "
                    + $"{MaximumPanelIdLength} characters.",
                    nameof(value));
            }

            if (!candidate.All(IsPanelIdCharacter))
            {
                throw new ArgumentException(
                    "A panel identifier must contain only ASCII letters, "
                    + "digits, hyphens, and full stops.",
                    nameof(value));
            }

            panelId = candidate;
        }
    }

    private static bool IsPanelIdCharacter(char value) =>
        char.IsAsciiLetterOrDigit(value)
        || value is '-' or '.';
}
