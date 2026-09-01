using Hase.Core.Domain.Commands;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Properties;

namespace Hase.Client.Wpf.Tests;

/// <summary>
/// Builds command descriptors as an instrument now publishes them, so these
/// fixtures declare what they exercise instead of relying on the client
/// recognising a path.
/// </summary>
internal static class DeclaredCommandDescriptors
{
    /// <summary>
    /// One member of a selection, naming its own short label, the selection
    /// it belongs to, the property reporting which member is in effect, and
    /// the value that property reads when this one is.
    /// </summary>
    public static CommandDescriptor Mode(
        string path,
        string displayName,
        string shortLabel) =>
        new(
            DescriptorPath.Parse(path),
            displayName)
        {
            Presentation = new CommandPresentation
            {
                ShortLabel = shortLabel,
                SelectionGroupId = "operating-mode",
                SelectionStatePath = DescriptorPath.Parse("Operating.Mode"),
                SelectionValue = shortLabel
            }
        };

    /// <summary>
    /// A labelled command that declares no selection, so it is offered as a
    /// control rather than as one of a set of choices.
    /// </summary>
    public static CommandDescriptor Input(
        string path,
        string displayName) =>
        new(
            DescriptorPath.Parse(path),
            displayName)
        {
            Presentation = new CommandPresentation
            {
                ShortLabel = displayName
            }
        };

    /// <summary>
    /// A command whose Boolean argument the instrument requires to be
    /// confirmed explicitly.
    /// </summary>
    public static CommandDescriptor Confirmed(
        DescriptorPath path,
        string displayName) =>
        new(
            path,
            displayName,
            new CommandArgumentDescriptor(
                "Confirmation",
                new BooleanDataDescriptor()))
        {
            RequiresExplicitConfirmation = true
        };
}
