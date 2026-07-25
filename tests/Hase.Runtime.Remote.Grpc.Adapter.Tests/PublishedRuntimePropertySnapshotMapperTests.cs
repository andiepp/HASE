using Hase.Core.Domain.Data;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Connections;
using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class PublishedRuntimePropertySnapshotMapperTests
{
    [Fact]
    public void Constructor_NullDescriptorMapper_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "descriptorMapper",
            () =>
                new PublishedRuntimePropertySnapshotMapper(
                    null!,
                    CreateConnectionStatusMapper(),
                    CreatePropertyValueMapper()));
    }

    [Fact]
    public void Constructor_NullConnectionStatusMapper_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "connectionStatusMapper",
            () =>
                new PublishedRuntimePropertySnapshotMapper(
                    CreateDescriptorMapper(),
                    null!,
                    CreatePropertyValueMapper()));
    }

    [Fact]
    public void Constructor_NullPropertyValueMapper_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "propertyValueMapper",
            () =>
                new PublishedRuntimePropertySnapshotMapper(
                    CreateDescriptorMapper(),
                    CreateConnectionStatusMapper(),
                    null!));
    }

    [Fact]
    public void Map_NullSnapshot_ShouldThrow()
    {
        var mapper =
            CreateMapper();

        Assert.Throws<ArgumentNullException>(
            "snapshot",
            () =>
                mapper.Map(
                    null!));
    }

    [Fact]
    public void Map_KnownSnapshot_ShouldPreserveIdentityAndDelegateChildren()
    {
        Northbound.PublishedRuntimePropertySnapshot snapshot =
            CreateSnapshot(
                new PropertyValue(
                    23.75,
                    DateTimeOffset.UnixEpoch));
        var mappedDescriptor =
            new GrpcV1.PropertyDescriptor
            {
                PropertyId =
                    "mapped-property"
            };
        var mappedConnectionStatus =
            new GrpcV1.EndpointConnectionStatus
            {
                Detail =
                    "mapped-ready"
            };
        var mappedCurrentValue =
            new GrpcV1.PropertyValue
            {
                Quality =
                    GrpcV1.PropertyQuality.Good
            };
        var descriptorMapper =
            new TestPropertyDescriptorMapper(
                mappedDescriptor);
        var connectionStatusMapper =
            new TestConnectionStatusMapper(
                mappedConnectionStatus);
        var propertyValueMapper =
            new TestPropertyValueMapper(
                mappedCurrentValue);
        var mapper =
            new PublishedRuntimePropertySnapshotMapper(
                descriptorMapper,
                connectionStatusMapper,
                propertyValueMapper);

        GrpcV1.PublishedRuntimePropertySnapshot result =
            mapper.Map(
                snapshot);

        Assert.Equal(
            snapshot.Target.EndpointId.Value,
            result.Target.EndpointId);
        Assert.Equal(
            snapshot.Target.AttachmentGeneration.ToString(),
            result.Target.AttachmentGeneration);
        Assert.Equal(
            snapshot.Target.InstrumentId.Value,
            result.Target.InstrumentId);
        Assert.Equal(
            snapshot.Target.PropertyId.Value,
            result.Target.PropertyId);
        Assert.Same(
            mappedDescriptor,
            result.Descriptor_);
        Assert.Same(
            mappedConnectionStatus,
            result.ConnectionStatus);
        Assert.Same(
            mappedCurrentValue,
            result.CurrentValue);
        Assert.Same(
            snapshot.Descriptor,
            descriptorMapper.Input);
        Assert.Same(
            snapshot.ConnectionStatus,
            connectionStatusMapper.Input);
        Assert.Same(
            snapshot.CurrentValue,
            propertyValueMapper.Input);
    }

    [Fact]
    public void Map_UnknownValue_ShouldPreserveAbsence()
    {
        var propertyValueMapper =
            CreatePropertyValueMapper();
        var mapper =
            new PublishedRuntimePropertySnapshotMapper(
                CreateDescriptorMapper(),
                CreateConnectionStatusMapper(),
                propertyValueMapper);

        GrpcV1.PublishedRuntimePropertySnapshot result =
            mapper.Map(
                CreateSnapshot(
                    currentValue: null));

        Assert.Null(
            result.CurrentValue);
        Assert.Null(
            propertyValueMapper.Input);
    }

    [Fact]
    public void Map_DescriptorMapperReturnsNull_ShouldThrow()
    {
        var mapper =
            new PublishedRuntimePropertySnapshotMapper(
                new TestPropertyDescriptorMapper(
                    null!),
                CreateConnectionStatusMapper(),
                CreatePropertyValueMapper());

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    mapper.Map(
                        CreateSnapshot(
                            currentValue: null)));

        Assert.Equal(
            "The Property descriptor mapper returned null.",
            exception.Message);
    }

    [Fact]
    public void Map_ConnectionStatusMapperReturnsNull_ShouldThrow()
    {
        var mapper =
            new PublishedRuntimePropertySnapshotMapper(
                CreateDescriptorMapper(),
                new TestConnectionStatusMapper(
                    null!),
                CreatePropertyValueMapper());

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    mapper.Map(
                        CreateSnapshot(
                            currentValue: null)));

        Assert.Equal(
            "The endpoint connection status mapper returned null.",
            exception.Message);
    }

    [Fact]
    public void Map_PropertyValueMapperReturnsNull_ShouldThrow()
    {
        var mapper =
            new PublishedRuntimePropertySnapshotMapper(
                CreateDescriptorMapper(),
                CreateConnectionStatusMapper(),
                new TestPropertyValueMapper(
                    null!));

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    mapper.Map(
                        CreateSnapshot(
                            new PropertyValue(
                                true,
                                DateTimeOffset.UnixEpoch))));

        Assert.Equal(
            "The Property value mapper returned null.",
            exception.Message);
    }

    private static PublishedRuntimePropertySnapshotMapper CreateMapper()
    {
        return new PublishedRuntimePropertySnapshotMapper(
            CreateDescriptorMapper(),
            CreateConnectionStatusMapper(),
            CreatePropertyValueMapper());
    }

    private static TestPropertyDescriptorMapper CreateDescriptorMapper()
    {
        return new TestPropertyDescriptorMapper(
            new GrpcV1.PropertyDescriptor());
    }

    private static TestConnectionStatusMapper CreateConnectionStatusMapper()
    {
        return new TestConnectionStatusMapper(
            new GrpcV1.EndpointConnectionStatus());
    }

    private static TestPropertyValueMapper CreatePropertyValueMapper()
    {
        return new TestPropertyValueMapper(
            new GrpcV1.PropertyValue());
    }

    private static Northbound.PublishedRuntimePropertySnapshot CreateSnapshot(
        PropertyValue? currentValue)
    {
        var propertyId =
            new PropertyId(
                "temperature");
        var target =
            new Northbound.RuntimeHostPropertyTarget(
                new EndpointId(
                    "endpoint-01"),
                new Northbound.RuntimeEndpointAttachmentGeneration(
                    new Guid(
                        "868e79d4-b1a4-4a63-81cd-5a800d9ba3fd")),
                new InstrumentId(
                    "environment-sensor-01"),
                propertyId);
        var descriptor =
            new PropertyDescriptor(
                propertyId,
                new DescriptorPath(
                    "physical",
                    "environment-sensor",
                    "temperature"),
                "Temperature",
                new StringDataDescriptor());

        return new Northbound.PublishedRuntimePropertySnapshot(
            target,
            descriptor,
            new EndpointConnectionStatus(
                EndpointConnectionState.Ready),
            currentValue);
    }

    private sealed class TestPropertyDescriptorMapper
        : IPropertyDescriptorMapper
    {
        private readonly GrpcV1.PropertyDescriptor result;

        public TestPropertyDescriptorMapper(
            GrpcV1.PropertyDescriptor result)
        {
            this.result =
                result;
        }

        public PropertyDescriptor? Input
        {
            get;
            private set;
        }

        public GrpcV1.PropertyDescriptor Map(
            PropertyDescriptor descriptor)
        {
            Input =
                descriptor;

            return result;
        }
    }

    private sealed class TestConnectionStatusMapper
        : IEndpointConnectionStatusMapper
    {
        private readonly GrpcV1.EndpointConnectionStatus result;

        public TestConnectionStatusMapper(
            GrpcV1.EndpointConnectionStatus result)
        {
            this.result =
                result;
        }

        public EndpointConnectionStatus? Input
        {
            get;
            private set;
        }

        public GrpcV1.EndpointConnectionStatus Map(
            EndpointConnectionStatus status)
        {
            Input =
                status;

            return result;
        }
    }

    private sealed class TestPropertyValueMapper
        : IPropertyValueMapper
    {
        private readonly GrpcV1.PropertyValue result;

        public TestPropertyValueMapper(
            GrpcV1.PropertyValue result)
        {
            this.result =
                result;
        }

        public PropertyValue? Input
        {
            get;
            private set;
        }

        public GrpcV1.PropertyValue Map(
            PropertyValue source)
        {
            Input =
                source;

            return result;
        }
    }
}
