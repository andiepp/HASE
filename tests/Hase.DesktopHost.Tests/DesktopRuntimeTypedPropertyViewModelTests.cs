using Hase.Core.Domain.Data;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.DesktopHost.App.ViewModels;
using Hase.Runtime.Northbound;

namespace Hase.DesktopHost.Tests;

public sealed class DesktopRuntimeTypedPropertyViewModelTests
{
    [Fact]
    public void NumericEditor_ShouldApplyDescriptorRange()
    {
        DesktopRuntimePropertyViewModel viewModel =
            Create(
                new NumericDataDescriptor(
                    Quantities.Temperature,
                    Units.Celsius,
                    new ValueRange(
                        -10,
                        50)),
                20.0);

        viewModel.RequestedValueText =
            "51";

        Assert.True(
            viewModel.HasTextEditor);
        Assert.False(
            viewModel.HasValidRequestedValue);
        Assert.Contains(
            "between -10 and 50",
            viewModel.ValidationMessage);
    }

    [Fact]
    public void StringEditor_ShouldPreserveExactRequestedValue()
    {
        DesktopRuntimePropertyViewModel viewModel =
            Create(
                new StringDataDescriptor(),
                "current");

        viewModel.RequestedValueText =
            "  exact text  ";
        DesktopRuntimePropertyWriteRequest request =
            Assert.IsType<DesktopRuntimePropertyWriteRequest>(
                viewModel.TryBeginWrite());

        Assert.Equal(
            "  exact text  ",
            request.RequestedValue);
        Assert.Equal(
            "  exact text  ",
            request.InputSummary);
    }

    [Fact]
    public void ByteArrayEditor_ShouldCaptureExactBytesAndSummary()
    {
        DesktopRuntimePropertyViewModel viewModel =
            Create(
                new ByteArrayDataDescriptor(),
                new ByteArrayValue(
                    new byte[]
                    {
                        0x01
                    }));

        viewModel.RequestedValueText =
            "00 53 FF";
        DesktopRuntimePropertyWriteRequest request =
            Assert.IsType<DesktopRuntimePropertyWriteRequest>(
                viewModel.TryBeginWrite());

        Assert.Equal(
            new byte[]
            {
                0x00,
                0x53,
                0xFF
            },
            Assert.IsType<ByteArrayValue>(
                request.RequestedValue).ToArray());
        Assert.Equal(
            "00 53 FF",
            request.InputSummary);
    }

    [Fact]
    public void AuthoritativeUpdate_ShouldNotOverwriteRequestedText()
    {
        PropertyDescriptor descriptor =
            CreateDescriptor(
                new StringDataDescriptor());
        DesktopRuntimePropertyViewModel viewModel =
            Create(
                descriptor,
                "current");
        viewModel.RequestedValueText =
            "operator input";

        viewModel.Update(
            CreateSnapshot(
                descriptor,
                "new current"));

        Assert.Equal(
            "operator input",
            viewModel.RequestedValueText);
        Assert.Equal(
            "new current",
            viewModel.CurrentTypedValue);
    }

    private static DesktopRuntimePropertyViewModel Create(
        DataDescriptor data,
        object currentValue)
    {
        return Create(
            CreateDescriptor(
                data),
            currentValue);
    }

    private static DesktopRuntimePropertyViewModel Create(
        PropertyDescriptor descriptor,
        object currentValue)
    {
        return new DesktopRuntimePropertyViewModel(
            CreateSnapshot(
                descriptor,
                currentValue));
    }

    private static DesktopRuntimePropertySnapshot CreateSnapshot(
        PropertyDescriptor descriptor,
        object currentValue)
    {
        var target =
            new RuntimeHostPropertyTarget(
                new EndpointId(
                    "endpoint-01"),
                new RuntimeEndpointAttachmentGeneration(
                    Guid.Parse(
                        "09c247f2-1e53-46b3-93be-df28499f3e32")),
                new InstrumentId(
                    "instrument-01"),
                descriptor.Id);

        return new DesktopRuntimePropertySnapshot(
            target,
            descriptor.Id.Value,
            descriptor.DisplayName,
            descriptor.Path.ToString(),
            descriptor.AccessMode.ToString(),
            currentValue.ToString()!,
            "Good",
            "2026-07-29T12:00:00.0000000+00:00",
            true,
            descriptor.Data switch
            {
                BooleanDataDescriptor =>
                    DesktopRuntimePropertyDataKind.Boolean,
                NumericDataDescriptor =>
                    DesktopRuntimePropertyDataKind.Numeric,
                StringDataDescriptor =>
                    DesktopRuntimePropertyDataKind.String,
                ByteArrayDataDescriptor =>
                    DesktopRuntimePropertyDataKind.ByteArray,
                _ =>
                    DesktopRuntimePropertyDataKind.Unknown
            },
            true,
            true,
            currentValue is bool boolean
                ? boolean
                : null,
            true,
            descriptor,
            currentValue);
    }

    private static PropertyDescriptor CreateDescriptor(
        DataDescriptor data)
    {
        return new PropertyDescriptor(
            new PropertyId(
                "property-01"),
            new DescriptorPath(
                "Property",
                "Value"),
            "Property",
            data)
        {
            AccessMode =
                PropertyAccessMode.ReadWrite
        };
    }
}
