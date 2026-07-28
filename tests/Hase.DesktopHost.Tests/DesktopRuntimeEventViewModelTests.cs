using Hase.DesktopHost.App.ViewModels;

namespace Hase.DesktopHost.Tests;

public sealed class DesktopRuntimeEventViewModelTests
{
    [Fact]
    public void Constructor_ShouldProjectDescriptorMetadata()
    {
        var viewModel =
            new DesktopRuntimeEventViewModel(
                CreateEvent(
                    "Controller.ButtonPressed",
                    "Button pressed",
                    "Raised when the controller button is pressed."));

        Assert.Equal(
            "Controller.ButtonPressed",
            viewModel.Path);
        Assert.Equal(
            "Button pressed",
            viewModel.DisplayName);
        Assert.Equal(
            "Raised when the controller button is pressed.",
            viewModel.Description);
    }

    [Fact]
    public void InstrumentUpdate_WithUnchangedDescriptor_ShouldPreserveEventViewModel()
    {
        DesktopRuntimeEventSnapshot eventSnapshot =
            CreateEvent(
                "Controller.ButtonPressed",
                "Button pressed",
                "Raised when the controller button is pressed.");
        var instrument =
            new DesktopRuntimeInstrumentViewModel(
                CreateInstrument(
                    [eventSnapshot]));
        DesktopRuntimeEventViewModel original =
            instrument.Events[0];

        instrument.Update(
            CreateInstrument(
                [eventSnapshot]));

        Assert.Same(
            original,
            instrument.Events[0]);
    }

    [Fact]
    public void InstrumentUpdate_WithChangedMetadata_ShouldReplaceEventViewModel()
    {
        var instrument =
            new DesktopRuntimeInstrumentViewModel(
                CreateInstrument(
                    [
                        CreateEvent(
                            "Controller.ButtonPressed",
                            "Button pressed",
                            null)
                    ]));
        DesktopRuntimeEventViewModel original =
            instrument.Events[0];

        instrument.Update(
            CreateInstrument(
                [
                    CreateEvent(
                        "Controller.ButtonPressed",
                        "Controller button pressed",
                        "Updated description.")
                ]));

        Assert.NotSame(
            original,
            instrument.Events[0]);
        Assert.Equal(
            "Controller button pressed",
            instrument.Events[0].DisplayName);
        Assert.Equal(
            "Updated description.",
            instrument.Events[0].Description);
    }

    [Fact]
    public void InstrumentUpdate_ShouldAddRemoveAndPreserveDescriptorOrder()
    {
        var instrument =
            new DesktopRuntimeInstrumentViewModel(
                CreateInstrument(
                    [
                        CreateEvent(
                            "Controller.First",
                            "First",
                            null),
                        CreateEvent(
                            "Controller.Removed",
                            "Removed",
                            null)
                    ]));
        DesktopRuntimeEventViewModel first =
            instrument.Events[0];

        instrument.Update(
            CreateInstrument(
                [
                    CreateEvent(
                        "Controller.Added",
                        "Added",
                        null),
                    CreateEvent(
                        "Controller.First",
                        "First",
                        null)
                ]));

        Assert.Equal(
            2,
            instrument.EventCount);
        Assert.Equal(
            ["Controller.Added", "Controller.First"],
            instrument.Events
                .Select(
                    eventViewModel =>
                        eventViewModel.Path)
                .ToArray());
        Assert.Same(
            first,
            instrument.Events[1]);
    }

    [Fact]
    public void Constructor_WithEmptyPath_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            "snapshot",
            () =>
                new DesktopRuntimeEventViewModel(
                    CreateEvent(
                        string.Empty,
                        "Event",
                        null)));
    }

    [Fact]
    public void Constructor_WithEmptyDisplayName_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            "snapshot",
            () =>
                new DesktopRuntimeEventViewModel(
                    CreateEvent(
                        "Controller.Event",
                        string.Empty,
                        null)));
    }

    private static DesktopRuntimeEventSnapshot CreateEvent(
        string path,
        string displayName,
        string? description) =>
        new(
            path,
            displayName,
            description);

    private static DesktopRuntimeInstrumentSnapshot CreateInstrument(
        IReadOnlyList<DesktopRuntimeEventSnapshot> events) =>
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
            Events =
                events
        };
}
