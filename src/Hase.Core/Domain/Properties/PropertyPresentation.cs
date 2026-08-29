using Hase.Core.Domain.Data;

namespace Hase.Core.Domain.Properties;

/// <summary>
/// Declares how one Property relates to the other Properties of its
/// instrument, so that a presentation layer can render related Properties
/// together without knowing the device.
/// </summary>
/// <remarks>
/// This carries relationship, not appearance. It states that Properties belong
/// to one reading and where a Property sits on a shared independent axis. It
/// does not prescribe a control, a layout, or a colour: how to draw a group
/// remains entirely the presentation layer's decision.
///
/// Every member is optional. A Property without presentation metadata is
/// rendered on its own exactly as before.
/// </remarks>
public sealed record PropertyPresentation
{
    private readonly string? groupId;

    /// <summary>
    /// Gets the identifier of the group this Property belongs to.
    /// </summary>
    /// <remarks>
    /// Properties of one instrument sharing a group identifier form one
    /// logical reading and may be presented as a unit. Group identifiers are
    /// scoped to their instrument.
    /// </remarks>
    public string? GroupId
    {
        get => groupId;
        init
        {
            if (value is not null
                && string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "A presentation group identifier must not be empty or "
                    + "whitespace.",
                    nameof(value));
            }

            groupId = value?.Trim();
        }
    }

    /// <summary>
    /// Gets the coordinate of this Property on the independent axis shared by
    /// its group, for example the centre wavelength of a spectral channel.
    /// </summary>
    /// <remarks>
    /// A group whose every member declares an abscissa describes a sampled
    /// curve and can be presented as one, with the abscissa as the independent
    /// variable and the Property values as the dependent variable.
    /// </remarks>
    public QuantityValue? Abscissa
    {
        get;
        init;
    }
}
