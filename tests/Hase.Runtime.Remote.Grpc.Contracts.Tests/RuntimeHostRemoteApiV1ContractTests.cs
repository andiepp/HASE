using Google.Protobuf;
using Google.Protobuf.Reflection;
using Google.Protobuf.WellKnownTypes;
using Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Contracts.Tests;

// Verifies the generated version 1 host and endpoint-envelope contract surface.
public sealed class RuntimeHostRemoteApiV1ContractTests
{
    [Fact]
    public void Contract_UsesVersionedPackage()
    {
        Assert.Equal(
            "hase.runtime.remote.v1",
            RuntimeHostRemoteApiV1Reflection.Descriptor.Package);
    }

    [Fact]
    public void Contract_DefinesUnarySnapshotOperation()
    {
        var service = Assert.Single(
            RuntimeHostRemoteApiV1Reflection.Descriptor.Services);

        Assert.Equal("RuntimeHostRemoteApi", service.Name);

        var method = Assert.Single(service.Methods);

        Assert.Equal("GetSnapshot", method.Name);
        Assert.False(method.IsClientStreaming);
        Assert.False(method.IsServerStreaming);
        Assert.Equal(
            GetSnapshotRequest.Descriptor.FullName,
            method.InputType.FullName);
        Assert.Equal(
            GetSnapshotResponse.Descriptor.FullName,
            method.OutputType.FullName);
    }

    [Fact]
    public void SnapshotResponse_DefinesAuthoritativeHostMembers()
    {
        Assert.Empty(GetSnapshotRequest.Descriptor.Fields.InDeclarationOrder());

        FieldDescriptor[] fields =
            GetSnapshotResponse.Descriptor.Fields.InDeclarationOrder().ToArray();

        Assert.Collection(
            fields,
            field =>
            {
                Assert.Equal("runtime_host_id", field.Name);
                Assert.Equal(1, field.FieldNumber);
                Assert.Equal(FieldType.String, field.FieldType);
                Assert.False(field.IsRepeated);
            },
            field =>
            {
                Assert.Equal("api_version", field.Name);
                Assert.Equal(2, field.FieldNumber);
                Assert.Equal(FieldType.Message, field.FieldType);
                Assert.Equal(
                    RuntimeHostApiVersion.Descriptor.FullName,
                    field.MessageType.FullName);
                Assert.False(field.IsRepeated);
            },
            field =>
            {
                Assert.Equal("endpoints", field.Name);
                Assert.Equal(3, field.FieldNumber);
                Assert.Equal(FieldType.Message, field.FieldType);
                Assert.Equal(
                    PublishedRuntimeEndpointSnapshot.Descriptor.FullName,
                    field.MessageType.FullName);
                Assert.True(field.IsRepeated);
            });
    }

    [Fact]
    public void ApiVersion_DefinesUnsignedMajorAndMinorMembers()
    {
        FieldDescriptor[] fields =
            RuntimeHostApiVersion.Descriptor.Fields
                .InDeclarationOrder()
                .ToArray();

        Assert.Collection(
            fields,
            field =>
            {
                Assert.Equal("major", field.Name);
                Assert.Equal(1, field.FieldNumber);
                Assert.Equal(FieldType.UInt32, field.FieldType);
            },
            field =>
            {
                Assert.Equal("minor", field.Name);
                Assert.Equal(2, field.FieldNumber);
                Assert.Equal(FieldType.UInt32, field.FieldType);
            });
    }

    [Fact]
    public void RemoteValue_DefinesClosedVersionOneUnion()
    {
        OneofDescriptor kind =
            Assert.Single(
                RemoteValue.Descriptor.Oneofs);

        Assert.Equal(
            "kind",
            kind.Name);

        Assert.Collection(
            kind.Fields,
            field =>
            {
                Assert.Equal("boolean_value", field.Name);
                Assert.Equal(1, field.FieldNumber);
                Assert.Equal(FieldType.Bool, field.FieldType);
            },
            field =>
            {
                Assert.Equal("string_value", field.Name);
                Assert.Equal(2, field.FieldNumber);
                Assert.Equal(FieldType.String, field.FieldType);
            },
            field =>
            {
                Assert.Equal("numeric_value", field.Name);
                Assert.Equal(3, field.FieldNumber);
                Assert.Equal(FieldType.Double, field.FieldType);
            });
    }

