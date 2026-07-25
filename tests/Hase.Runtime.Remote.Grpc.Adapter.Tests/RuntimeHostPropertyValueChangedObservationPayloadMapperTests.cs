using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostPropertyValueChangedObservationPayloadMapperTests
{
    [Fact]
    public void Constructor_NullPropertyValueMapper_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "propertyValueMapper",
            () =>
                new RuntimeHostPropertyValueChangedObservationPayloadMapper(
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
    public void Map_DefinedValues_ShouldPreserveIdentityAndBothValues()
    {
        var previousValue =
            new PropertyValue(
                23.5,
                DateTimeOffset.UnixEpoch);
        var currentValue =
            new PropertyValue(
                23.75,
                DateTimeOffset.UnixEpoch.AddSeconds(
                    1));
        var mappedPrevious =
            new GrpcV1.PropertyValue();
        var mappedCurrent =
            new GrpcV1.PropertyValue();
        var valueMapper =
            new TestPropertyValueMapper(
                mappedPrevious,
                mappedCurrent);
        var mapper =
            new RuntimeHostPropertyValueChangedObservationPayloadMapper(
                valueMapper);

        GrpcV1.PropertyValueChangedObservation result =
            mapper.Map(
                CreatePayload(
                    previousValue,
                    currentValue));

        Assert.Equal(
            "environment-sensor-01",
            result.InstrumentId);
        Assert.Equal(
            "temperature",
            result.PropertyId);
        Assert.Equal(
            new[]
            {
                previousValue,
                currentValue
            },
            valueMapper.Inputs);
        Assert.Same(
            mappedPrevious,
            result.PreviousValue);
        Assert.Same(
            mappedCurrent,
            result.CurrentValue);
    }

    [Fact]
    public void Map_AbsentPreviousValue_ShouldPreserveAbsence()
    {
        var currentValue =
            new PropertyValue(
                23.75,
                DateTimeOffset.UnixEpoch);
        var mappedCurrent =
            new GrpcV1.PropertyValue();
        var valueMapper =
            new TestPropertyValueMapper(
                mappedCurrent);
        var mapper =
            new RuntimeHostPropertyValueChangedObservationPayloadMapper(
                valueMapper);

        GrpcV1.PropertyValueChangedObservation result =
            mapper.Map(
                CreatePayload(
                    previousValue: null,
                    currentValue:
                        currentValue));

        Assert.Null(
            result.PreviousValue);
        Assert.Same(
            mappedCurrent,
            result.CurrentValue);
        Assert.Equal(
            new[]
            {
                currentValue
            },
            valueMapper.Inputs);
    }

    [Fact]
    public void Map_PreviousValueMapperReturnsNull_ShouldThrow()
    {
        var mapper =
            new RuntimeHostPropertyValueChangedObservationPayloadMapper(
                new TestPropertyValueMapper(
                    null!,
                    new GrpcV1.PropertyValue()));

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    mapper.Map(
                        CreatePayload(
                            new PropertyValue(
                                23.5,
                                DateTimeOffset.UnixEpoch),
                            new PropertyValue(
                                23.75,
                                DateTimeOffset.UnixEpoch))));

        Assert.Equal(
            "The previous Property value mapper returned null.",
            exception.Message);
    }

    [Fact]
    public void Map_CurrentValueMapperReturnsNull_ShouldThrow()
    {
        var mapper =
            new RuntimeHostPropertyValueChangedObservationPayloadMapper(
                new TestPropertyValueMapper(
                    new GrpcV1.PropertyValue[]
                    {
                        null!
                    }));

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    mapper.Map(
                        CreatePayload(
                            previousValue: null,
                            currentValue:
                                new PropertyValue(
                                    23.75,
                                    DateTimeOffset.UnixEpoch))));

        Assert.Equal(
            "The current Property value mapper returned null.",
            exception.Message);
    }

    private static RuntimeHostPropertyValueChangedObservationPayloadMapper
        CreateMapper()
    {
        return new RuntimeHostPropertyValueChangedObservationPayloadMapper(
            new TestPropertyValueMapper(
                new GrpcV1.PropertyValue()));
    }

    private static Northbound.RuntimeHostPropertyValueChangedObservationPayload
        CreatePayload(
            PropertyValue? previousValue,
            PropertyValue currentValue)
    {
        return new Northbound
            .RuntimeHostPropertyValueChangedObservationPayload(
                new InstrumentId(
                    "environment-sensor-01"),
                new PropertyId(
                    "temperature"),
                previousValue,
                currentValue);
    }

    private sealed class TestPropertyValueMapper
        : IPropertyValueMapper
    {
        private readonly Queue<GrpcV1.PropertyValue> results;

        public TestPropertyValueMapper(
            params GrpcV1.PropertyValue[] results)
        {
            this.results =
                new Queue<GrpcV1.PropertyValue>(
                    results);
        }

        public List<PropertyValue> Inputs
        {
            get;
        } =
            [];

        public GrpcV1.PropertyValue Map(
            PropertyValue source)
        {
            Inputs.Add(
                source);

            return results.Dequeue();
        }
    }
}
