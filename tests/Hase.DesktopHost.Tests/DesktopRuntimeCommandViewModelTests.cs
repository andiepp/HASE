using Hase.Core.Domain.Commands;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.DesktopHost.App.ViewModels;
using Hase.Runtime.Northbound;

namespace Hase.DesktopHost.Tests;

public sealed class DesktopRuntimeCommandViewModelTests
{
    [Fact]
    public void Constructor_ShouldProjectDescriptorMetadata()
    {
        var descriptor =
            new CommandDescriptor(
                DescriptorPath.Parse(
                    "Controller.Toggle"),
                "Toggle status LED")
            {
                Description =
                    "Toggles the endpoint status LED."
            };
        var viewModel =
            new DesktopRuntimeCommandViewModel(
                CreateCommand(
                    descriptor));

        Assert.Equal(
            "Controller.Toggle",
            viewModel.Path);
        Assert.Equal(
            "Toggle status LED",
            viewModel.DisplayName);
        Assert.Equal(
            "Toggles the endpoint status LED.",
            viewModel.Description);
        Assert.Same(
            descriptor,
            viewModel.Descriptor);
        Assert.False(
            viewModel.RequiresArgument);
        Assert.True(
            viewModel.HasValidArgument);
        Assert.True(
            viewModel.CanExecute);
    }

    [Fact]
    public void BooleanCommand_ShouldExposeBooleanEditor()
    {
        DesktopRuntimeCommandViewModel viewModel =
            CreateTypedCommand(
                new BooleanDataDescriptor());

        Assert.True(
            viewModel.RequiresArgument);
        Assert.True(
            viewModel.HasBooleanEditor);
        Assert.False(
            viewModel.HasTextEditor);
        Assert.False(
            viewModel.HasValidArgument);
        Assert.False(
            viewModel.CanExecute);

        viewModel.RequestedBooleanArgument =
            true;

        Assert.True(
            viewModel.HasValidArgument);
        Assert.True(
            viewModel.CanExecute);
        Assert.True(
            Assert.IsType<bool>(
                viewModel.InputResult.Value));
    }

    [Theory]
    [InlineData("23.5", true)]
    [InlineData("23,5", false)]
    [InlineData("126", false)]
    public void NumericCommand_ShouldUseDescriptorValidation(
        string input,
        bool expectedValid)
    {
        DesktopRuntimeCommandViewModel viewModel =
            CreateTypedCommand(
                new NumericDataDescriptor(
                    Quantities.Temperature,
                    Units.Celsius,
                    new ValueRange(
                        -40,
                        125)));

        viewModel.RequestedArgumentText =
            input;

        Assert.True(
            viewModel.HasTextEditor);
        Assert.Equal(
            expectedValid,
            viewModel.HasValidArgument);
        Assert.Equal(
            expectedValid,
            viewModel.CanExecute);
    }

    [Fact]
    public void ByteArrayCommand_InvalidInput_ShouldRemainDisabled()
    {
        DesktopRuntimeCommandViewModel viewModel =
            CreateTypedCommand(
                new ByteArrayDataDescriptor());

        viewModel.RequestedArgumentText =
            "0";

        Assert.False(
            viewModel.HasValidArgument);
        Assert.False(
            viewModel.CanExecute);
        Assert.NotEmpty(
            viewModel.ValidationMessage);
    }

    [Fact]
    public void ByteArrayCommand_ValidInput_ShouldBecomeExecutableAndNotify()
    {
        DesktopRuntimeCommandViewModel viewModel =
            CreateTypedCommand(
                new ByteArrayDataDescriptor());
        var changedProperties =
            new List<string?>();
        viewModel.PropertyChanged +=
            (_, eventArgs) =>
                changedProperties.Add(
                    eventArgs.PropertyName);

        viewModel.RequestedArgumentText =
            "00 7F FF";

        Assert.True(
            viewModel.HasValidArgument);
        Assert.True(
            viewModel.CanExecute);
        Assert.Equal(
            new byte[]
            {
                0x00,
                0x7F,
                0xFF
            },
            Assert.IsType<ByteArrayValue>(
                    viewModel.InputResult.Value)
                .ToArray());
        Assert.Contains(
            nameof(DesktopRuntimeCommandViewModel.RequestedArgumentText),
            changedProperties);
        Assert.Contains(
            nameof(DesktopRuntimeCommandViewModel.HasValidArgument),
            changedProperties);
        Assert.Contains(
            nameof(DesktopRuntimeCommandViewModel.CanExecute),
            changedProperties);
    }

