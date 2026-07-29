using Hase.Core.Domain.Data;

namespace Hase.Client.Wpf.Services;

public static class PropertyInputRemoteValueMapper
{
    public static RemoteValue Map(
        object value)
    {
        ArgumentNullException.ThrowIfNull(
            value);

        return value switch
        {
            bool boolean =>
                RemoteValue.FromBoolean(
                    boolean),
            double numeric =>
                RemoteValue.FromNumeric(
                    numeric),
            string text =>
                RemoteValue.FromString(
                    text),
            ByteArrayValue bytes =>
                RemoteValue.FromByteArray(
                    bytes),
            _ =>
                throw new ArgumentException(
                    "The parsed Property value type is not supported by "
                    + "the remote API.",
                    nameof(value))
        };
    }
}
