using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostCommandTargetMapperTests
{
    [Fact]
    public void Map_NullSource_ShouldThrow()
    {
        var mapper =
            new RuntimeHostCommandTargetMapper();

        Assert.Throws<ArgumentNullException>(
            "source",
            () =>
                mapper.Map(
                    null!));
    }

    [Fact]
    public void Map_DefinedTarget_ShouldPreserveGenerationScopedIdentity()
    {
        var attachmentGeneration =
            new Guid(
                "868e79d4-b1a4-4a63-81cd-5a800d9ba3fd");
        var source =
            new GrpcV1.CommandTarget
            {
                EndpointId =
                    "endpoint-01",
                AttachmentGeneration =
                    attachmentGeneration.ToString(
                        "D"),
                InstrumentId =
                    "environment-sensor-01"
            };

        source.CommandPathSegments.Add(
            "Calibration");
        source.CommandPathSegments.Add(
            "Reset");

        var mapper =
            new RuntimeHostCommandTargetMapper();

        Northbound.RuntimeHostCommandTarget result =
            mapper.Map(
                source);

        Assert.Equal(
            source.EndpointId,
            result.EndpointId.Value);
        Assert.Equal(
            attachmentGeneration,
            result.AttachmentGeneration.Value);
        Assert.Equal(
            source.InstrumentId,
            result.InstrumentId.Value);
        Assert.Equal(
            source.CommandPathSegments,
            result.CommandPath.Segments);
    }

    [Fact]
    public void Map_InvalidAttachmentGeneration_ShouldThrow()
    {
        var source =
            new GrpcV1.CommandTarget
            {
                EndpointId =
                    "endpoint-01",
                AttachmentGeneration =
                    "not-a-guid",
                InstrumentId =
                    "environment-sensor-01"
            };

        source.CommandPathSegments.Add(
            "Reset");

        var mapper =
            new RuntimeHostCommandTargetMapper();

        Assert.Throws<FormatException>(
            () =>
                mapper.Map(
                    source));
    }

    [Fact]
    public void Map_EmptyCommandPath_ShouldThrow()
    {
        var source =
            new GrpcV1.CommandTarget
            {
                EndpointId =
                    "endpoint-01",
                AttachmentGeneration =
                    new Guid(
                            "868e79d4-b1a4-4a63-81cd-5a800d9ba3fd")
                        .ToString(
                            "D"),
                InstrumentId =
                    "environment-sensor-01"
            };

        var mapper =
            new RuntimeHostCommandTargetMapper();

        Assert.Throws<ArgumentException>(
            "segments",
            () =>
                mapper.Map(
                    source));
    }
}