    [Fact]
    public void InstrumentUpdate_WithUnchangedDescriptor_ShouldPreserveCommandViewModel()
    {
        DesktopRuntimeCommandSnapshot command =
            CreateCommand(
                new CommandDescriptor(
                    DescriptorPath.Parse(
                        "Controller.Toggle"),
                    "Toggle status LED")
                {
                    Description =
                        "Toggles the endpoint status LED."
                });
        var instrument =
            new DesktopRuntimeInstrumentViewModel(
                CreateInstrument(
                    [command]));
        DesktopRuntimeCommandViewModel original =
            instrument.Commands[0];

        instrument.Update(
            CreateInstrument(
                [command]));

        Assert.Same(
            original,
            instrument.Commands[0]);
    }

    [Fact]
    public void InstrumentUpdate_WithChangedDescriptor_ShouldReplaceCommandViewModel()
    {
        var instrument =
            new DesktopRuntimeInstrumentViewModel(
                CreateInstrument(
                    [
                        CreateCommand(
                            new CommandDescriptor(
                                DescriptorPath.Parse(
                                    "Controller.Send"),
                                "Send",
                                new CommandArgumentDescriptor(
                                    "Value",
                                    new StringDataDescriptor())))
                    ]));
        DesktopRuntimeCommandViewModel original =
            instrument.Commands[0];

        instrument.Update(
            CreateInstrument(
                [
                    CreateCommand(
                        new CommandDescriptor(
                            DescriptorPath.Parse(
                                "Controller.Send"),
                            "Send",
                            new CommandArgumentDescriptor(
                                "Value",
                                new ByteArrayDataDescriptor())))
                ]));

        Assert.NotSame(
            original,
            instrument.Commands[0]);
        Assert.Equal(
            "ByteArray",
            instrument.Commands[0].ArgumentDataType);
    }

    [Fact]
    public void InstrumentUpdate_ShouldAddRemoveAndPreserveDescriptorOrder()
    {
        var instrument =
            new DesktopRuntimeInstrumentViewModel(
                CreateInstrument(
                    [
                        CreateCommand(
                            CreateParameterlessDescriptor(
                                "Controller.First",
                                "First")),
                        CreateCommand(
                            CreateParameterlessDescriptor(
                                "Controller.Removed",
                                "Removed"))
                    ]));
        DesktopRuntimeCommandViewModel first =
            instrument.Commands[0];

        instrument.Update(
            CreateInstrument(
                [
                    CreateCommand(
                        CreateParameterlessDescriptor(
                            "Controller.Added",
                            "Added")),
                    CreateCommand(
                        CreateParameterlessDescriptor(
                            "Controller.First",
                            "First"))
                ]));

        Assert.Equal(
            2,
            instrument.CommandCount);
        Assert.Equal(
            ["Controller.Added", "Controller.First"],
            instrument.Commands
                .Select(
                    command =>
                        command.Path)
                .ToArray());
        Assert.Same(
            first,
            instrument.Commands[1]);
    }

