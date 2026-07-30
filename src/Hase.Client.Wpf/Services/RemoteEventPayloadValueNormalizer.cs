using System.IO;

namespace Hase.Client.Wpf.Services;

/// <summary>
/// Normalizes the remote value union for shared Event payload presentation.
/// </summary>
public static class RemoteEventPayloadValueNormalizer
{
    public static object? Normalize(
        RemoteValue? value)
    {
        if (value is null)
        {
            return null;
        }

        return value.Kind switch
        {
            RemoteValueKind.Boolean =>
                value.BooleanValue
                ?? throw Invalid(
                    value.Kind),
            RemoteValueKind.String =>
                value.StringValue
                ?? throw Invalid(
                    value.Kind),
            RemoteValueKind.Numeric =>
                value.NumericValue
                ?? throw Invalid(
                    value.Kind),
            RemoteValueKind.ByteArray =>
                value.ByteArrayValue
                ?? throw Invalid(
                    value.Kind),
            _ =>
                throw new InvalidDataException(
                    $"The remote Event payload value kind '{value.Kind}' " +
                    "is not supported.")
        };
    }

    private static InvalidDataException Invalid(
        RemoteValueKind kind) =>
        new(
            $"The remote Event payload value kind '{kind}' has no value.");
}
