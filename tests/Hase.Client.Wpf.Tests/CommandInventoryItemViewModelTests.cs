using Hase.Client.Wpf.ViewModels;
using Hase.Core.Domain.Commands;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;

namespace Hase.Client.Wpf.Tests;

public sealed class CommandInventoryItemViewModelTests
{
    [Fact]
    public void ParameterlessReadyCommand_ShouldBeExecutable()
    {
        CommandInventoryItemViewModel command =
            CreateCommand(
                descriptor:
                    new CommandDescriptor(
                        DescriptorPath.Parse(
                            "Controller.Reset"),
                        "Reset"));

        Assert.False(
            command.RequiresArgument);
        Assert.True(
            command.HasValidArgument);
        Assert.True(
            command.CanExecute);
    }

    [Fact]
    public void BooleanCommand_ShouldExposeBooleanEditor()
    {
        CommandInventoryItemViewModel command =
            CreateTypedCommand(
                new BooleanDataDescriptor());

        Assert.True(
            command.RequiresArgument);
        Assert.True(
            command.HasBooleanEditor);
        Assert.False(
            command.HasTextEditor);
        Assert.False(
            command.HasValidArgument);

        command.RequestedBooleanArgument =
            true;

        Assert.True(
            command.HasValidArgument);
        Assert.True(
            command.CanExecute);
        Assert.True(
            Assert.IsType<bool>(
                command.InputResult.Value));
    }

    [Theory]
    [InlineData("23.5", true)]
    [InlineData("23,5", false)]
    [InlineData("126", false)]
    public void NumericCommand_ShouldUseDescriptorValidation(
        string input,
        bool expectedValid)
    {
        CommandInventoryItemViewModel command =
            CreateTypedCommand(
                new NumericDataDescriptor(
                    Quantities.Temperature,
                    Units.Celsius,
                    new ValueRange(
                        -40,
                        125)));

        command.RequestedArgumentText =
            input;

        Assert.True(
            command.HasTextEditor);
        Assert.Equal(
            expectedValid,
            command.HasValidArgument);
        Assert.Equal(
            expectedValid,
            command.CanExecute);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("  HASE  ")]
    public void StringCommand_ShouldPreserveExactInput(
        string input)
    {
        CommandInventoryItemViewModel command =
            CreateTypedCommand(
                new StringDataDescriptor());

        command.RequestedArgumentText =
            input;

        Assert.True(
            command.HasValidArgument);
        Assert.Equal(
            input,
            Assert.IsType<string>(
                command.InputResult.Value));
    }

    [Fact]
    public void ByteArrayCommand_InvalidInput_ShouldNotBeExecutable()
    {
        CommandInventoryItemViewModel command =
            CreateTypedCommand(
                new ByteArrayDataDescriptor());

        command.RequestedArgumentText =
            "0";

        Assert.False(
            command.HasValidArgument);
        Assert.False(
            command.CanExecute);
        Assert.NotEmpty(
            command.ValidationMessage);
    }

    [Fact]
    public void ByteArrayCommand_ValidInput_ShouldBecomeExecutableAndNotify()
    {
        CommandInventoryItemViewModel command =
            CreateTypedCommand(
                new ByteArrayDataDescriptor());
        var changedProperties =
            new List<string?>();
        command.PropertyChanged +=
            (_, eventArgs) =>
                changedProperties.Add(
                    eventArgs.PropertyName);

        command.RequestedArgumentText =
            "00 7F FF";

        Assert.True(
            command.HasValidArgument);
        Assert.True(
            command.CanExecute);
        Assert.Equal(
            new byte[]
            {
                0x00,
                0x7F,
                0xFF
            },
            Assert.IsType<ByteArrayValue>(
                    command.InputResult.Value)
                .ToArray());
        Assert.Contains(
            nameof(CommandInventoryItemViewModel.RequestedArgumentText),
            changedProperties);
        Assert.Contains(
            nameof(CommandInventoryItemViewModel.HasValidArgument),
            changedProperties);
        Assert.Contains(
            nameof(CommandInventoryItemViewModel.CanExecute),
            changedProperties);
    }

    [Fact]
    public void EditingState_ShouldBeMutable()
    {
        CommandInventoryItemViewModel command =
            CreateTypedCommand(
                new ByteArrayDataDescriptor());

        Assert.False(
            command.IsEditingArgument);

        command.IsEditingArgument =
            true;

        Assert.True(
            command.IsEditingArgument);
    }