    [Fact]
    public void RemoteValue_RoundTrip_ShouldPreserveEachVersionOneVariant()
    {
        var booleanValue =
            new RemoteValue
            {
                BooleanValue =
                    true
            };
        var stringValue =
            new RemoteValue
            {
                StringValue =
                    "Ready"
            };
        var numericValue =
            new RemoteValue
            {
                NumericValue =
                    23.75
            };

        RemoteValue booleanRoundTrip =
            RemoteValue.Parser.ParseFrom(
                booleanValue.ToByteArray());
        RemoteValue stringRoundTrip =
            RemoteValue.Parser.ParseFrom(
                stringValue.ToByteArray());
        RemoteValue numericRoundTrip =
            RemoteValue.Parser.ParseFrom(
                numericValue.ToByteArray());

        Assert.Equal(
            RemoteValue.KindOneofCase.BooleanValue,
            booleanRoundTrip.KindCase);
        Assert.True(
            booleanRoundTrip.BooleanValue);
        Assert.Equal(
            RemoteValue.KindOneofCase.StringValue,
            stringRoundTrip.KindCase);
        Assert.Equal(
            "Ready",
            stringRoundTrip.StringValue);
        Assert.Equal(
            RemoteValue.KindOneofCase.NumericValue,
            numericRoundTrip.KindCase);
        Assert.Equal(
            23.75,
            numericRoundTrip.NumericValue);
    }

    [Fact]
    public void RemoteValue_DefaultInstance_ShouldRepresentAbsence()
    {
        var value =
            new RemoteValue();

        Assert.Equal(
            RemoteValue.KindOneofCase.None,
            value.KindCase);
    }

    [Fact]
    public void PublishedEndpointSnapshot_DefinesEnvelopeMembers()
    {
        FieldDescriptor[] fields =
            PublishedRuntimeEndpointSnapshot.Descriptor.Fields
                .InDeclarationOrder()
                .ToArray();

        Assert.Collection(
            fields,
            field =>
            {
                Assert.Equal("endpoint_id", field.Name);
                Assert.Equal(1, field.FieldNumber);
                Assert.Equal(FieldType.String, field.FieldType);
                Assert.False(field.IsRepeated);
            },
            field =>
            {
                Assert.Equal("attachment_generation", field.Name);
                Assert.Equal(2, field.FieldNumber);
                Assert.Equal(FieldType.String, field.FieldType);
                Assert.False(field.IsRepeated);
            },
            field =>
            {
                Assert.Equal("descriptor", field.Name);
                Assert.Equal(3, field.FieldNumber);
                Assert.Equal(FieldType.Message, field.FieldType);
                Assert.Equal(
                    EndpointDescriptor.Descriptor.FullName,
                    field.MessageType.FullName);
                Assert.False(field.IsRepeated);
            },
            field =>
            {
                Assert.Equal("connection_status", field.Name);
                Assert.Equal(4, field.FieldNumber);
                Assert.Equal(FieldType.Message, field.FieldType);
                Assert.Equal(
                    EndpointConnectionStatus.Descriptor.FullName,
                    field.MessageType.FullName);
                Assert.False(field.IsRepeated);
            });
    }

