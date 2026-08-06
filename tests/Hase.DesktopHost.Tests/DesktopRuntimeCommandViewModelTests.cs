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
    public void CompleteModeCommandSet_ShouldExposeOrderedSelectorAndUnrelatedCommands()
    {
        var instrument = new DesktopRuntimeInstrumentViewModel(
            CreateInstrument(
            [
                CreateModeCommand("Mode.SelectShortCircuit", "Select SHORT"),
                CreateModeCommand("Mode.SelectConstantPower", "Select CW"),
                CreateCommand(CreateParameterlessDescriptor("System.Reset", "Reset")),
                CreateModeCommand("Mode.SelectConstantResistance", "Select CR"),
                CreateModeCommand("Mode.SelectConstantVoltage", "Select CV"),
                CreateModeCommand("Mode.SelectConstantCurrent", "Select CC")
            ]));

        Assert.True(instrument.HasModeSelectionSelector);
        Assert.Null(instrument.SelectedModeCommand);
        Assert.Equal(
            ["CC", "CV", "CR", "CW", "SHORT"],
            instrument.ModeSelectionCommands
                .Select(command => command.ModeSelectionLabel)
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
            CreateCompleteModeCommandSet());
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
            CreateInstrument(CreateCompleteModeCommandSet()));
        DesktopRuntimeCommandViewModel selected = instrument.ModeSelectionCommands[2];
        instrument.SelectedModeCommand = selected;

        RuntimeHostCommandTarget? target =
            instrument.SelectedModeCommand.TryBeginExecution();

        Assert.Same(selected.Target, target);
        Assert.Equal(
            "Mode.SelectConstantResistance",
            target!.CommandPath.ToString());
        Assert.Equal(
            DesktopRuntimeCommandExecutionState.Executing,
            selected.ExecutionState);
    }

    [Fact]
    public void IncompleteModeCommandSet_ShouldRetainGenericPresentation()
    {
        var commands = CreateCompleteModeCommandSet().Take(4).ToArray();
        var instrument = new DesktopRuntimeInstrumentViewModel(
            CreateInstrument(commands));

        Assert.False(instrument.HasModeSelectionSelector);
        Assert.Empty(instrument.ModeSelectionCommands);
        Assert.Null(instrument.SelectedModeCommand);
        Assert.Equal(4, instrument.GeneralCommands.Count);
    }

    [Fact]
    public void ModeSelectionCandidate_WithArgument_ShouldRemainGeneric()
    {
        DesktopRuntimeCommandSnapshot[] commands = CreateCompleteModeCommandSet();
        commands[1] = CreateCommand(
            new CommandDescriptor(
                DescriptorPath.Parse("Mode.SelectConstantVoltage"),
                "Select CV",
                new CommandArgumentDescriptor(
                    "Unexpected",
                    new BooleanDataDescriptor())));
        var instrument = new DesktopRuntimeInstrumentViewModel(
            CreateInstrument(commands));

        Assert.False(instrument.HasModeSelectionSelector);
        Assert.Equal(5, instrument.GeneralCommands.Count);
    }

    [Fact]
    public void CompleteInputControlSet_ShouldExposeDedicatedStableCommands()
    {
        DesktopRuntimeInstrumentSnapshot snapshot = CreateInstrument(
        [
            CreateModeCommand("Mode.SelectConstantCurrent", "Select CC"),
            CreateModeCommand("Mode.SelectConstantVoltage", "Select CV"),
            CreateModeCommand("Mode.SelectConstantResistance", "Select CR"),
            CreateModeCommand("Mode.SelectConstantPower", "Select CW"),
            CreateModeCommand("Mode.SelectShortCircuit", "Select SHORT"),
            CreateInputCommand("Input.Deactivate", "Deactivate input"),
            CreateCommand(CreateParameterlessDescriptor("System.Reset", "Reset")),
            CreateInputCommand("Input.Activate", "Activate input"),
            CreateCommand(
                new CommandDescriptor(
                    DescriptorPath.Parse("ShortCircuit.Activate"),
                    "Activate SHORT",
                    new CommandArgumentDescriptor(
                        "Confirmation",
                        new BooleanDataDescriptor())))
        ]);
        var instrument = new DesktopRuntimeInstrumentViewModel(snapshot);

        Assert.True(instrument.HasInputControlControls);
        Assert.True(instrument.HasModeSelectionSelector);
        Assert.Equal("Input.Activate", instrument.ActivateInputCommand!.Path);
        Assert.Equal("Input.Deactivate", instrument.DeactivateInputCommand!.Path);
        Assert.Equal(
            ["Activate", "Deactivate"],
            instrument.InputControlCommands
                .Select(command => command.InputControlLabel)
                .ToArray());
        Assert.Equal(
            ["System.Reset", "ShortCircuit.Activate"],
            instrument.GeneralCommands
                .Select(command => command.Path)
                .ToArray());
    }

    [Fact]
    public void InputControlCommands_ShouldSurviveUnchangedRefresh()
    {
        DesktopRuntimeInstrumentSnapshot snapshot = CreateInstrument(
            CreateCompleteInputControlCommandSet());
        var instrument = new DesktopRuntimeInstrumentViewModel(snapshot);
        DesktopRuntimeCommandViewModel activate = instrument.ActivateInputCommand!;
        DesktopRuntimeCommandViewModel deactivate = instrument.DeactivateInputCommand!;

        instrument.Update(snapshot);

        Assert.Same(activate, instrument.ActivateInputCommand);
        Assert.Same(deactivate, instrument.DeactivateInputCommand);
        Assert.True(instrument.ActivateInputCommand!.CanExecute);
        Assert.True(instrument.DeactivateInputCommand!.CanExecute);
    }

    [Theory]
    [InlineData(0, "Input.Activate")]
    [InlineData(1, "Input.Deactivate")]
    public void InputControlCommand_ExplicitExecutionUsesOwnTarget(
        int commandIndex,
        string expectedPath)
    {
        var instrument = new DesktopRuntimeInstrumentViewModel(
            CreateInstrument(CreateCompleteInputControlCommandSet()));
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
    public void IncompleteInputControlSet_ShouldRetainGenericPresentation()
    {
        var instrument = new DesktopRuntimeInstrumentViewModel(
            CreateInstrument(
            [
                CreateInputCommand("Input.Activate", "Activate input")
            ]));

        Assert.False(instrument.HasInputControlControls);
        Assert.Empty(instrument.InputControlCommands);
        Assert.Null(instrument.ActivateInputCommand);
        Assert.Null(instrument.DeactivateInputCommand);
        Assert.Equal(["Input.Activate"], instrument.GeneralCommands
            .Select(command => command.Path)
            .ToArray());
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

    private static DesktopRuntimeCommandSnapshot CreateModeCommand(
        string path,
        string displayName) =>
        CreateCommand(
            CreateParameterlessDescriptor(path, displayName));

    private static DesktopRuntimeCommandSnapshot CreateInputCommand(
        string path,
        string displayName) =>
        CreateCommand(
            CreateParameterlessDescriptor(path, displayName));

    private static DesktopRuntimeCommandSnapshot[] CreateCompleteModeCommandSet() =>
    [
        CreateModeCommand("Mode.SelectConstantCurrent", "Select CC"),
        CreateModeCommand("Mode.SelectConstantVoltage", "Select CV"),
        CreateModeCommand("Mode.SelectConstantResistance", "Select CR"),
        CreateModeCommand("Mode.SelectConstantPower", "Select CW"),
        CreateModeCommand("Mode.SelectShortCircuit", "Select SHORT")
    ];

    private static DesktopRuntimeCommandSnapshot[] CreateCompleteInputControlCommandSet() =>
    [
        CreateInputCommand("Input.Activate", "Activate input"),
        CreateInputCommand("Input.Deactivate", "Deactivate input")
    ];

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
