using Google.Protobuf;
using Google.Protobuf.Reflection;
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
    public void EndpointNestedMessages_StartWithoutPrematureFields()
    {
        Assert.Empty(
            EndpointDescriptor.Descriptor.Fields.InDeclarationOrder());
        Assert.Empty(
            EndpointConnectionStatus.Descriptor.Fields.InDeclarationOrder());
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
}
