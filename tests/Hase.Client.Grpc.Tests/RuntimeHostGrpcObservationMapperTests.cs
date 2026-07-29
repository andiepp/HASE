using Google.Protobuf.WellKnownTypes;
using Hase.Client;
using Hase.Client.Grpc;
using Hase.Core.Domain.Data;
using GrpcV1 = Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Client.Grpc.Tests;

public sealed class RuntimeHostGrpcObservationMapperTests
{
    private const string Generation =
        "0a11d9d4-7a02-43be-ae3f-eef9d11e0de8";

    [Fact]
    public void MapInitialSnapshot_CompleteDescriptor_ShouldMapClientSnapshot()
    {
        GrpcV1.ObservationInitialSnapshot source =
            CreateInitialSnapshot();
        var instrument =
            new GrpcV1.InstrumentDescriptor
            {
                InstrumentId =
                    "instrument-01",
                Name =
                    "Controller",
                Kind =
                    "controller",
                Manufacturer =
                    "HASE",
                Description =
                    "Controller instrument"
            };
        instrument.Properties.Add(
            new GrpcV1.PropertyDescriptor
            {
                PropertyId =
                    "enabled",
                DisplayName =
                    "Enabled",
                Description =
                    "Enable state",
                AccessMode =
                    GrpcV1.PropertyAccessMode.ReadWrite,
                Data =
                    new GrpcV1.DataDescriptor
                    {
                        BooleanDescriptor =
                            new GrpcV1.BooleanDataDescriptor()
                    },
                PathSegments =
                {
                    "Controller",
                    "Enabled"
                }
            });
        instrument.Commands.Add(
            new GrpcV1.CommandDescriptor
            {
                DisplayName =
                    "Toggle",
                Argument =
                    new GrpcV1.CommandArgumentDescriptor
                    {
                        DisplayName =
                            "Payload",
                        Description =
                            "Opaque payload",
                        Data =
                            new GrpcV1.DataDescriptor
                            {
                                ByteArrayDescriptor =
                                    new GrpcV1.ByteArrayDataDescriptor()
                            }
                    },
                PathSegments =
                {
                    "Controller",
                    "Toggle"
                }
            });
        instrument.Events.Add(
            new GrpcV1.EventDescriptor
            {
                DisplayName =
                    "Button pressed",
                PathSegments =
                {
                    "Controller",
                    "ButtonPressed"
                }
            });
        source.Snapshot.Endpoints[0].Descriptor_.Instruments.Add(
            instrument);

        RemoteObservationInitialSnapshot result =
            new RuntimeHostGrpcObservationMapper().MapInitialSnapshot(
                source);

        Assert.Equal(
            "runtime-01",
            result.Snapshot.RuntimeHostId.Value);
        Assert.Equal(
            RuntimeHostClientApiVersion.Current,
            result.Snapshot.ApiVersion);
        Assert.Equal(
            7UL,
            result.SnapshotSequence.Value);
        RemoteEndpointAttachmentSnapshot endpoint =
            Assert.Single(
                result.Snapshot.Attachments);
        Assert.Equal(
            RemoteEndpointConnectionState.Ready,
            endpoint.ConnectionStatus.State);
        Assert.Equal(
            "Endpoint",
            endpoint.Descriptor.Metadata.DisplayName);
        var mappedInstrument =
            Assert.Single(
                endpoint.Descriptor.Instruments);
        Assert.Equal(
            "HASE",
            mappedInstrument.Metadata.Manufacturer);
        Assert.IsType<BooleanDataDescriptor>(
            Assert.Single(
                mappedInstrument.Interface.Properties).Data);
        var mappedCommand =
            Assert.Single(
                mappedInstrument.Interface.Commands);
        Assert.NotNull(
            mappedCommand.Argument);
        Assert.Equal(
            "Payload",
            mappedCommand.Argument.DisplayName);
        Assert.Equal(
            "Opaque payload",
            mappedCommand.Argument.Description);
        Assert.IsType<ByteArrayDataDescriptor>(
            mappedCommand.Argument.Data);
        Assert.Single(
            mappedInstrument.Interface.Events);
    }

    [Fact]
    public void MapInitialSnapshot_UnsupportedMajorVersion_ShouldThrow()
    {
        GrpcV1.ObservationInitialSnapshot source =
            CreateInitialSnapshot();
        source.Snapshot.ApiVersion.Major =
            2;

        Assert.Throws<NotSupportedException>(
            () => new RuntimeHostGrpcObservationMapper()
                .MapInitialSnapshot(
                    source));
    }

    [Fact]
    public void MapInitialSnapshot_MissingSnapshot_ShouldThrow()
    {
        Assert.Throws<InvalidDataException>(
            () => new RuntimeHostGrpcObservationMapper()
                .MapInitialSnapshot(
                    new GrpcV1.ObservationInitialSnapshot()));
    }

