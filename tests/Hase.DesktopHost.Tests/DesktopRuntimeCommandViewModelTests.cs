using Hase.DesktopHost.App.ViewModels;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Northbound;

namespace Hase.DesktopHost.Tests;

public sealed class DesktopRuntimeCommandViewModelTests
{
    [Fact]
    public void Constructor_ShouldProjectDescriptorMetadata()
    {
        var viewModel =
            new DesktopRuntimeCommandViewModel(
                CreateCommand(
                    "Controller.Toggle",
                    "Toggle status LED",
                    "Toggles the endpoint status LED."));

        Assert.Equal(
            "Controller.Toggle",
            viewModel.Path);
        Assert.Equal(
            "Toggle status LED",
            viewModel.DisplayName);
        Assert.Equal(
            "Toggles the endpoint status LED.",
            viewModel.Description);
    }

    [Fact]
    public void InstrumentUpdate_WithUnchangedDescriptor_ShouldPreserveCommandViewModel()
    {
        DesktopRuntimeCommandSnapshot command =
            CreateCommand(
                "Controller.Toggle",
                "Toggle status LED",
                "Toggles the endpoint status LED.");
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
    public void InstrumentUpdate_WithChangedMetadata_ShouldReplaceCommandViewModel()
    {
        var instrument =
            new DesktopRuntimeInstrumentViewModel(
                CreateInstrument(
                    [
                        CreateCommand(
                            "Controller.Toggle",
                            "Toggle status LED",
                            null)
                    ]));
        DesktopRuntimeCommandViewModel original =
            instrument.Commands[0];

        instrument.Update(
            CreateInstrument(
                [
                    CreateCommand(
                        "Controller.Toggle",
                        "Toggle LED",
                        "Updated description.")
                ]));

        Assert.NotSame(
            original,
            instrument.Commands[0]);
        Assert.Equal(
            "Toggle LED",
            instrument.Commands[0].DisplayName);
        Assert.Equal(
            "Updated description.",
            instrument.Commands[0].Description);
    }

    [Fact]
    public void InstrumentUpdate_ShouldAddRemoveAndPreserveDescriptorOrder()
    {
        var instrument =
            new DesktopRuntimeInstrumentViewModel(
                CreateInstrument(
                    [
                        CreateCommand(
                            "Controller.First",
                            "First",
                            null),
                        CreateCommand(
                            "Controller.Removed",
                            "Removed",
                            null)
                    ]));
        DesktopRuntimeCommandViewModel first =
            instrument.Commands[0];

        instrument.Update(
            CreateInstrument(
                [
                    CreateCommand(
                        "Controller.Added",
                        "Added",
                        null),
                    CreateCommand(
                        "Controller.First",
                        "First",
                        null)
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
    public void Constructor_WithEmptyPath_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            "snapshot",
            () => new DesktopRuntimeCommandViewModel(
                CreateCommand(
                    string.Empty,
                    "Command",
                    null)));
    }

    [Fact]
    public void Constructor_WithEmptyDisplayName_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            "snapshot",
            () => new DesktopRuntimeCommandViewModel(
                CreateCommand(
                    "Controller.Command",
                    string.Empty,
                    null)));
    }

    private static DesktopRuntimeCommandSnapshot CreateCommand(
        string path,
        string displayName,
        string? description) =>
        new(
            CreateTarget(
                path),
            path,
            displayName,
            description,
            IsEndpointReady: true);

    private static RuntimeHostCommandTarget CreateTarget(
        string path) =>
        new(
            new EndpointId("endpoint-1"),
            new RuntimeEndpointAttachmentGeneration(
                Guid.Parse("55e39774-cc7f-4473-8a2e-4bc5bbb79f55")),
            new InstrumentId("instrument-1"),
            new DescriptorPath(
                string.IsNullOrWhiteSpace(path)
                    ? ["Command"]
                    : path.Split('.')));

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
