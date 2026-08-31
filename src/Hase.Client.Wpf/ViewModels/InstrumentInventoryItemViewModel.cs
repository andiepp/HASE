namespace Hase.Client.Wpf.ViewModels;

public sealed record InstrumentInventoryItemViewModel(
    string InstrumentId,
    string Name,
    string Kind,
    IReadOnlyList<PropertyInventoryItemViewModel> Properties,
    IReadOnlyList<CommandInventoryItemViewModel> Commands)
{
    private static readonly string[] ModeSelectionOrder =
    [
        "CC",
        "CV",
        "CR",
        "CW",
        "SHORT"
    ];

    private static readonly string[] InputControlOrder =
    [
        "Activate input",
        "Deactivate input"
    ];

    public IReadOnlyList<CommandInventoryItemViewModel> ModeSelectionCommands
    {
        get
        {
            CommandInventoryItemViewModel[] candidates =
                Commands
                    .Where(
                        command =>
                            command.IsModeSelectionCandidate)
                    .ToArray();
            bool hasCompleteSelector =
                candidates.Length == ModeSelectionOrder.Length
                && ModeSelectionOrder.All(
                    label =>
                        candidates.Count(
                            command =>
                                string.Equals(
                                    command.ModeSelectionLabel,
                                    label,
                                    StringComparison.Ordinal))
                        == 1);

            return hasCompleteSelector
                ? ModeSelectionOrder
                    .Select(
                        label =>
                            candidates.Single(
                                command =>
                                    string.Equals(
                                        command.ModeSelectionLabel,
                                        label,
                                        StringComparison.Ordinal)))
                    .ToArray()
                : [];
        }
    }

    public bool HasModeSelectionSelector =>
        ModeSelectionCommands.Count
        == ModeSelectionOrder.Length;

    public IReadOnlyList<CommandInventoryItemViewModel> InputControlCommands
    {
        get
        {
            CommandInventoryItemViewModel[] candidates =
                Commands
                    .Where(
                        command =>
                            command.IsInputControlCandidate)
                    .ToArray();
            bool hasCompleteControls =
                candidates.Length == InputControlOrder.Length
                && InputControlOrder.All(
                    label =>
                        candidates.Count(
                            command =>
                                string.Equals(
                                    command.InputControlLabel,
                                    label,
                                    StringComparison.Ordinal))
                        == 1);

            return hasCompleteControls
                ? InputControlOrder
                    .Select(
                        label =>
                            candidates.Single(
                                command =>
                                    string.Equals(
                                        command.InputControlLabel,
                                        label,
                                        StringComparison.Ordinal)))
                    .ToArray()
                : [];
        }
    }

    public bool HasInputControls =>
        InputControlCommands.Count
        == InputControlOrder.Length;

    public CommandInventoryItemViewModel? ConfirmedShortCircuitActivationCommand
    {
        get
        {
            CommandInventoryItemViewModel[] candidates =
                Commands
                    .Where(command =>
                        command.IsConfirmedShortCircuitActivation)
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
                            || !command.IsConfirmedShortCircuitActivation))
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
