using Google.Protobuf.WellKnownTypes;
using Hase.Client;
using Hase.Client.Grpc;
using Hase.Core.Domain.Identity;
using GrpcV1 = Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Client.Grpc.Tests;

public sealed class RuntimeHostGrpcPropertyMapperTests
{
    [Fact]
    public void MapRequest_ShouldPreserveCompleteTargetIdentity()
    {
        Guid generation =
            Guid.Parse(
                "7206a38d-d980-4495-bc11-f7cdf9f14ebd");
        var target =
            new RemotePropertyTarget(
                new RemoteEndpointAttachmentKey(
                    new EndpointId(
                        "endpoint-01"),
                    new RemoteEndpointAttachmentGeneration(
                        generation)),
                new InstrumentId(
                    "instrument-01"),
                new PropertyId(
                    "property-01"));

        GrpcV1.ReadAuthoritativePropertyRequest result =
            new RuntimeHostGrpcPropertyMapper().MapRequest(
                target);

        Assert.Equal(
            "endpoint-01",
            result.Target.EndpointId);
        Assert.Equal(
            generation.ToString(
                "D"),
            result.Target.AttachmentGeneration);
        Assert.Equal(
            "instrument-01",
            result.Target.InstrumentId);
        Assert.Equal(
            "property-01",
            result.Target.PropertyId);
    }

    [Fact]
    public void MapResult_Success_ShouldMapConfirmedValue()
    {
        DateTimeOffset timestampUtc =
            new(
                2026,
                7,
                27,
                12,
                30,
                0,
                TimeSpan.Zero);
        var source =
            new GrpcV1.PropertyOperationResult
            {
                Status =
                    GrpcV1.PropertyOperationStatus.Success,
                ConfirmedValue =
                    new GrpcV1.PropertyValue
                    {
                        Value =
                            new GrpcV1.RemoteValue
                            {
                                NumericValue =
                                    23.75
                            },
                        TimestampUtc =
                            Timestamp.FromDateTimeOffset(
                                timestampUtc),
                        Quality =
                            GrpcV1.PropertyQuality.Good
                    }
            };

        RemotePropertyOperationResult result =
            new RuntimeHostGrpcPropertyMapper().MapResult(
                source);

        Assert.True(
            result.IsSuccess);
        Assert.Equal(
            23.75,
            result.ConfirmedValue!.Value!.NumericValue!.Value);
        Assert.Equal(
            timestampUtc,
            result.ConfirmedValue.TimestampUtc);
        Assert.Equal(
            RemotePropertyQuality.Good,
            result.ConfirmedValue.Quality);
    }

    [Fact]
    public void MapResult_Failure_ShouldPreserveStatusAndDiagnostic()
    {
        var source =
            new GrpcV1.PropertyOperationResult
            {
                Status =
                    GrpcV1.PropertyOperationStatus.EndpointUnavailable,
                Diagnostic =
                    " Endpoint unavailable. "
            };

        RemotePropertyOperationResult result =
            new RuntimeHostGrpcPropertyMapper().MapResult(
                source);

        Assert.False(
            result.IsSuccess);
        Assert.Equal(
            RemotePropertyOperationStatus.EndpointUnavailable,
            result.Status);
        Assert.Equal(
            "Endpoint unavailable.",
            result.Diagnostic);
    }
}
