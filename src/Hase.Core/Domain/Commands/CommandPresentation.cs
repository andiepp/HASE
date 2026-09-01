using Hase.Core.Domain.Properties;

namespace Hase.Core.Domain.Commands;

/// <summary>
/// Declares how one Command relates to the other Commands of its instrument,
/// so that a presentation layer can offer them together without knowing the
/// device.
/// </summary>
/// <remarks>
/// This carries relationship and the instrument's own naming, not appearance.
/// It states that a Command is one of a set of mutually exclusive choices,
/// which Property reports the choice currently in effect, and what that
/// Property reads when this Command's choice is the one in effect. It does not
/// prescribe a control, a layout, or a colour: how to draw a selection remains
/// entirely the presentation layer's decision.
///
/// Every member is optional. A Command without presentation metadata is
/// offered on its own exactly as before.
/// </remarks>
public sealed record CommandPresentation
{
    private readonly string? shortLabel;
    private readonly string? selectionGroupId;
    private readonly string? selectionValue;

    /// <summary>
    /// Gets the instrument's own short name for this Command, for use where
    /// the full display name does not fit.
    /// </summary>
    /// <remarks>
    /// This is the device's vocabulary rather than a caption chosen for a
    /// layout: an electronic load calls its constant-current mode "CC".
    /// </remarks>
    public string? ShortLabel
    {
        get => shortLabel;
        init => shortLabel = Normalize(value, nameof(ShortLabel));
    }

    /// <summary>
    /// Gets the identifier of the selection this Command belongs to.
    /// </summary>
    /// <remarks>
    /// Commands of one instrument sharing a selection identifier are mutually
    /// exclusive choices and may be presented as one control. Selection
    /// identifiers are scoped to their instrument.
    /// </remarks>
    public string? SelectionGroupId
    {
        get => selectionGroupId;
        init => selectionGroupId = Normalize(value, nameof(SelectionGroupId));
    }

    /// <summary>
    /// Gets the path of the Property that reports which choice of this
    /// Command's selection is currently in effect.
    /// </summary>
    public DescriptorPath? SelectionStatePath
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the value that Property reads when this Command's choice is the
    /// one in effect.
    /// </summary>
    /// <remarks>
    /// Compared without case sensitivity, because an instrument may report
    /// its own vocabulary in more than one casing.
    /// </remarks>
    public string? SelectionValue
    {
        get => selectionValue;
        init => selectionValue = Normalize(value, nameof(SelectionValue));
    }

    /// <summary>
    /// Indicates whether this Command declares a selection this presentation
    /// layer can resolve against a Property.
    /// </summary>
    public bool DeclaresResolvableSelection =>
        SelectionGroupId is not null
        && SelectionStatePath is not null
        && SelectionValue is not null;

    /// <summary>
    /// Indicates whether the supplied Property reading means this Command's
    /// choice is the one in effect.
    /// </summary>
    public bool IsInEffect(string? reportedValue) =>
        SelectionValue is string expected
        && reportedValue is not null
        && string.Equals(
            expected,
            reportedValue.Trim(),
            StringComparison.OrdinalIgnoreCase);

    private static string? Normalize(string? value, string memberName)
    {
        if (value is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                $"A command presentation {memberName} must not be empty or "
                + "whitespace.",
                memberName);
        }

        return value.Trim();
    }
}