    [Fact]
    public void MapObservation_AttachmentPublished_ShouldMapPayload()
    {
        GrpcV1.RuntimeHostObservation source =
            CreateObservation(
                GrpcV1.RuntimeHostObservationKind.AttachmentPublished);
        source.AttachmentPublished =
            new GrpcV1.AttachmentPublishedObservation
            {
                Endpoint =
                    CreateEndpoint()
            };

        RemoteRuntimeHostObservation result =
            new RuntimeHostGrpcObservationMapper().MapObservation(
                source);

        Assert.Equal(
            RemoteObservationKind.AttachmentPublished,
            result.Kind);
        var payload =
            Assert.IsType<RemoteAttachmentPublishedObservationPayload>(
                result.Payload);
        Assert.Equal(
            result.Attachment,
            payload.Endpoint.Key);
    }

    [Fact]
    public void MapObservation_AttachmentEnded_ShouldMapPayload()
    {
        GrpcV1.RuntimeHostObservation source =
            CreateObservation(
                GrpcV1.RuntimeHostObservationKind.AttachmentEnded);
        source.AttachmentEnded =
            new GrpcV1.AttachmentEndedObservation
            {
                EndedAtUtc =
                    Timestamp.FromDateTimeOffset(
                        DateTimeOffset.UnixEpoch)
            };

        RemoteRuntimeHostObservation result =
            new RuntimeHostGrpcObservationMapper().MapObservation(
                source);

        var payload =
            Assert.IsType<RemoteAttachmentEndedObservationPayload>(
                result.Payload);
        Assert.Equal(
            DateTimeOffset.UnixEpoch,
            payload.EndedAtUtc);
    }

    [Fact]
    public void MapObservation_ConnectionStatusChanged_ShouldMapPayload()
    {
        GrpcV1.RuntimeHostObservation source =
            CreateObservation(
                GrpcV1.RuntimeHostObservationKind.ConnectionStatusChanged);
        source.ConnectionStatusChanged =
            new GrpcV1.ConnectionStatusChangedObservation
            {
                PreviousStatus =
                    CreateStatus(
                        GrpcV1.EndpointConnectionState.Ready),
                CurrentStatus =
                    CreateStatus(
                        GrpcV1.EndpointConnectionState.Reconnecting)
            };

        RemoteRuntimeHostObservation result =
            new RuntimeHostGrpcObservationMapper().MapObservation(
                source);

        var payload =
            Assert.IsType<RemoteConnectionStatusChangedObservationPayload>(
                result.Payload);
        Assert.Equal(
            RemoteEndpointConnectionState.Ready,
            payload.PreviousStatus.State);
        Assert.Equal(
            RemoteEndpointConnectionState.Reconnecting,
            payload.CurrentStatus.State);
    }

    [Fact]
    public void MapObservation_PropertyValueChanged_ShouldMapPayload()
    {
        GrpcV1.RuntimeHostObservation source =
            CreateObservation(
                GrpcV1.RuntimeHostObservationKind.PropertyValueChanged);
        source.PropertyValueChanged =
            new GrpcV1.PropertyValueChangedObservation
            {
                InstrumentId =
                    "instrument-01",
                PropertyId =
                    "enabled",
                PreviousValue =
                    CreatePropertyValue(
                        false),
                CurrentValue =
                    CreatePropertyValue(
                        true)
            };

        RemoteRuntimeHostObservation result =
            new RuntimeHostGrpcObservationMapper().MapObservation(
                source);

        var payload =
            Assert.IsType<RemotePropertyValueChangedObservationPayload>(
                result.Payload);
        Assert.False(
            payload.PreviousValue!.Value!.BooleanValue!.Value);
        Assert.True(
            payload.CurrentValue.Value!.BooleanValue!.Value);
        Assert.Equal(
            RemotePropertyQuality.Good,
            payload.CurrentValue.Quality);
    }

    [Fact]
    public void MapObservation_ByteArrayPropertyValueChanged_ShouldMapPayload()
    {
        GrpcV1.RuntimeHostObservation source =
            CreateObservation(
                GrpcV1.RuntimeHostObservationKind.PropertyValueChanged);
        source.PropertyValueChanged =
            new GrpcV1.PropertyValueChangedObservation
            {
                InstrumentId =
                    "byte-buffer-01",
                PropertyId =
                    "byte-buffer-01.buffer-value",
                CurrentValue =
                    new GrpcV1.PropertyValue
                    {
                        Value =
                            new GrpcV1.RemoteValue
                            {
                                ByteArrayValue =
                                    Google.Protobuf.ByteString.CopyFrom(
                                        new byte[]
                                        {
                                            0x00,
                                            0x53,
                                            0xff
                                        })
                            },
                        TimestampUtc =
                            Timestamp.FromDateTimeOffset(
                                DateTimeOffset.UnixEpoch),
                        Quality =
                            GrpcV1.PropertyQuality.Good
                    }
            };

        RemoteRuntimeHostObservation result =
            new RuntimeHostGrpcObservationMapper().MapObservation(
                source);

        var payload =
            Assert.IsType<RemotePropertyValueChangedObservationPayload>(
                result.Payload);

        Assert.Equal(
            RemoteValueKind.ByteArray,
            payload.CurrentValue.Value!.Kind);
        Assert.Equal(
            new byte[]
            {
                0x00,
                0x53,
                0xff
            },
            payload.CurrentValue.Value.ByteArrayValue!.ToArray());
    }