    [Fact]
    public void EndpointDescriptor_DefinesIdentityMetadataAndInstruments()
    {
        FieldDescriptor[] fields =
            EndpointDescriptor.Descriptor.Fields
                .InDeclarationOrder()
                .ToArray();

        Assert.Collection(
            fields,
            field =>
            {
                Assert.Equal("endpoint_id", field.Name);
                Assert.Equal(1, field.FieldNumber);
                Assert.Equal(FieldType.String, field.FieldType);
                Assert.False(field.HasPresence);
            },
            field =>
            {
                Assert.Equal("display_name", field.Name);
                Assert.Equal(2, field.FieldNumber);
                Assert.Equal(FieldType.String, field.FieldType);
                Assert.True(field.HasPresence);
            },
            field =>
            {
                Assert.Equal("description", field.Name);
                Assert.Equal(3, field.FieldNumber);
                Assert.Equal(FieldType.String, field.FieldType);
                Assert.True(field.HasPresence);
            },
            field =>
            {
                Assert.Equal("instruments", field.Name);
                Assert.Equal(4, field.FieldNumber);
                Assert.Equal(FieldType.Message, field.FieldType);
                Assert.Equal(
                    InstrumentDescriptor.Descriptor.FullName,
                    field.MessageType.FullName);
                Assert.True(field.IsRepeated);
            });
    }

    [Fact]
    public void InstrumentDescriptor_DefinesIdentityMetadataAndInterface()
    {
        FieldDescriptor[] fields =
            InstrumentDescriptor.Descriptor.Fields
                .InDeclarationOrder()
                .ToArray();

        Assert.Collection(
            fields,
            field => AssertField(
                field,
                "instrument_id",
                1,
                FieldType.String,
                false,
                false),
            field => AssertField(
                field,
                "name",
                2,
                FieldType.String,
                false,
                false),
            field => AssertField(
                field,
                "kind",
                3,
                FieldType.String,
                false,
                false),
            field => AssertField(
                field,
                "manufacturer",
                4,
                FieldType.String,
                true,
                false),
            field => AssertField(
                field,
                "model",
                5,
                FieldType.String,
                true,
                false),
            field => AssertField(
                field,
                "serial_number",
                6,
                FieldType.String,
                true,
                false),
            field => AssertField(
                field,
                "firmware_version",
                7,
                FieldType.String,
                true,
                false),
            field => AssertField(
                field,
                "hardware_revision",
                8,
                FieldType.String,
                true,
                false),
            field => AssertField(
                field,
                "description",
                9,
                FieldType.String,
                true,
                false),
            field => AssertMessageField(
                field,
                "properties",
                10,
                PropertyDescriptor.Descriptor,
                true),
            field => AssertMessageField(
                field,
                "commands",
                11,
                CommandDescriptor.Descriptor,
                true),
            field => AssertMessageField(
                field,
                "events",
                12,
                EventDescriptor.Descriptor,
                true));
    }

    [Fact]
    public void CommandAndEventDescriptors_DefinePathAndMetadata()
    {
        AssertPathDescriptor(
            CommandDescriptor.Descriptor);
        AssertPathDescriptor(
            EventDescriptor.Descriptor);
    }

    [Fact]
    public void CommandAndEventDescriptors_RoundTripIndependently()
    {
        var command =
            new CommandDescriptor
            {
                DisplayName =
                    "Start Sweep",
                Description =
                    "Starts the configured sweep"
            };

        command.PathSegments.Add(
            "DDS");
        command.PathSegments.Add(
            "Sweep");
        command.PathSegments.Add(
            "Start");

        CommandDescriptor commandRoundTrip =
            CommandDescriptor.Parser.ParseFrom(
                command.ToByteArray());

        Assert.Equal(
            command.PathSegments.ToArray(),
            commandRoundTrip.PathSegments.ToArray());
        Assert.Equal(
            command.DisplayName,
            commandRoundTrip.DisplayName);
        Assert.True(
            commandRoundTrip.HasDescription);
        Assert.Equal(
            command.Description,
            commandRoundTrip.Description);

        var eventDescriptor =
            new EventDescriptor
            {
                DisplayName =
                    "PLL Lock Lost",
                Description =
                    "Reports loss of PLL lock"
            };

        eventDescriptor.PathSegments.Add(
            "DDS");
        eventDescriptor.PathSegments.Add(
            "PLL");
        eventDescriptor.PathSegments.Add(
            "LockLost");

        EventDescriptor eventRoundTrip =
            EventDescriptor.Parser.ParseFrom(
                eventDescriptor.ToByteArray());

        Assert.Equal(
            eventDescriptor.PathSegments.ToArray(),
            eventRoundTrip.PathSegments.ToArray());
        Assert.Equal(
            eventDescriptor.DisplayName,
            eventRoundTrip.DisplayName);
        Assert.True(
            eventRoundTrip.HasDescription);
        Assert.Equal(
            eventDescriptor.Description,
            eventRoundTrip.Description);
    }

