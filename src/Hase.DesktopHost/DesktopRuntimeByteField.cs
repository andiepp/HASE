using System.Collections.ObjectModel;

namespace Hase.DesktopHost;

/// <summary>
/// Represents one immutable interpreted range of captured diagnostic bytes.
/// </summary>
public sealed class DesktopRuntimeByteField
{
    private readonly ReadOnlyCollection<byte> bytes;

    public DesktopRuntimeByteField(
        int offset,
        int length,
        string name,
        string interpretedValue,
        ReadOnlySpan<byte> capturedBytes,
        DesktopRuntimeByteFieldValidation validation =
            DesktopRuntimeByteFieldValidation.NotApplicable)
    {
        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(offset));
        }

        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(length));
        }

        if (capturedBytes.Length > length)
        {
            throw new ArgumentException(
                "Captured field bytes must not exceed the declared field length.",
                nameof(capturedBytes));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Field name must not be empty.",
                nameof(name));
        }

        ArgumentNullException.ThrowIfNull(
            interpretedValue);

        if (!Enum.IsDefined(validation))
        {
            throw new ArgumentOutOfRangeException(
                nameof(validation));
        }

        Offset = offset;
        Length = length;
        Name = name.Trim();
        InterpretedValue = interpretedValue;
        Validation = validation;

        bytes =
            Array.AsReadOnly(
                capturedBytes.ToArray());
    }

    public int Offset { get; }

    public int Length { get; }

    public string Name { get; }

    public string InterpretedValue { get; }

    public DesktopRuntimeByteFieldValidation Validation { get; }

    public IReadOnlyList<byte> Bytes => bytes;

    public string ByteHex =>
        DesktopRuntimeByteFormatting.FormatHex(
            bytes);
}
