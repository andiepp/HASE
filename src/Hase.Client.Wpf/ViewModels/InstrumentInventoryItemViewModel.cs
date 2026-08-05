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

    public IReadOnlyList<CommandInventoryItemViewModel> GeneralCommands =>
        HasModeSelectionSelector
            ? Commands
                .Where(
                    command =>
                        !command.IsModeSelectionCandidate)
                .ToArray()
            : Commands;

    public bool IsInvokingModeCommand
    {
        get;
        set;
    }

}
