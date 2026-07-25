using Google.Protobuf.WellKnownTypes;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostEventOccurredObservationPayloadMapperTests
{
    [Fact]
    public void Constructor_NullRemoteValueMapper_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "remoteValueMapper",
            () =>
                new RuntimeHostEventOccurredObservationPayloadMapper(
                    null!));
    }

    [Fact]
    public void Map_NullPayload_ShouldThrow()
    {
        var mapper =
            CreateMapper();

        Assert.Throws<ArgumentNullException>(
            "payload",
            () =>
                mapper.Map(
                    null!));
    }

    [Fact]
    public void Map_DefinedValue_ShouldPreserveIdentityPathTimeAndValue()
    {
        var occurredAtUtc =
            new DateTimeOffset(
                2026,
                7,
                25,
                18,
                15,
                30,
                TimeSpan.Zero);
        var mappedValue =
            new GrpcV1.RemoteValue
            {
                StringValue =
                    "pressed"
            };
        var valueMapper =
            new TestRemoteValueMapper(
                mappedValue);
        var mapper =
            new RuntimeHostEventOccurredObservationPayloadMapper(
                valueMapper);

        GrpcV1.EventOccurredObservation result =
            mapper.Map(
                CreatePayload(
                    occurredAtUtc,
                    "pressed"));

        Assert.Equal(
            "controller-01",
            result.InstrumentId);
        Assert.Equal(
            new[]
            {
                "Controller",
                "ButtonPressed"
            },
            result.EventPathSegments);
        Assert.Equal(
            Timestamp.FromDateTimeOffset(
                occurredAtUtc),
            result.OccurredAtUtc);
        Assert.Equal(
            "pressed",
            valueMapper.Input);
        Assert.Same(
            mappedValue,
            result.Value);
    }

    [Fact]
    public void Map_AbsentValue_ShouldPreserveAbsence()
    {
        var valueMapper =
            new TestRemoteValueMapper(
                new GrpcV1.RemoteValue());
        var mapper =
            new RuntimeHostEventOccurredObservationPayloadMapper(
                valueMapper);

        GrpcV1.EventOccurredObservation result =
            mapper.Map(
                CreatePayload(
                    DateTimeOffset.UnixEpoch,
                    value: null));

        Assert.Null(
            valueMapper.Input);
        Assert.Null(
            result.Value);
    }

    [Fact]
    public void Map_RemoteValueMapperReturnsNull_ShouldThrow()
    {
        var mapper =
            new RuntimeHostEventOccurredObservationPayloadMapper(
                new TestRemoteValueMapper(
                    null!));

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    mapper.Map(
                        CreatePayload(
                            DateTimeOffset.UnixEpoch,
                            "pressed")));

        Assert.Equal(
            "The remote Event value mapper returned null.",
            exception.Message);
    }

    private static RuntimeHostEventOccurredObservationPayloadMapper
        CreateMapper()
    {
        return new RuntimeHostEventOccurredObservationPayloadMapper(
            new TestRemoteValueMapper(
                new GrpcV1.RemoteValue()));
    }

    private static Northbound.RuntimeHostEventOccurredObservationPayload
        CreatePayload(
            DateTimeOffset occurredAtUtc,
            object? value)
    {
        return new Northbound.RuntimeHostEventOccurredObservationPayload(
            new InstrumentId(
                "controller-01"),
            new DescriptorPath(
                "Controller",
                "ButtonPressed"),
            occurredAtUtc,
            value);
    }

    private sealed class TestRemoteValueMapper
        : IRemoteValueMapper
    {
        private readonly GrpcV1.RemoteValue result;

        public TestRemoteValueMapper(
            GrpcV1.RemoteValue result)
        {
            this.result =
                result;
        }

        public object? Input
        {
            get;
            private set;
        }

        public GrpcV1.RemoteValue Map(
            object value)
        {
            Input =
                value;

            return result;
        }

        public object? MapToClr(
            GrpcV1.RemoteValue value)
        {
            throw new NotSupportedException();
        }
    }
}
