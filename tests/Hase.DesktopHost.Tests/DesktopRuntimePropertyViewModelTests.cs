using Hase.DesktopHost.App.ViewModels;

namespace Hase.DesktopHost.Tests;

public sealed class DesktopRuntimePropertyViewModelTests
{
    [Fact]
    public void Update_ShouldApplyNewValueAndBeginHighlight()
    {
        var viewModel =
            new DesktopRuntimePropertyViewModel(
                CreateProperty(
                    "Off",
                    "2026-07-28T10:00:00.0000000+00:00"));

        viewModel.Update(
            CreateProperty(
                "On",
                "2026-07-28T10:00:01.0000000+00:00"));

        Assert.Equal(
            "On",
            viewModel.Value);
        Assert.True(
            viewModel.IsRecentlyChanged);
    }

    [Fact]
    public void InstrumentUpdate_ShouldPreservePropertyViewModel()
    {
        var instrument =
            new DesktopRuntimeInstrumentViewModel(
                CreateInstrument(
                    CreateProperty(
                        "Off",
                        "2026-07-28T10:00:00.0000000+00:00")));
        DesktopRuntimePropertyViewModel original =
            instrument.Properties[0];

        instrument.Update(
            CreateInstrument(
                CreateProperty(
                    "On",
                    "2026-07-28T10:00:01.0000000+00:00")));

        Assert.Same(
            original,
            instrument.Properties[0]);
        Assert.Equal(
            "On",
            original.Value);
        Assert.True(
            original.IsRecentlyChanged);
    }

    private static DesktopRuntimePropertySnapshot CreateProperty(
        string value,
        string timestampUtc) =>
        new(
            "property-1",
            "Built-in LED state",
            "Led.State",
            "ReadWrite",
            value,
            "Good",
            timestampUtc,
            IsKnown: true);

    private static DesktopRuntimeInstrumentSnapshot CreateInstrument(
        DesktopRuntimePropertySnapshot property) =>
        new(
            "instrument-1",
            "Controller",
            "Controller",
            "Arduino",
            "Uno",
            null,
            null,
            null,
            null)
        {
            Properties =
                [property]
        };
}
