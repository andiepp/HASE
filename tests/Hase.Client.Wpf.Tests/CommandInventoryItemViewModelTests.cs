using Hase.Client.Wpf.ViewModels;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;

namespace Hase.Client.Wpf.Tests;

public sealed class CommandInventoryItemViewModelTests
{
    [Fact]
    public void ParameterlessReadyCommand_ShouldBeExecutable()
    {
        var command =
            new CommandInventoryItemViewModel(
                CreateTarget(),
                "Controller.Reset",
                "Reset",
                null,
                true);

        Assert.True(
            command.HasValidArgument);
        Assert.True(
            command.CanExecute);
    }

    [Fact]
    public void ByteArrayCommand_InvalidInput_ShouldNotBeExecutable()
    {
        var command =
            CreateByteArrayCommand();

        command.RequestedArgumentText =
            "0";

        Assert.False(
            command.HasValidArgument);
        Assert.False(
            command.CanExecute);
    }

    [Fact]
    public void ByteArrayCommand_ValidInput_ShouldBecomeExecutableAndNotify()
    {
        var command =
            CreateByteArrayCommand();
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

    private static CommandInventoryItemViewModel CreateByteArrayCommand()
    {
        return new CommandInventoryItemViewModel(
            CreateTarget(),
            "Controller.Send",
            "Send",
            null,
            true)
        {
            RequiresArgument =
                true,
            ArgumentDisplayName =
                "Payload",
            ArgumentDataType =
                "ByteArray"
        };
    }

    private static RemoteCommandTarget CreateTarget()
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
            DescriptorPath.Parse(
                "Controller.Send"));
    }
}