    [Fact]
    public void PropertyDescriptor_DefinesIdentityPathMetadataAccessAndData()
    {
        FieldDescriptor[] fields =
            PropertyDescriptor.Descriptor.Fields
                .InDeclarationOrder()
                .ToArray();

        Assert.Collection(
            fields,
            field => AssertField(
                field,
                "property_id",
                1,
                FieldType.String,
                false,
                false),
            field => AssertField(
                field,
                "path_segments",
                2,
                FieldType.String,
                false,
                true),
            field => AssertField(
                field,
                "display_name",
                3,
                FieldType.String,
                false,
                false),
            field => AssertField(
                field,
                "description",
                4,
                FieldType.String,
                true,
                false),
            field =>
            {
                AssertField(
                    field,
                    "access_mode",
                    5,
                    FieldType.Enum,
                    false,
                    false);
                Assert.Equal(
                    "hase.runtime.remote.v1.PropertyAccessMode",
                    field.EnumType.FullName);
            },
            field => AssertMessageField(
                field,
                "data",
                6,
                DataDescriptor.Descriptor,
                false));
    }

    [Fact]
    public void PropertyAccessMode_DefinesClrFlagValues()
    {
        EnumValueDescriptor[] values =
            PropertyDescriptor.Descriptor
                .FindFieldByNumber(5)
                .EnumType
                .Values
                .ToArray();

        Assert.Collection(
            values,
            value => AssertEnumValue(
                value,
                "PROPERTY_ACCESS_MODE_NONE",
                0),
            value => AssertEnumValue(
                value,
                "PROPERTY_ACCESS_MODE_READ",
                1),
            value => AssertEnumValue(
                value,
                "PROPERTY_ACCESS_MODE_WRITE",
                2),
            value => AssertEnumValue(
                value,
                "PROPERTY_ACCESS_MODE_READ_WRITE",
                3));
    }

    [Fact]
    public void DataDescriptor_DefinesExclusiveVariants()
    {
        OneofDescriptor kind =
            Assert.Single(
                DataDescriptor.Descriptor.Oneofs);

        Assert.Equal(
            "kind",
            kind.Name);

        Assert.Collection(
            kind.Fields,
            field => AssertMessageField(
                field,
                "numeric",
                1,
                NumericDataDescriptor.Descriptor,
                false),
            field => AssertMessageField(
                field,
                "boolean_descriptor",
                2,
                BooleanDataDescriptor.Descriptor,
                false),
            field => AssertMessageField(
                field,
                "string_descriptor",
                3,
                StringDataDescriptor.Descriptor,
                false));
    }

    [Fact]
    public void NumericDataDescriptor_DefinesEngineeringMembers()
    {
        FieldDescriptor[] fields =
            NumericDataDescriptor.Descriptor.Fields
                .InDeclarationOrder()
                .ToArray();

        Assert.Collection(
            fields,
            field => AssertMessageField(
                field,
                "quantity",
                1,
                Quantity.Descriptor,
                false),
            field => AssertMessageField(
                field,
                "native_unit",
                2,
                Unit.Descriptor,
                false),
            field => AssertMessageField(
                field,
                "range",
                3,
                ValueRange.Descriptor,
                false),
            field => AssertMessageField(
                field,
                "resolution",
                4,
                Resolution.Descriptor,
                false));
    }

