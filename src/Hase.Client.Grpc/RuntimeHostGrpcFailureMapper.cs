using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text.Json;
using Grpc.Core;

namespace Hase.Client.Grpc;

/// <summary>
/// Maps transport and adapter failures into the normalized client error model.
/// </summary>
public sealed class RuntimeHostGrpcFailureMapper
{
    public RuntimeHostClientException Map(
        Exception exception)
    {
        ArgumentNullException.ThrowIfNull(
            exception);

        if (exception is RuntimeHostClientException normalized)
        {
            return normalized;
        }

        RuntimeHostClientFailureCategory category =
            exception switch
            {
                RpcException rpcException =>
                    MapStatus(
                        rpcException.StatusCode),
                OperationCanceledException =>
                    RuntimeHostClientFailureCategory.Cancelled,
                NotSupportedException =>
                    RuntimeHostClientFailureCategory.ApiCompatibility,
                InvalidDataException =>
                    RuntimeHostClientFailureCategory.InvalidRemoteContract,
                AuthenticationException
                    or CryptographicException
                    or JsonException
                    or UnauthorizedAccessException
                    or FileNotFoundException =>
                    RuntimeHostClientFailureCategory.LocalConfiguration,
                _ =>
                    RuntimeHostClientFailureCategory.Unknown
            };

        return new RuntimeHostClientException(
            category,
            MessageFor(
                category),
            exception);
    }

    private static RuntimeHostClientFailureCategory MapStatus(
        StatusCode statusCode)
    {
        return statusCode switch
        {
            StatusCode.Unauthenticated =>
                RuntimeHostClientFailureCategory.Authentication,
            StatusCode.PermissionDenied =>
                RuntimeHostClientFailureCategory.Authorization,
            StatusCode.DeadlineExceeded =>
                RuntimeHostClientFailureCategory.DeadlineExceeded,
            StatusCode.Cancelled =>
                RuntimeHostClientFailureCategory.Cancelled,
            StatusCode.Unavailable =>
                RuntimeHostClientFailureCategory.TransportUnavailable,
            StatusCode.DataLoss =>
                RuntimeHostClientFailureCategory.ObservationGap,
            _ =>
                RuntimeHostClientFailureCategory.Unknown
        };
    }

    private static string MessageFor(
        RuntimeHostClientFailureCategory category)
    {
        return category switch
        {
            RuntimeHostClientFailureCategory.Authentication =>
                "Runtime-host client authentication failed.",
            RuntimeHostClientFailureCategory.Authorization =>
                "The runtime-host client is not authorized.",
            RuntimeHostClientFailureCategory.ApiCompatibility =>
                "The runtime-host API version is not compatible.",
            RuntimeHostClientFailureCategory.TransportUnavailable =>
                "The runtime-host transport is unavailable.",
            RuntimeHostClientFailureCategory.DeadlineExceeded =>
                "The runtime-host operation deadline was exceeded.",
            RuntimeHostClientFailureCategory.Cancelled =>
                "The runtime-host operation was cancelled.",
            RuntimeHostClientFailureCategory.ObservationGap =>
                "The runtime-host observation subscription has a gap.",
            RuntimeHostClientFailureCategory.InvalidRemoteContract =>
                "The runtime host returned invalid contract data.",
            RuntimeHostClientFailureCategory.LocalConfiguration =>
                "The local runtime-host client configuration is invalid.",
            _ =>
                "The runtime-host client failed."
        };
    }
}