    [Fact]
    public void DeclaredSelection_ShouldExposeSelectorAndLeaveOthersGeneral()
    {
        var instrument = new DesktopRuntimeInstrumentViewModel(
            CreateInstrument(
            [
                CreateSelectionCommand("Selection.Gamma", "Choose gamma", "Gamma"),
                CreateCommand(CreateParameterlessDescriptor("System.Reset", "Reset")),
                CreateSelectionCommand("Selection.Alpha", "Choose alpha", "Alpha"),
                CreateSelectionCommand("Selection.Beta", "Choose beta", "Beta")
            ]));

        Assert.True(instrument.HasModeSelectionSelector);
        Assert.Null(instrument.SelectedModeCommand);
        Assert.Equal(
            ["Gamma", "Alpha", "Beta"],
            instrument.ModeSelectionCommands
                .Select(command => command.ModeSelectionLabel!)
                .ToArray());
        Assert.Equal(
            ["System.Reset"],
            instrument.GeneralCommands
                .Select(command => command.Path)
                .ToArray());
    }

    [Fact]
    public void ModeSelection_ShouldNotBeginExecutionAndShouldSurviveRefresh()
    {
        DesktopRuntimeInstrumentSnapshot snapshot = CreateInstrument(
            CreateDeclaredSelectionSet());
        var instrument = new DesktopRuntimeInstrumentViewModel(snapshot);
        DesktopRuntimeCommandViewModel selected = instrument.ModeSelectionCommands[1];

        instrument.SelectedModeCommand = selected;

        Assert.Equal(DesktopRuntimeCommandExecutionState.Ready, selected.ExecutionState);
        instrument.Update(snapshot);
        Assert.Same(selected, instrument.SelectedModeCommand);
        Assert.Equal(DesktopRuntimeCommandExecutionState.Ready, selected.ExecutionState);
    }

    [Fact]
    public void SelectedModeCommand_ExplicitExecutionShouldUseExistingCommandTarget()
    {
        var instrument = new DesktopRuntimeInstrumentViewModel(
            CreateInstrument(CreateDeclaredSelectionSet()));
        DesktopRuntimeCommandViewModel selected = instrument.ModeSelectionCommands[2];
        instrument.SelectedModeCommand = selected;

        RuntimeHostCommandTarget? target =
            instrument.SelectedModeCommand.TryBeginExecution();

        Assert.Same(selected.Target, target);
        Assert.Equal("Selection.Gamma", target!.CommandPath.ToString());
        Assert.Equal(
            DesktopRuntimeCommandExecutionState.Executing,
            selected.ExecutionState);
    }

    [Fact]
    public void UndeclaredCommands_ShouldRetainGenericPresentation()
    {
        var instrument = new DesktopRuntimeInstrumentViewModel(
            CreateInstrument(
            [
                CreateCommand(CreateParameterlessDescriptor("System.Reset", "Reset")),
                CreateCommand(CreateParameterlessDescriptor("System.Clear", "Clear"))
            ]));

        Assert.False(instrument.HasModeSelectionSelector);
        Assert.Empty(instrument.ModeSelectionCommands);
        Assert.False(instrument.HasInputControlControls);
        Assert.Null(instrument.SelectedModeCommand);
        Assert.Equal(2, instrument.GeneralCommands.Count);
    }

    [Fact]
    public void ASingleDeclaredChoice_IsNotOfferedAsASelector()
    {
        // A selection worth one control takes at least two choices.
        var instrument = new DesktopRuntimeInstrumentViewModel(
            CreateInstrument(
            [
                CreateSelectionCommand("Selection.Alpha", "Choose alpha", "Alpha")
            ]));

        Assert.False(instrument.HasModeSelectionSelector);
        Assert.Equal(
            ["Selection.Alpha"],
            instrument.GeneralCommands
                .Select(command => command.Path)
                .ToArray());
    }

    [Fact]
    public void OnlyOneDeclaredSelectionIsOffered()
    {
        // An instrument may declare several selections; the presentation
        // offers one control rather than merging unrelated choices.
        var instrument = new DesktopRuntimeInstrumentViewModel(
            CreateInstrument(
            [
                CreateSelectionCommand("Selection.Alpha", "Choose alpha", "Alpha"),
                CreateSelectionCommand("Selection.Beta", "Choose beta", "Beta"),
                CreateSelectionCommand(
                    "Range.Low", "Choose low", "Low", "measurement-range"),
                CreateSelectionCommand(
                    "Range.High", "Choose high", "High", "measurement-range")
            ]));

        Assert.Equal(
            ["Alpha", "Beta"],
            instrument.ModeSelectionCommands
                .Select(command => command.ModeSelectionLabel!)
                .ToArray());
        Assert.Equal(
            ["Range.Low", "Range.High"],
            instrument.GeneralCommands
                .Select(command => command.Path)
                .ToArray());
    }