    [Fact]
    public void QuantityAndUnit_DefineRequiredTextAndQuantityReference()
    {
        Assert.Collection(
            Quantity.Descriptor.Fields.InDeclarationOrder(),
            field => AssertField(
                field,
                "id",
                1,
                FieldType.String,
                false,
                false),
            field => AssertField(
                field,
                "display_name",
                2,
                FieldType.String,
                false,
                false));

        Assert.Collection(
            Unit.Descriptor.Fields.InDeclarationOrder(),
            field => AssertField(
                field,
                "id",
                1,
                FieldType.String,
                false,
                false),
            field => AssertField(
                field,
                "display_name",
                2,
                FieldType.String,
                false,
                false),
            field => AssertField(
                field,
                "symbol",
                3,
                FieldType.String,
                false,
                false),
            field => AssertMessageField(
                field,
                "quantity",
                4,
                Quantity.Descriptor,
                false));
    }

    [Fact]
    public void RangeAndResolution_DefineDoubleMembers()
    {
        Assert.Collection(
            ValueRange.Descriptor.Fields.InDeclarationOrder(),
            field => AssertField(
                field,
                "minimum",
                1,
                FieldType.Double,
                false,
                false),
            field => AssertField(
                field,
                "maximum",
                2,
                FieldType.Double,
                false,
                false));

        FieldDescriptor resolution =
            Assert.Single(
                Resolution.Descriptor.Fields.InDeclarationOrder());

        AssertField(
            resolution,
            "value",
            1,
            FieldType.Double,
            false,
            false);
    }

    [Fact]
    public void BooleanAndStringDataDescriptors_HaveNoAdditionalMembers()
    {
        Assert.Empty(
            BooleanDataDescriptor.Descriptor.Fields.InDeclarationOrder());
        Assert.Empty(
            StringDataDescriptor.Descriptor.Fields.InDeclarationOrder());
    }

    [Fact]
    public void DataDescriptor_RoundTrip_PreservesEachVariant()
    {
        var quantity =
            new Quantity
            {
                Id =
                    "temperature",
                DisplayName =
                    "Temperature"
            };

        var numeric =
            new DataDescriptor
            {
                Numeric =
                    new NumericDataDescriptor
                    {
                        Quantity =
                            quantity,
                        NativeUnit =
                            new Unit
                            {
                                Id =
                                    "degree-celsius",
                                DisplayName =
                                    "Degree Celsius",
                                Symbol =
                                    "°C",
                                Quantity =
                                    quantity.Clone()
                            },
                        Range =
                            new ValueRange
                            {
                                Minimum = -40.0,
                                Maximum = 85.0
                            },
                        Resolution =
                            new Resolution
                            {
                                Value = 0.01
                            }
                    }
            };

        DataDescriptor numericRoundTrip =
            DataDescriptor.Parser.ParseFrom(
                numeric.ToByteArray());

        Assert.Equal(
            DataDescriptor.KindOneofCase.Numeric,
            numericRoundTrip.KindCase);
        Assert.Equal(
            "temperature",
            numericRoundTrip.Numeric.Quantity.Id);
        Assert.Equal(
            "°C",
            numericRoundTrip.Numeric.NativeUnit.Symbol);
        Assert.Equal(
            -40.0,
            numericRoundTrip.Numeric.Range.Minimum);
        Assert.Equal(
            85.0,
            numericRoundTrip.Numeric.Range.Maximum);
        Assert.Equal(
            0.01,
            numericRoundTrip.Numeric.Resolution.Value);

        var booleanData =
            new DataDescriptor
            {
                BooleanDescriptor =
                    new BooleanDataDescriptor()
            };

        DataDescriptor booleanRoundTrip =
            DataDescriptor.Parser.ParseFrom(
                booleanData.ToByteArray());

        Assert.Equal(
            DataDescriptor.KindOneofCase.BooleanDescriptor,
            booleanRoundTrip.KindCase);

        var stringData =
            new DataDescriptor
            {
                StringDescriptor =
                    new StringDataDescriptor()
            };

        DataDescriptor stringRoundTrip =
            DataDescriptor.Parser.ParseFrom(
                stringData.ToByteArray());

        Assert.Equal(
            DataDescriptor.KindOneofCase.StringDescriptor,
            stringRoundTrip.KindCase);
    }