    [Fact]
    public void MapObservation_EventOccurred_ShouldMapPayload()
    {
        GrpcV1.RuntimeHostObservation source =
            CreateObservation(
                GrpcV1.RuntimeHostObservationKind.EventOccurred);
        source.EventOccurred =
            new GrpcV1.EventOccurredObservation
            {
                InstrumentId =
                    "instrument-01",
                OccurredAtUtc =
                    Timestamp.FromDateTimeOffset(
                        DateTimeOffset.UnixEpoch),
                Value =
                    new GrpcV1.RemoteValue
                    {
                        NumericValue =
                            12.5
                    },
                EventPathSegments =
                {
                    "Controller",
                    "ButtonPressed"
                }
            };

        RemoteRuntimeHostObservation result =
            new RuntimeHostGrpcObservationMapper().MapObservation(
                source);

        var payload =
            Assert.IsType<RemoteEventOccurredObservationPayload>(
                result.Payload);
        Assert.Equal(
            "Controller.ButtonPressed",
            payload.EventPath.ToString());
        Assert.Equal(
            12.5,
            payload.Value!.NumericValue!.Value);
    }

    [Fact]
    public void MapObservation_MismatchedKindAndPayload_ShouldThrow()
    {
        GrpcV1.RuntimeHostObservation source =
            CreateObservation(
                GrpcV1.RuntimeHostObservationKind.EventOccurred);
        source.AttachmentEnded =
            new GrpcV1.AttachmentEndedObservation
            {
                EndedAtUtc =
                    Timestamp.FromDateTimeOffset(
                        DateTimeOffset.UnixEpoch)
            };

        Assert.Throws<InvalidDataException>(
            () => new RuntimeHostGrpcObservationMapper().MapObservation(
                source));
    }

    [Fact]
    public void MapObservation_InvalidAttachmentGeneration_ShouldThrow()
    {
        GrpcV1.RuntimeHostObservation source =
            CreateObservation(
                GrpcV1.RuntimeHostObservationKind.AttachmentEnded);
        source.AttachmentGeneration =
            "not-a-generation";
        source.AttachmentEnded =
            new GrpcV1.AttachmentEndedObservation
            {
                EndedAtUtc =
                    Timestamp.FromDateTimeOffset(
                        DateTimeOffset.UnixEpoch)
            };

        Assert.Throws<InvalidDataException>(
            () => new RuntimeHostGrpcObservationMapper().MapObservation(
                source));
    }

    private static GrpcV1.ObservationInitialSnapshot CreateInitialSnapshot()
    {
        var snapshot =
            new GrpcV1.GetSnapshotResponse
            {
                RuntimeHostId =
                    "runtime-01",
                ApiVersion =
                    new GrpcV1.RuntimeHostApiVersion
                    {
                        Major =
                            1,
                        Minor =
                            0
                    }
            };
        snapshot.Endpoints.Add(
            CreateEndpoint());

        return new GrpcV1.ObservationInitialSnapshot
        {
            Snapshot =
                snapshot,
            SnapshotSequence =
                7
        };
    }

    private static GrpcV1.PublishedRuntimeEndpointSnapshot CreateEndpoint()
    {
        return new GrpcV1.PublishedRuntimeEndpointSnapshot
        {
            EndpointId =
                "endpoint-01",
            AttachmentGeneration =
                Generation,
            ConnectionStatus =
                CreateStatus(
                    GrpcV1.EndpointConnectionState.Ready),
            Descriptor_ =
                new GrpcV1.EndpointDescriptor
                {
                    EndpointId =
                        "endpoint-01",
                    DisplayName =
                        "Endpoint"
                }
        };
    }

    private static GrpcV1.EndpointConnectionStatus CreateStatus(
        GrpcV1.EndpointConnectionState state)
    {
        return new GrpcV1.EndpointConnectionStatus
        {
            State =
                state,
            ChangedAtUtc =
                Timestamp.FromDateTimeOffset(
                    DateTimeOffset.UnixEpoch)
        };
    }

    private static GrpcV1.PropertyValue CreatePropertyValue(
        bool value)
    {
        return new GrpcV1.PropertyValue
        {
            Value =
                new GrpcV1.RemoteValue
                {
                    BooleanValue =
                        value
                },
            TimestampUtc =
                Timestamp.FromDateTimeOffset(
                    DateTimeOffset.UnixEpoch),
            Quality =
                GrpcV1.PropertyQuality.Good
        };
    }

    private static GrpcV1.RuntimeHostObservation CreateObservation(
        GrpcV1.RuntimeHostObservationKind kind)
    {
        return new GrpcV1.RuntimeHostObservation
        {
            Sequence =
                8,
            EndpointId =
                "endpoint-01",
            AttachmentGeneration =
                Generation,
            Kind =
                kind
        };
    }
}