    [Fact]
    public void SelectionCandidate_WithArgument_ShouldRemainGeneric()
    {
        DesktopRuntimeCommandSnapshot[] commands = CreateDeclaredSelectionSet();
        commands[1] = CreateCommand(
            new CommandDescriptor(
                DescriptorPath.Parse("Selection.Beta"),
                "Choose beta",
                new CommandArgumentDescriptor(
                    "Unexpected",
                    new BooleanDataDescriptor()))
            {
                Presentation = new CommandPresentation
                {
                    ShortLabel = "Beta",
                    SelectionGroupId = SelectionGroup,
                    SelectionStatePath = DescriptorPath.Parse("Selection.State"),
                    SelectionValue = "Beta"
                }
            });
        var instrument = new DesktopRuntimeInstrumentViewModel(
            CreateInstrument(commands));

        Assert.Equal(
            ["Alpha", "Gamma"],
            instrument.ModeSelectionCommands
                .Select(command => command.ModeSelectionLabel!)
                .ToArray());
        Assert.Equal(
            ["Selection.Beta"],
            instrument.GeneralCommands
                .Select(command => command.Path)
                .ToArray());
    }

    [Fact]
    public void DeclaredLabelsWithoutASelection_ShouldBeOfferedAsInputControls()
    {
        DesktopRuntimeInstrumentSnapshot snapshot = CreateInstrument(
        [
            CreateSelectionCommand("Selection.Alpha", "Choose alpha", "Alpha"),
            CreateSelectionCommand("Selection.Beta", "Choose beta", "Beta"),
            CreateLabelledCommand("Control.Stop", "Stop the output", "Stop"),
            CreateCommand(CreateParameterlessDescriptor("System.Reset", "Reset")),
            CreateLabelledCommand("Control.Start", "Start the output", "Start"),
            CreateConfirmationCommandSnapshot()
        ]);
        var instrument = new DesktopRuntimeInstrumentViewModel(snapshot);

        Assert.True(instrument.HasInputControlControls);
        Assert.True(instrument.HasModeSelectionSelector);
        Assert.True(instrument.HasConfirmationCommands);
        Assert.Equal(
            ["Stop", "Start"],
            instrument.InputControlCommands
                .Select(command => command.InputControlLabel!)
                .ToArray());
        Assert.Equal(
            ["Guarded.Transmit"],
            instrument.ConfirmationCommands
                .Select(command => command.Path)
                .ToArray());
        Assert.Equal(
            ["System.Reset"],
            instrument.GeneralCommands
                .Select(command => command.Path)
                .ToArray());
    }

    [Fact]
    public void InputControlCommands_ShouldSurviveUnchangedRefresh()
    {
        DesktopRuntimeInstrumentSnapshot snapshot = CreateInstrument(
            CreateDeclaredLabelSet());
        var instrument = new DesktopRuntimeInstrumentViewModel(snapshot);
        DesktopRuntimeCommandViewModel start = instrument.InputControlCommands[0];
        DesktopRuntimeCommandViewModel stop = instrument.InputControlCommands[1];

        instrument.Update(snapshot);

        Assert.Same(start, instrument.InputControlCommands[0]);
        Assert.Same(stop, instrument.InputControlCommands[1]);
        Assert.True(start.CanExecute);
        Assert.True(stop.CanExecute);
    }