    [Fact]
    public void PropertyDescriptor_RoundTrip_PreservesDefinedMembers()
    {
        var descriptor =
            new PropertyDescriptor
            {
                PropertyId =
                    "temperature",
                DisplayName =
                    "Temperature",
                Description =
                    "Environment temperature",
                AccessMode =
                    (PropertyAccessMode)1,
                Data =
                    new DataDescriptor()
            };

        descriptor.PathSegments.Add(
            "physical");
        descriptor.PathSegments.Add(
            "environment-sensor");
        descriptor.PathSegments.Add(
            "temperature");

        PropertyDescriptor roundTrip =
            PropertyDescriptor.Parser.ParseFrom(
                descriptor.ToByteArray());

        Assert.Equal(
            descriptor.PropertyId,
            roundTrip.PropertyId);
        Assert.Equal(
            descriptor.PathSegments.ToArray(),
            roundTrip.PathSegments.ToArray());
        Assert.Equal(
            descriptor.DisplayName,
            roundTrip.DisplayName);
        Assert.True(
            roundTrip.HasDescription);
        Assert.Equal(
            descriptor.Description,
            roundTrip.Description);
        Assert.Equal(
            1,
            (int)roundTrip.AccessMode);
        Assert.NotNull(
            roundTrip.Data);
    }

    [Fact]
    public void InstrumentDescriptor_RoundTrip_PreservesMetadataAndInterface()
    {
        var descriptor =
            new InstrumentDescriptor
            {
                InstrumentId =
                    "instrument-01",
                Name =
                    "Instrument 01",
                Kind =
                    "environment-sensor",
                Manufacturer =
                    "HASE",
                Model =
                    "Validation",
                SerialNumber =
                    "SN-01",
                FirmwareVersion =
                    "1.0",
                HardwareRevision =
                    "A",
                Description =
                    "Validation instrument"
            };

        descriptor.Properties.Add(
            new PropertyDescriptor());
        descriptor.Commands.Add(
            new CommandDescriptor());
        descriptor.Events.Add(
            new EventDescriptor());

        InstrumentDescriptor roundTrip =
            InstrumentDescriptor.Parser.ParseFrom(
                descriptor.ToByteArray());

        Assert.Equal(
            descriptor.InstrumentId,
            roundTrip.InstrumentId);
        Assert.Equal(
            descriptor.Name,
            roundTrip.Name);
        Assert.Equal(
            descriptor.Kind,
            roundTrip.Kind);
        Assert.True(
            roundTrip.HasManufacturer);
        Assert.Equal(
            descriptor.Manufacturer,
            roundTrip.Manufacturer);
        Assert.True(
            roundTrip.HasModel);
        Assert.Equal(
            descriptor.Model,
            roundTrip.Model);
        Assert.True(
            roundTrip.HasSerialNumber);
        Assert.Equal(
            descriptor.SerialNumber,
            roundTrip.SerialNumber);
        Assert.True(
            roundTrip.HasFirmwareVersion);
        Assert.Equal(
            descriptor.FirmwareVersion,
            roundTrip.FirmwareVersion);
        Assert.True(
            roundTrip.HasHardwareRevision);
        Assert.Equal(
            descriptor.HardwareRevision,
            roundTrip.HardwareRevision);
        Assert.True(
            roundTrip.HasDescription);
        Assert.Equal(
            descriptor.Description,
            roundTrip.Description);
        Assert.Single(
            roundTrip.Properties);
        Assert.Single(
            roundTrip.Commands);
        Assert.Single(
            roundTrip.Events);
    }