    [Fact]
    public void CompleteModeCommandSet_ShouldExposeOrderedSelectorAndGeneralCommands()
    {
        var instrument = new InstrumentInventoryItemViewModel(
            "electronic-load-01",
            "Electronic Load",
            "ElectronicLoad",
            [],
            [
                CreateModeCommand("Mode.SelectShortCircuit", "Select SHORT"),
                CreateModeCommand("Mode.SelectConstantPower", "Select CW"),
                CreateCommand(new CommandDescriptor(
                    DescriptorPath.Parse("System.Reset"),
                    "Reset")),
                CreateModeCommand("Mode.SelectConstantResistance", "Select CR"),
                CreateModeCommand("Mode.SelectConstantVoltage", "Select CV"),
                CreateModeCommand("Mode.SelectConstantCurrent", "Select CC")
            ]);

        Assert.True(instrument.HasModeSelectionSelector);
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
    public void ModeSelectionCommand_ShouldRemainReadyForDirectExecution()
    {
        var instrument = new InstrumentInventoryItemViewModel(
            "electronic-load-01",
            "Electronic Load",
            "ElectronicLoad",
            [],
            CreateCompleteModeCommandSet());
        CommandInventoryItemViewModel selected =
            instrument.ModeSelectionCommands[2];

        Assert.Equal(
            "Mode.SelectConstantResistance",
            selected.Target.CommandPath.ToString());
        Assert.True(selected.CanExecute);
    }

    [Fact]
    public void IncompleteModeCommandSet_ShouldRemainGeneric()
    {
        var instrument = new InstrumentInventoryItemViewModel(
            "electronic-load-01",
            "Electronic Load",
            "ElectronicLoad",
            [],
            CreateCompleteModeCommandSet().Take(4).ToArray());

        Assert.False(instrument.HasModeSelectionSelector);
        Assert.Empty(instrument.ModeSelectionCommands);
        Assert.Equal(4, instrument.GeneralCommands.Count);
    }

    [Fact]
    public void ArgumentBearingModeLookalike_ShouldRemainGeneric()
    {
        CommandInventoryItemViewModel[] commands = CreateCompleteModeCommandSet();
        commands[1] = CreateCommand(
            new CommandDescriptor(
                DescriptorPath.Parse("Mode.SelectConstantVoltage"),
                "Select CV",
                new CommandArgumentDescriptor(
                    "Unexpected",
                    new BooleanDataDescriptor())));
        var instrument = new InstrumentInventoryItemViewModel(
            "electronic-load-01",
            "Electronic Load",
            "ElectronicLoad",
            [],
            commands);

        Assert.False(instrument.HasModeSelectionSelector);
        Assert.Equal(5, instrument.GeneralCommands.Count);
    }

    [Fact]
    public void UnavailableEndpoint_ShouldDisableEveryModeSelectionCommand()
    {
        CommandInventoryItemViewModel[] commands =
        [
            CreateModeCommand("Mode.SelectConstantCurrent", "Select CC", endpointReady: false),
            CreateModeCommand("Mode.SelectConstantVoltage", "Select CV", endpointReady: false),
            CreateModeCommand("Mode.SelectConstantResistance", "Select CR", endpointReady: false),
            CreateModeCommand("Mode.SelectConstantPower", "Select CW", endpointReady: false),
            CreateModeCommand("Mode.SelectShortCircuit", "Select SHORT", endpointReady: false)
        ];
        var instrument = new InstrumentInventoryItemViewModel(
            "electronic-load-01",
            "Electronic Load",
            "ElectronicLoad",
            [],
            commands);
        Assert.All(
            instrument.ModeSelectionCommands,
            command =>
                Assert.False(command.CanExecute));
    }

    private static CommandInventoryItemViewModel CreateTypedCommand(
        DataDescriptor data)
    {
        return CreateCommand(
            new CommandDescriptor(
                DescriptorPath.Parse(
                    "Controller.Send"),
                "Send",
                new CommandArgumentDescriptor(
                    "Value",
                    data)));
    }

    private static CommandInventoryItemViewModel CreateCommand(
        CommandDescriptor descriptor,
        bool endpointReady = true)
    {
        return new CommandInventoryItemViewModel(
            CreateTarget(descriptor.Path),
            descriptor.Path.ToString(),
            descriptor.DisplayName,
            descriptor.Description,
            endpointReady)
        {
            Descriptor =
                descriptor
        };
    }

    private static CommandInventoryItemViewModel CreateModeCommand(
        string path,
        string displayName,
        bool endpointReady = true) =>
        CreateCommand(
            new CommandDescriptor(
                DescriptorPath.Parse(path),
                displayName),
            endpointReady);

    private static CommandInventoryItemViewModel[] CreateCompleteModeCommandSet() =>
    [
        CreateModeCommand("Mode.SelectConstantCurrent", "Select CC"),
        CreateModeCommand("Mode.SelectConstantVoltage", "Select CV"),
        CreateModeCommand("Mode.SelectConstantResistance", "Select CR"),
        CreateModeCommand("Mode.SelectConstantPower", "Select CW"),
        CreateModeCommand("Mode.SelectShortCircuit", "Select SHORT")
    ];

    private static RemoteCommandTarget CreateTarget(
        DescriptorPath commandPath)
    {
        return new RemoteCommandTarget(
            new RemoteEndpointAttachmentKey(
                new EndpointId(
                    "endpoint-01"),
                new RemoteEndpointAttachmentGeneration(
                    Guid.Parse(
                        "8f88a60b-ff77-420f-bc7d-73ad82c718e9"))),
            new InstrumentId(
                "controller-01"),
            commandPath);
    }
}
