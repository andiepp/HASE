using System.Security.Authentication;
using Grpc.Core;
using Hase.Client;
using Hase.Client.Grpc;

namespace Hase.Client.Grpc.Tests;

public sealed class RuntimeHostGrpcFailureMapperTests
{
    [Theory]
    [InlineData(
        StatusCode.Unauthenticated,
        RuntimeHostClientFailureCategory.Authentication)]
    [InlineData(
        StatusCode.PermissionDenied,
        RuntimeHostClientFailureCategory.Authorization)]
    [InlineData(
        StatusCode.DeadlineExceeded,
        RuntimeHostClientFailureCategory.DeadlineExceeded)]
    [InlineData(
        StatusCode.Cancelled,
        RuntimeHostClientFailureCategory.Cancelled)]
    [InlineData(
        StatusCode.Unavailable,
        RuntimeHostClientFailureCategory.TransportUnavailable)]
    [InlineData(
        StatusCode.DataLoss,
        RuntimeHostClientFailureCategory.ObservationGap)]
    [InlineData(
        StatusCode.Internal,
        RuntimeHostClientFailureCategory.Unknown)]
    public void Map_RpcStatus_ShouldMapCategory(
        StatusCode statusCode,
        RuntimeHostClientFailureCategory expected)
    {
        RuntimeHostClientException result =
            new RuntimeHostGrpcFailureMapper().Map(
                new RpcException(
                    new Status(
                        statusCode,
                        "transport detail")));

        Assert.Equal(
            expected,
            result.Category);
        Assert.DoesNotContain(
            "transport detail",
            result.Message,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(typeof(OperationCanceledException), RuntimeHostClientFailureCategory.Cancelled)]
    [InlineData(typeof(NotSupportedException), RuntimeHostClientFailureCategory.ApiCompatibility)]
    [InlineData(typeof(InvalidDataException), RuntimeHostClientFailureCategory.InvalidRemoteContract)]
    [InlineData(typeof(AuthenticationException), RuntimeHostClientFailureCategory.LocalConfiguration)]
    [InlineData(typeof(Exception), RuntimeHostClientFailureCategory.Unknown)]
    public void Map_LocalException_ShouldMapCategory(
        Type exceptionType,
        RuntimeHostClientFailureCategory expected)
    {
        var exception =
            (Exception)Activator.CreateInstance(
                exceptionType)!;

        RuntimeHostClientException result =
            new RuntimeHostGrpcFailureMapper().Map(
                exception);

        Assert.Equal(
            expected,
            result.Category);
        Assert.Same(
            exception,
            result.InnerException);
    }

    [Fact]
    public void Map_NormalizedFailure_ShouldPreserveInstance()
    {
        var expected =
            new RuntimeHostClientException(
                RuntimeHostClientFailureCategory.Authorization,
                "Denied.");

        RuntimeHostClientException result =
            new RuntimeHostGrpcFailureMapper().Map(
                expected);

        Assert.Same(
            expected,
            result);
    }
}