    [Fact]
    public void EndpointDescriptor_RoundTrip_PreservesOptionalMetadata()
    {
        var descriptor =
            new EndpointDescriptor
            {
                EndpointId =
                    "endpoint-01",
                DisplayName =
                    "Endpoint 01",
                Description =
                    "Validation endpoint"
            };

        descriptor.Instruments.Add(
            new InstrumentDescriptor());

        EndpointDescriptor roundTrip =
            EndpointDescriptor.Parser.ParseFrom(
                descriptor.ToByteArray());

        Assert.Equal(
            "endpoint-01",
            roundTrip.EndpointId);
        Assert.True(
            roundTrip.HasDisplayName);
        Assert.Equal(
            "Endpoint 01",
            roundTrip.DisplayName);
        Assert.True(
            roundTrip.HasDescription);
        Assert.Equal(
            "Validation endpoint",
            roundTrip.Description);
        Assert.Single(
            roundTrip.Instruments);
    }

    [Fact]
    public void ConnectionStatus_DefinesNormalizedMembers()
    {
        FieldDescriptor[] fields =
            EndpointConnectionStatus.Descriptor.Fields
                .InDeclarationOrder()
                .ToArray();

        Assert.Collection(
            fields,
            field =>
            {
                Assert.Equal("state", field.Name);
                Assert.Equal(1, field.FieldNumber);
                Assert.Equal(FieldType.Enum, field.FieldType);
                Assert.Equal(
                    "hase.runtime.remote.v1.EndpointConnectionState",
                    field.EnumType.FullName);
                Assert.False(field.HasPresence);
            },
            field =>
            {
                Assert.Equal("changed_at_utc", field.Name);
                Assert.Equal(2, field.FieldNumber);
                Assert.Equal(FieldType.Message, field.FieldType);
                Assert.Equal(
                    Timestamp.Descriptor.FullName,
                    field.MessageType.FullName);
                Assert.True(field.HasPresence);
            },
            field =>
            {
                Assert.Equal("detail", field.Name);
                Assert.Equal(3, field.FieldNumber);
                Assert.Equal(FieldType.String, field.FieldType);
                Assert.True(field.HasPresence);
            });
    }

    [Fact]
    public void ConnectionState_DefinesStableNumericValues()
    {
        EnumValueDescriptor[] values =
            EndpointConnectionStatus.Descriptor
                .FindFieldByNumber(1)
                .EnumType
                .Values
                .ToArray();

        Assert.Collection(
            values,
            value => AssertEnumValue(
                value,
                "ENDPOINT_CONNECTION_STATE_UNSPECIFIED",
                0),
            value => AssertEnumValue(
                value,
                "ENDPOINT_CONNECTION_STATE_DISCONNECTED",
                1),
            value => AssertEnumValue(
                value,
                "ENDPOINT_CONNECTION_STATE_CONNECTING",
                2),
            value => AssertEnumValue(
                value,
                "ENDPOINT_CONNECTION_STATE_SYNCHRONIZING",
                3),
            value => AssertEnumValue(
                value,
                "ENDPOINT_CONNECTION_STATE_READY",
                4),
            value => AssertEnumValue(
                value,
                "ENDPOINT_CONNECTION_STATE_RECONNECTING",
                5),
            value => AssertEnumValue(
                value,
                "ENDPOINT_CONNECTION_STATE_FAULTED",
                6));
    }