    [Theory]
    [InlineData(0, "Control.Start")]
    [InlineData(1, "Control.Stop")]
    public void InputControlCommand_ExplicitExecutionUsesOwnTarget(
        int commandIndex,
        string expectedPath)
    {
        var instrument = new DesktopRuntimeInstrumentViewModel(
            CreateInstrument(CreateDeclaredLabelSet()));
        DesktopRuntimeCommandViewModel command =
            instrument.InputControlCommands[commandIndex];

        RuntimeHostCommandTarget? target = command.TryBeginExecution();

        Assert.Same(command.Target, target);
        Assert.Equal(expectedPath, target!.CommandPath.ToString());
        Assert.Equal(
            DesktopRuntimeCommandExecutionState.Executing,
            command.ExecutionState);
    }

    [Fact]
    public void AConfirmationCommand_IsOfferedWithoutAnyInputControl()
    {
        // The confirmation surface no longer depends on the instrument also
        // declaring input controls.
        var instrument = new DesktopRuntimeInstrumentViewModel(
            CreateInstrument([CreateConfirmationCommandSnapshot()]));

        Assert.False(instrument.HasInputControlControls);
        Assert.True(instrument.HasConfirmationCommands);
        Assert.Equal(
            "Guarded.Transmit",
            Assert.Single(instrument.ConfirmationCommands).Path);
        Assert.Empty(instrument.GeneralCommands);
    }

    [Fact]
    public void DeclaredConfirmation_RequiresExactlyTrue()
    {
        DesktopRuntimeCommandViewModel command = CreateConfirmationCommand();

        Assert.True(command.RequiresExplicitConfirmation);
        Assert.False(command.HasValidArgument);
        Assert.False(command.CanExecute);
        Assert.Contains("explicit", command.ValidationMessage, StringComparison.OrdinalIgnoreCase);

        command.RequestedBooleanArgument = false;

        Assert.False(command.HasValidArgument);
        Assert.False(command.CanExecute);

        command.RequestedBooleanArgument = true;

        Assert.True(command.HasValidArgument);
        Assert.True(command.CanExecute);
        Assert.True(Assert.IsType<bool>(command.InputResult.Value));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void DeclaredConfirmation_ConsumesConfirmationAfterAttempt(
        int outcome)
    {
        DesktopRuntimeCommandViewModel command = CreateConfirmationCommand();
        command.RequestedBooleanArgument = true;
        Assert.NotNull(command.TryBeginExecution());

        switch (outcome)
        {
            case 0:
                command.CompleteExecution(RuntimeHostCommandOperationResult.Successful());
                break;
            case 1:
                command.CompleteExecution(RuntimeHostCommandOperationResult.Failed(
                    RuntimeHostCommandOperationStatus.EndpointRejected));
                break;
            case 2:
                command.CancelExecution();
                break;
            default:
                command.FailExecution(new InvalidOperationException("Failure."));
                break;
        }

        Assert.Null(command.RequestedBooleanArgument);
        Assert.False(command.HasValidArgument);
        Assert.False(command.CanExecute);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    public void ConfirmationIsDeclaredRatherThanInferred(
        bool declared,
        bool booleanArgument,
        bool expected)
    {
        var descriptor = new CommandDescriptor(
            DescriptorPath.Parse("Guarded.Transmit"),
            "Transmit guarded Command",
            new CommandArgumentDescriptor(
                "Confirmation",
                booleanArgument
                    ? new BooleanDataDescriptor()
                    : new StringDataDescriptor()))
        {
            RequiresExplicitConfirmation = declared
        };
        var command = new DesktopRuntimeCommandViewModel(CreateCommand(descriptor));

        Assert.Equal(expected, command.RequiresExplicitConfirmation);
    }

    [Fact]
    public void Constructor_WithEmptyPath_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            "snapshot",
            () => new DesktopRuntimeCommandViewModel(
                CreateCommand(
                    CreateParameterlessDescriptor(
                        "Controller.Command",
                        "Command"),
                    pathOverride:
                        string.Empty)));
    }

