using Hase.DesktopHost.App.ViewModels;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Northbound;

namespace Hase.DesktopHost.Tests;

public sealed class DesktopRuntimePropertyViewModelTests
{
    [Fact]
    public void Constructor_WithWritableBoolean_ShouldInitializeIndependentRequestedValue()
    {
        var viewModel =
            new DesktopRuntimePropertyViewModel(
                CreateBooleanProperty(
                    value: false,
                    "2026-07-28T10:00:00.0000000+00:00"));

        Assert.Equal(
            DesktopRuntimePropertyDataKind.Boolean,
            viewModel.DataKind);
        Assert.True(
            viewModel.CanWrite);
        Assert.True(
            viewModel.HasBooleanEditor);
        Assert.False(
            viewModel.CurrentBooleanValue);
        Assert.False(
            viewModel.RequestedBooleanValue);
    }

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

    [Fact]
    public void Update_AfterOperatorEdit_ShouldNotOverwriteRequestedBooleanValue()
    {
        var viewModel =
            new DesktopRuntimePropertyViewModel(
                CreateBooleanProperty(
                    value: false,
                    "2026-07-28T10:00:00.0000000+00:00"));

        viewModel.RequestedBooleanValue =
            true;

        viewModel.Update(
            CreateBooleanProperty(
                value: false,
                "2026-07-28T10:00:01.0000000+00:00"));

        Assert.False(
            viewModel.CurrentBooleanValue);
        Assert.True(
            viewModel.RequestedBooleanValue);
    }

    [Fact]
    public void Update_WhenAuthoritativeBooleanChanges_ShouldNotChangeRequestedValue()
    {
        var viewModel =
            new DesktopRuntimePropertyViewModel(
                CreateBooleanProperty(
                    value: false,
                    "2026-07-28T10:00:00.0000000+00:00"));

        viewModel.Update(
            CreateBooleanProperty(
                value: true,
                "2026-07-28T10:00:01.0000000+00:00"));

        Assert.True(
            viewModel.CurrentBooleanValue);
        Assert.False(
            viewModel.RequestedBooleanValue);
    }

    [Fact]
    public void ResetRequestedValueCommand_ShouldCopyCurrentAuthoritativeValue()
    {
        var viewModel =
            new DesktopRuntimePropertyViewModel(
                CreateBooleanProperty(
                    value: false,
                    "2026-07-28T10:00:00.0000000+00:00"));

        viewModel.RequestedBooleanValue =
            true;

        viewModel.ResetRequestedValueCommand.Execute();

        Assert.False(
            viewModel.RequestedBooleanValue);
    }

    [Fact]
    public void ReadOnlyBoolean_ShouldNotExposeBooleanEditor()
    {
        DesktopRuntimePropertySnapshot snapshot =
            CreateBooleanProperty(
                value: true,
                "2026-07-28T10:00:00.0000000+00:00")
            with
            {
                Access =
                    "Read",
                CanWrite =
                    false
            };

        var viewModel =
            new DesktopRuntimePropertyViewModel(
                snapshot);

        Assert.False(
            viewModel.HasBooleanEditor);
        Assert.Null(
            viewModel.RequestedBooleanValue);
        Assert.False(
            viewModel.ResetRequestedValueCommand.CanExecute());
    }

    [Fact]
    public void Update_WithIncompatibleDataKind_ShouldClearRequestedBooleanValue()
    {
        var viewModel =
            new DesktopRuntimePropertyViewModel(
                CreateBooleanProperty(
                    value: false,
                    "2026-07-28T10:00:00.0000000+00:00"));

        viewModel.RequestedBooleanValue =
            true;

        viewModel.Update(
            CreateProperty(
                "42",
                "2026-07-28T10:00:01.0000000+00:00"));

        Assert.False(
            viewModel.HasBooleanEditor);
        Assert.Null(
            viewModel.RequestedBooleanValue);
    }

    private static DesktopRuntimePropertySnapshot CreateProperty(
        string value,
        string timestampUtc) =>
        new(
            CreateTarget(),
            "property-1",
            "Built-in LED state",
            "Led.State",
            "ReadWrite",
            value,
            "Good",
            timestampUtc,
            IsKnown: true,
            DesktopRuntimePropertyDataKind.Numeric,
            CanRead: true,
            CanWrite: true,
            BooleanValue: null,
            IsEndpointReady: true);

    private static DesktopRuntimePropertySnapshot CreateBooleanProperty(
        bool value,
        string timestampUtc) =>
        new(
            CreateTarget(),
            "property-1",
            "Built-in LED state",
            "Led.State",
            "ReadWrite",
            value.ToString(),
            "Good",
            timestampUtc,
            IsKnown: true,
            DesktopRuntimePropertyDataKind.Boolean,
            CanRead: true,
            CanWrite: true,
            BooleanValue: value,
            IsEndpointReady: true);

    private static RuntimeHostPropertyTarget CreateTarget() =>
        new(
            new EndpointId("endpoint-1"),
            new RuntimeEndpointAttachmentGeneration(
                Guid.Parse("18716b64-519c-42c4-af5f-8238f5c24015")),
            new InstrumentId("instrument-1"),
            new PropertyId("property-1"));

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