    [Fact]
    public void ConnectionStatus_RoundTrip_PreservesOptionalMembers()
    {
        var status =
            new EndpointConnectionStatus
            {
                State =
                    (EndpointConnectionState)4,
                ChangedAtUtc =
                    Timestamp.FromDateTimeOffset(
                        new DateTimeOffset(
                            2026,
                            7,
                            25,
                            10,
                            30,
                            0,
                            TimeSpan.Zero)),
                Detail =
                    "Ready"
            };

        EndpointConnectionStatus roundTrip =
            EndpointConnectionStatus.Parser.ParseFrom(
                status.ToByteArray());

        Assert.Equal(
            4,
            (int)roundTrip.State);
        Assert.Equal(
            status.ChangedAtUtc,
            roundTrip.ChangedAtUtc);
        Assert.True(
            roundTrip.HasDetail);
        Assert.Equal(
            "Ready",
            roundTrip.Detail);
    }

    [Fact]
    public void SnapshotResponse_RoundTrip_PreservesDefinedMembers()
    {
        var response =
            new GetSnapshotResponse
            {
                RuntimeHostId =
                    "runtime-host-58c50d84-c4ad-47a0-b7c6-1eeed3483593",
                ApiVersion =
                    new RuntimeHostApiVersion
                    {
                        Major = 1,
                        Minor = 0
                    }
            };

        response.Endpoints.Add(
            new PublishedRuntimeEndpointSnapshot
            {
                EndpointId =
                    "endpoint-01",
                AttachmentGeneration =
                    "868e79d4-b1a4-4a63-81cd-5a800d9ba3fd",
                Descriptor_ =
                    new EndpointDescriptor(),
                ConnectionStatus =
                    new EndpointConnectionStatus()
            });

        GetSnapshotResponse roundTrip =
            GetSnapshotResponse.Parser.ParseFrom(
                response.ToByteArray());

        Assert.Equal(
            response.RuntimeHostId,
            roundTrip.RuntimeHostId);
        Assert.Equal(
            1u,
            roundTrip.ApiVersion.Major);
        Assert.Equal(
            0u,
            roundTrip.ApiVersion.Minor);
        PublishedRuntimeEndpointSnapshot endpoint =
            Assert.Single(
                roundTrip.Endpoints);

        Assert.Equal(
            "endpoint-01",
            endpoint.EndpointId);
        Assert.Equal(
            "868e79d4-b1a4-4a63-81cd-5a800d9ba3fd",
            endpoint.AttachmentGeneration);
        Assert.NotNull(
            endpoint.Descriptor_);
        Assert.NotNull(
            endpoint.ConnectionStatus);
    }

    private static void AssertEnumValue(
        EnumValueDescriptor value,
        string expectedName,
        int expectedNumber)
    {
        Assert.Equal(
            expectedName,
            value.Name);
        Assert.Equal(
            expectedNumber,
            value.Number);
    }

    private static void AssertPathDescriptor(
        MessageDescriptor descriptor)
    {
        Assert.Collection(
            descriptor.Fields.InDeclarationOrder(),
            field => AssertField(
                field,
                "path_segments",
                1,
                FieldType.String,
                false,
                true),
            field => AssertField(
                field,
                "display_name",
                2,
                FieldType.String,
                false,
                false),
            field => AssertField(
                field,
                "description",
                3,
                FieldType.String,
                true,
                false));
    }

    private static void AssertField(
        FieldDescriptor field,
        string expectedName,
        int expectedNumber,
        FieldType expectedType,
        bool expectedPresence,
        bool expectedRepeated)
    {
        Assert.Equal(
            expectedName,
            field.Name);
        Assert.Equal(
            expectedNumber,
            field.FieldNumber);
        Assert.Equal(
            expectedType,
            field.FieldType);
        Assert.Equal(
            expectedPresence,
            field.HasPresence);
        Assert.Equal(
            expectedRepeated,
            field.IsRepeated);
    }

    private static void AssertMessageField(
        FieldDescriptor field,
        string expectedName,
        int expectedNumber,
        MessageDescriptor expectedMessage,
        bool expectedRepeated)
    {
        AssertField(
            field,
            expectedName,
            expectedNumber,
            FieldType.Message,
            !expectedRepeated,
            expectedRepeated);
        Assert.Equal(
            expectedMessage.FullName,
            field.MessageType.FullName);
    }
}
