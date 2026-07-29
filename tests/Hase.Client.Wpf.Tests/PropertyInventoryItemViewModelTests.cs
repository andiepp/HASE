using Hase.Client.Wpf.ViewModels;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;

namespace Hase.Client.Wpf.Tests;

public sealed class PropertyInventoryItemViewModelTests
{
    [Fact]
    public void NumericEditor_InvalidThenValidInput_ShouldNotifyValidation()
    {
        PropertyInventoryItemViewModel viewModel =
            CreateTextEditor(
                new NumericDataDescriptor(
                    Quantities.Temperature,
                    Units.Celsius,
                    new ValueRange(
                        -10,
                        50)),
                "23,5");
        var changed =
            new List<string?>();
        viewModel.PropertyChanged +=
            (_, eventArgs) =>
                changed.Add(
                    eventArgs.PropertyName);

        Assert.False(
            viewModel.HasValidRequestedValue);
        Assert.False(
            viewModel.CanSubmitWrite);

        viewModel.RequestedValueText =
            "23.5";

        Assert.True(
            viewModel.HasValidRequestedValue);
        Assert.True(
            viewModel.CanSubmitWrite);
        Assert.Contains(
            nameof(viewModel.ValidationMessage),
            changed);
        Assert.Contains(
            nameof(viewModel.CanSubmitWrite),
            changed);
    }

    [Fact]
    public void StringEditor_WhitespaceInput_ShouldRemainValidAndExact()
    {
        PropertyInventoryItemViewModel viewModel =
            CreateTextEditor(
                new StringDataDescriptor(),
                "  exact text  ");

        Assert.True(
            viewModel.HasValidRequestedValue);
        Assert.Equal(
            "  exact text  ",
            Assert.IsType<string>(
                viewModel.InputResult.Value));
    }

    [Fact]
    public void ByteArrayEditor_InvalidInput_ShouldExposeSharedMessage()
    {
        PropertyInventoryItemViewModel viewModel =
            CreateTextEditor(
                new ByteArrayDataDescriptor(),
                "00 53 FFF");

        Assert.False(
            viewModel.HasValidRequestedValue);
        Assert.Equal(
            "Enter complete hexadecimal bytes, for example: 00 53 FF.",
            viewModel.ValidationMessage);
    }

    [Fact]
    public void BooleanEditor_ShouldProduceIndependentRequestedValue()
    {
        PropertyDescriptor descriptor =
            CreateDescriptor(
                new BooleanDataDescriptor());
        PropertyInventoryItemViewModel viewModel =
            CreateViewModel(
                descriptor,
                PropertyInputEditorKind.Boolean,
                string.Empty);

        viewModel.RequestedBooleanValue =
            true;

        Assert.True(
            viewModel.HasBooleanEditor);
        Assert.True(
            viewModel.HasValidRequestedValue);
        Assert.True(
            Assert.IsType<bool>(
                viewModel.InputResult.Value));
        Assert.Equal(
            "False",
            viewModel.Value);
    }

    private static PropertyInventoryItemViewModel CreateTextEditor(
        DataDescriptor data,
        string requestedValueText)
    {
        return CreateViewModel(
            CreateDescriptor(
                data),
            PropertyInputEditorKind.Text,
            requestedValueText);
    }

    private static PropertyInventoryItemViewModel CreateViewModel(
        PropertyDescriptor descriptor,
        PropertyInputEditorKind editorKind,
        string requestedValueText)
    {
        var target =
            new RemotePropertyTarget(
                new RemoteEndpointAttachmentKey(
                    new EndpointId(
                        "endpoint-01"),
                    new RemoteEndpointAttachmentGeneration(
                        Guid.Parse(
                            "31bda489-b8ec-49bf-bf69-1947b13e37cd"))),
                new InstrumentId(
                    "instrument-01"),
                descriptor.Id);

        return new PropertyInventoryItemViewModel(
            target,
            descriptor.Id.Value,
            descriptor.Path.ToString(),
            descriptor.DisplayName,
            descriptor.AccessMode.ToString(),
            descriptor.Data.GetType().Name,
            null,
            "False",
            null,
            null,
            false,
            true,
            true,
            descriptor.Data is BooleanDataDescriptor,
            true,
            descriptor,
            editorKind,
            requestedValueText);
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