    [Fact]
    public void Constructor_WithEmptyDisplayName_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            "snapshot",
            () => new DesktopRuntimeCommandViewModel(
                CreateCommand(
                    CreateParameterlessDescriptor(
                        "Controller.Command",
                        "Command"),
                    displayNameOverride:
                        string.Empty)));
    }

    private static DesktopRuntimeCommandViewModel CreateTypedCommand(
        DataDescriptor data) =>
        new(
            CreateCommand(
                new CommandDescriptor(
                    DescriptorPath.Parse(
                        "Controller.Send"),
                    "Send",
                    new CommandArgumentDescriptor(
                        "Value",
                        data))));

    private static CommandDescriptor CreateParameterlessDescriptor(
        string path,
        string displayName) =>
        new(
            DescriptorPath.Parse(
                path),
            displayName);

    private const string SelectionGroup = "operating-selection";

    private static DesktopRuntimeCommandSnapshot CreateSelectionCommand(
        string path,
        string displayName,
        string shortLabel,
        string selectionGroupId = SelectionGroup) =>
        CreateCommand(
            new CommandDescriptor(
                DescriptorPath.Parse(path),
                displayName)
            {
                Presentation = new CommandPresentation
                {
                    ShortLabel = shortLabel,
                    SelectionGroupId = selectionGroupId,
                    SelectionStatePath = DescriptorPath.Parse("Selection.State"),
                    SelectionValue = shortLabel
                }
            });

    private static DesktopRuntimeCommandSnapshot CreateLabelledCommand(
        string path,
        string displayName,
        string shortLabel) =>
        CreateCommand(
            new CommandDescriptor(
                DescriptorPath.Parse(path),
                displayName)
            {
                Presentation = new CommandPresentation
                {
                    ShortLabel = shortLabel
                }
            });

    private static DesktopRuntimeCommandSnapshot[] CreateDeclaredSelectionSet() =>
    [
        CreateSelectionCommand("Selection.Alpha", "Choose alpha", "Alpha"),
        CreateSelectionCommand("Selection.Beta", "Choose beta", "Beta"),
        CreateSelectionCommand("Selection.Gamma", "Choose gamma", "Gamma")
    ];

    private static DesktopRuntimeCommandSnapshot[] CreateDeclaredLabelSet() =>
    [
        CreateLabelledCommand("Control.Start", "Start the output", "Start"),
        CreateLabelledCommand("Control.Stop", "Stop the output", "Stop")
    ];

    private static DesktopRuntimeCommandSnapshot CreateConfirmationCommandSnapshot() =>
        CreateCommand(
            new CommandDescriptor(
                DescriptorPath.Parse("Guarded.Transmit"),
                "Transmit guarded Command",
                new CommandArgumentDescriptor(
                    "Confirmation",
                    new BooleanDataDescriptor())
                {
                    Description =
                        "The value true explicitly confirms this transmission."
                })
            {
                RequiresExplicitConfirmation = true
            });

    private static DesktopRuntimeCommandViewModel CreateConfirmationCommand() =>
        new(CreateConfirmationCommandSnapshot());

    private static DesktopRuntimeCommandSnapshot CreateCommand(
        CommandDescriptor descriptor,
        string? pathOverride = null,
        string? displayNameOverride = null) =>
        new(
            CreateTarget(
                descriptor.Path.ToString()),
            pathOverride
                ?? descriptor.Path.ToString(),
            displayNameOverride
                ?? descriptor.DisplayName,
            descriptor.Description,
            IsEndpointReady: true,
            descriptor);

    private static RuntimeHostCommandTarget CreateTarget(
        string path) =>
        new(
            new EndpointId("endpoint-1"),
            new RuntimeEndpointAttachmentGeneration(
                Guid.Parse("55e39774-cc7f-4473-8a2e-4bc5bbb79f55")),
            new InstrumentId("instrument-1"),
            DescriptorPath.Parse(
                path));

    private static DesktopRuntimeInstrumentSnapshot CreateInstrument(
        IReadOnlyList<DesktopRuntimeCommandSnapshot> commands) =>
        new(
            "instrument-1",
            "Controller",
            "Controller",
            "HASE",
            "Controller",
            null,
            null,
            null,
            null)
        {
            Commands =
                commands
        };
}
