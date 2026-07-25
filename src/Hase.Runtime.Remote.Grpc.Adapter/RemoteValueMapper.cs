using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps the closed set of version 1 normalized CLR values to the remote value
/// union.
/// </summary>
public sealed class RemoteValueMapper
    : IRemoteValueMapper
{
    /// <inheritdoc />
    public GrpcV1.RemoteValue Map(
        object value)
    {
        ArgumentNullException.ThrowIfNull(
            value);

        return value switch
        {
            bool booleanValue =>
                new GrpcV1.RemoteValue
                {
                    BooleanValue =
                        booleanValue
                },
            string stringValue =>
                new GrpcV1.RemoteValue
                {
                    StringValue =
                        stringValue
                },
            byte numericValue =>
                CreateNumeric(
                    numericValue),
            sbyte numericValue =>
                CreateNumeric(
                    numericValue),
            short numericValue =>
                CreateNumeric(
                    numericValue),
            ushort numericValue =>
                CreateNumeric(
                    numericValue),
            int numericValue =>
                CreateNumeric(
                    numericValue),
            uint numericValue =>
                CreateNumeric(
                    numericValue),
            long numericValue =>
                CreateNumeric(
                    numericValue),
            ulong numericValue =>
                CreateNumeric(
                    numericValue),
            float numericValue =>
                CreateNumeric(
                    numericValue),
            double numericValue =>
                CreateNumeric(
                    numericValue),
            decimal numericValue =>
                CreateNumeric(
                    Convert.ToDouble(
                        numericValue)),
            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "The CLR value type is not supported by remote API version 1.")
        };
    }

    /// <inheritdoc />
    public object? MapToClr(
        GrpcV1.RemoteValue value)
    {
        ArgumentNullException.ThrowIfNull(
            value);

        return value.KindCase switch
        {
            GrpcV1.RemoteValue.KindOneofCase.None =>
                null,
            GrpcV1.RemoteValue.KindOneofCase.BooleanValue =>
                value.BooleanValue,
            GrpcV1.RemoteValue.KindOneofCase.StringValue =>
                value.StringValue,
            GrpcV1.RemoteValue.KindOneofCase.NumericValue =>
                value.NumericValue,
            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value.KindCase,
                    "The remote value variant is not supported.")
        };
    }

    private static GrpcV1.RemoteValue CreateNumeric(
        double value)
    {
        return new GrpcV1.RemoteValue
        {
            NumericValue =
                value
        };
    }
}
