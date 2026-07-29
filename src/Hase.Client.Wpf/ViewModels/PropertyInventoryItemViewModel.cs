using Hase.Core.Domain.Data;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Operator.Input;
using Prism.Mvvm;

namespace Hase.Client.Wpf.ViewModels;

public sealed class PropertyInventoryItemViewModel
    : BindableBase
{
    private bool requestedBooleanValue;
    private string requestedValueText;
    private bool isEditingRequestedValue;

    public PropertyInventoryItemViewModel(
        RemotePropertyTarget target,
        string propertyId,
        string path,
        string displayName,
        string accessMode,
        string dataType,
        string? unit,
        string value,
        string? timestampUtc,
        string? quality,
        bool isStale,
        bool supportsRead,
        bool canRead,
        bool supportsBooleanWrite,
        bool canWrite,
        PropertyDescriptor? descriptor = null,
        PropertyInputEditorKind editorKind =
            PropertyInputEditorKind.None,
        string requestedValueText = "")
    {
        Target =
            target
            ?? throw new ArgumentNullException(
                nameof(target));
        PropertyId =
            propertyId;
        Path =
            path;
        DisplayName =
            displayName;
        AccessMode =
            accessMode;
        DataType =
            dataType;
        Unit =
            unit;
        Value =
            value;
        TimestampUtc =
            timestampUtc;
        Quality =
            quality;
        IsStale =
            isStale;
        SupportsRead =
            supportsRead;
        CanRead =
            canRead;
        SupportsBooleanWrite =
            supportsBooleanWrite;
        CanWrite =
            canWrite;
        Descriptor =
            descriptor
            ?? CreateCompatibilityDescriptor(
                propertyId,
                path,
                displayName,
                dataType,
                supportsBooleanWrite);
        EditorKind =
            editorKind != PropertyInputEditorKind.None
                ? editorKind
                : supportsBooleanWrite
                    ? PropertyInputEditorKind.Boolean
                    : PropertyInputEditorKind.None;
        this.requestedValueText =
            requestedValueText;
    }

    public RemotePropertyTarget Target
    {
        get;
    }

    public string PropertyId
    {
        get;
    }

    public string Path
    {
        get;
    }

    public string DisplayName
    {
        get;
    }

    public string AccessMode
    {
        get;
    }

    public string DataType
    {
        get;
    }

    public string? Unit
    {
        get;
    }

    public string Value
    {
        get;
    }

    public string? TimestampUtc
    {
        get;
    }

    public string? Quality
    {
        get;
    }

    public bool IsStale
    {
        get;
    }

    public bool SupportsRead
    {
        get;
    }

    public bool CanRead
    {
        get;
    }

    public bool SupportsBooleanWrite
    {
        get;
    }

    public bool CanWrite
    {
        get;
    }

    public PropertyDescriptor Descriptor
    {
        get;
    }

    public PropertyInputEditorKind EditorKind
    {
        get;
    }

    public bool HasEditor =>
        EditorKind != PropertyInputEditorKind.None;

    public bool HasBooleanEditor =>
        EditorKind == PropertyInputEditorKind.Boolean;

    public bool HasTextEditor =>
        EditorKind == PropertyInputEditorKind.Text;

    public bool RequestedBooleanValue
    {
        get =>
            requestedBooleanValue;
        set
        {
            if (SetProperty(
                    ref requestedBooleanValue,
                    value))
            {
                RaiseValidationChanged();
            }
        }
    }

    public string RequestedValueText
    {
        get =>
            requestedValueText;
        set
        {
            ArgumentNullException.ThrowIfNull(
                value);

            if (SetProperty(
                    ref requestedValueText,
                    value))
            {
                RaiseValidationChanged();
            }
        }
    }

    public bool IsEditingRequestedValue
    {
        get =>
            isEditingRequestedValue;
        set =>
            SetProperty(
                ref isEditingRequestedValue,
                value);
    }

    public PropertyInputParseResult InputResult =>
        PropertyInputParser.Parse(
            Descriptor,
            GetInputText());

    public bool HasValidRequestedValue =>
        HasEditor
        && InputResult.IsSuccess;

    public string ValidationMessage =>
        HasEditor
        && !InputResult.IsSuccess
            ? InputResult.Message
            : string.Empty;

    public bool CanSubmitWrite =>
        CanWrite
        && HasValidRequestedValue;

    private string GetInputText()
    {
        return HasBooleanEditor
            ? RequestedBooleanValue.ToString()
            : RequestedValueText;
    }

    private void RaiseValidationChanged()
    {
        RaisePropertyChanged(
            nameof(InputResult));
        RaisePropertyChanged(
            nameof(HasValidRequestedValue));
        RaisePropertyChanged(
            nameof(ValidationMessage));
        RaisePropertyChanged(
            nameof(CanSubmitWrite));
    }

    private static PropertyDescriptor CreateCompatibilityDescriptor(
        string propertyId,
        string path,
        string displayName,
        string dataType,
        bool supportsBooleanWrite)
    {
        DataDescriptor data =
            dataType switch
            {
                "Boolean" =>
                    new BooleanDataDescriptor(),
                "Numeric" =>
                    new NumericDataDescriptor(
                        Quantities.Temperature,
                        Units.Celsius),
                "String" =>
                    new StringDataDescriptor(),
                "ByteArray" =>
                    new ByteArrayDataDescriptor(),
                _ =>
                    new StringDataDescriptor()
            };

        return new PropertyDescriptor(
            new PropertyId(
                propertyId),
            DescriptorPath.Parse(
                path),
            displayName,
            data)
        {
            AccessMode =
                supportsBooleanWrite
                    ? PropertyAccessMode.ReadWrite
                    : PropertyAccessMode.Read
        };
    }
}
