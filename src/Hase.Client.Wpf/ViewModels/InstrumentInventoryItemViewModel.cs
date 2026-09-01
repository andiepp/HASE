namespace Hase.Client.Wpf.ViewModels;

public sealed record InstrumentInventoryItemViewModel(
    string InstrumentId,
    string Name,
    string Kind,
    IReadOnlyList<PropertyInventoryItemViewModel> Properties,
    IReadOnlyList<CommandInventoryItemViewModel> Commands)
{
    /// <summary>
    /// Gets the commands of this instrument's first declared selection, in
    /// the order the instrument declares them.
    /// </summary>
    /// <remarks>
    /// Which commands form a selection, what they are called, and in which
    /// order they are offered are all the instrument's declarations. This
    /// view model holds no list of expected members.
    /// </remarks>
    public IReadOnlyList<CommandInventoryItemViewModel> ModeSelectionCommands
    {
        get
        {
            CommandInventoryItemViewModel[] candidates =
                Commands
                    .Where(command => command.IsModeSelectionCandidate)
                    .ToArray();

            if (candidates.Length == 0)
            {
                return [];
            }

            string? firstGroupId =
                candidates[0].SelectionGroupId;

            return candidates
                .Where(command =>
                    string.Equals(
                        command.SelectionGroupId,
                        firstGroupId,
                        StringComparison.Ordinal))
                .ToArray();
        }
    }

    public bool HasModeSelectionSelector =>
        ModeSelectionCommands.Count > 1;

    /// <summary>
    /// Gets the commands this instrument declares a label for without
    /// declaring a selection, in the order the instrument declares them.
    /// </summary>
    public IReadOnlyList<CommandInventoryItemViewModel> InputControlCommands =>
        Commands
            .Where(command => command.IsInputControlCandidate)
            .ToArray();

    public bool HasInputControls =>
        InputControlCommands.Count > 0;

    public CommandInventoryItemViewModel? ConfirmedShortCircuitActivationCommand
    {
        get
        {
            CommandInventoryItemViewModel[] candidates =
                Commands
                    .Where(command =>
                        command.RequiresExplicitConfirmation)
                    .ToArray();

            return candidates.Length == 1
                ? candidates[0]
                : null;
        }
    }

    public bool HasConfirmedShortCircuitActivation =>
        ConfirmedShortCircuitActivationCommand is not null;

    public IReadOnlyList<CommandInventoryItemViewModel> GeneralCommands =>
        HasModeSelectionSelector
        || HasInputControls
        || HasConfirmedShortCircuitActivation
            ? Commands
                .Where(
                    command =>
                        (!HasModeSelectionSelector
                            || !command.IsModeSelectionCandidate)
                        && (!HasInputControls
                            || !command.IsInputControlCandidate)
                        && (!HasConfirmedShortCircuitActivation
                            || !command.RequiresExplicitConfirmation))
                .ToArray()
            : Commands;

    public bool IsInvokingModeCommand
    {
        get;
        set;
    }

    public bool IsInvokingInputCommand
    {
        get;
        set;
    }

    public bool IsInvokingShortCircuitCommand
    {
        get;
        set;
    }

    /// <summary>
    /// Gets the Properties that declare a presentation group, grouped and in
    /// declaration order.
    /// </summary>
    public IReadOnlyList<PropertyGroupItemViewModel> PropertyGroups =>
        Properties
            .Where(
                property =>
                    property.IsGrouped)
            .GroupBy(
                property =>
                    property.GroupId!,
                StringComparer.Ordinal)
            .Select(
                group =>
                    new PropertyGroupItemViewModel(
                        group.Key,
                        group.ToArray()))
            .ToArray();

    /// <summary>
    /// Gets whether this instrument declares any presentation group.
    /// </summary>
    public bool HasPropertyGroups =>
        PropertyGroups.Count > 0;

    /// <summary>
    /// Gets the Properties that are presented on their own.
    /// </summary>
    public IReadOnlyList<PropertyInventoryItemViewModel> UngroupedProperties =>
        Properties
            .Where(
                property =>
                    !property.IsGrouped)
            .ToArray();

    /// <summary>
    /// Gets the panel identifier this instrument declares, if any.
    /// </summary>
    public string? PanelId
    {
        get;
        init;
    }

}
