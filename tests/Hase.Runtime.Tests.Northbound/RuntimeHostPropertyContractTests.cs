using Hase.Core.Domain.Data;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Connections;
using Hase.Runtime.Northbound;

namespace Hase.Runtime.Tests.Northbound;

public sealed class RuntimeHostPropertyContractTests
{
    [Fact]
    public void Target_StoresRequiredIdentities()
    {
        EndpointId endpointId =
            new(
                "endpoint-one");

        RuntimeEndpointAttachmentGeneration generation =
            RuntimeEndpointAttachmentGeneration.CreateNew();

        InstrumentId instrumentId =
            new(
                "instrument-one");

        PropertyId propertyId =
            new(
                "property-one");

        var target =
            new RuntimeHostPropertyTarget(
                endpointId,
                generation,
                instrumentId,
                propertyId);

        Assert.Same(
            endpointId,
            target.EndpointId);

        Assert.Same(
            generation,
            target.AttachmentGeneration);

        Assert.Same(
            instrumentId,
            target.InstrumentId);

        Assert.Same(
            propertyId,
            target.PropertyId);
    }

    [Fact]
    public void Target_NullEndpointId_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RuntimeHostPropertyTarget(
                null!,
                RuntimeEndpointAttachmentGeneration.CreateNew(),
                new InstrumentId(
                    "instrument-one"),
                new PropertyId(
                    "property-one")));
    }

    [Fact]
    public void Target_NullGeneration_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RuntimeHostPropertyTarget(
                new EndpointId(
                    "endpoint-one"),
                null!,
                new InstrumentId(
                    "instrument-one"),
                new PropertyId(
                    "property-one")));
    }

    [Fact]
    public void Target_NullInstrumentId_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RuntimeHostPropertyTarget(
                new EndpointId(
                    "endpoint-one"),
                RuntimeEndpointAttachmentGeneration.CreateNew(),
                null!,
                new PropertyId(
                    "property-one")));
    }

    [Fact]
    public void Target_NullPropertyId_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RuntimeHostPropertyTarget(
                new EndpointId(
                    "endpoint-one"),
                RuntimeEndpointAttachmentGeneration.CreateNew(),
                new InstrumentId(
                    "instrument-one"),
                null!));
    }

    [Fact]
    public void Snapshot_KnownValue_StoresImmutableState()
    {
        RuntimeHostPropertyTarget target =
            CreateTarget();

        PropertyDescriptor descriptor =
            CreateDescriptor(
                target.PropertyId);

        EndpointConnectionStatus connectionStatus =
            new(
                EndpointConnectionState.Faulted,
                DateTimeOffset.UnixEpoch,
                "Recovering.");

        PropertyValue currentValue =
            new(
                42,
                DateTimeOffset.UnixEpoch);

        var snapshot =
            new PublishedRuntimePropertySnapshot(
                target,
                descriptor,
                connectionStatus,
                currentValue);

        Assert.Same(
            target,
            snapshot.Target);

        Assert.Same(
            descriptor,
            snapshot.Descriptor);

        Assert.Same(
            connectionStatus,
            snapshot.ConnectionStatus);

        Assert.Same(
            currentValue,
            snapshot.CurrentValue);

        Assert.True(
            snapshot.IsKnown);
    }

    [Fact]
    public void Snapshot_UnknownValue_IsNotKnown()
    {
        RuntimeHostPropertyTarget target =
            CreateTarget();

        var snapshot =
            new PublishedRuntimePropertySnapshot(
                target,
                CreateDescriptor(
                    target.PropertyId),
                new EndpointConnectionStatus(
                    EndpointConnectionState.Disconnected),
                currentValue: null);

        Assert.Null(
            snapshot.CurrentValue);

        Assert.False(
            snapshot.IsKnown);
    }

    [Fact]
    public void Snapshot_MismatchedDescriptor_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new PublishedRuntimePropertySnapshot(
                CreateTarget(),
                CreateDescriptor(
                    new PropertyId(
                        "different-property")),
                new EndpointConnectionStatus(
                    EndpointConnectionState.Ready),
                currentValue: null));
    }

    [Fact]
    public void CachedResult_Success_ContainsSnapshot()
    {
        RuntimeHostPropertyTarget target =
            CreateTarget();

        var snapshot =
            new PublishedRuntimePropertySnapshot(
                target,
                CreateDescriptor(
                    target.PropertyId),
                new EndpointConnectionStatus(
                    EndpointConnectionState.Ready),
                currentValue: null);

        RuntimeHostCachedPropertyResult result =
            RuntimeHostCachedPropertyResult.Successful(
                snapshot);

        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            RuntimeHostPropertyOperationStatus.Success,
            result.Status);

        Assert.Same(
            snapshot,
            result.Snapshot);

        Assert.Null(
            result.Diagnostic);
    }

    [Fact]
    public void CachedResult_NullSuccessSnapshot_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => RuntimeHostCachedPropertyResult.Successful(
                null!));
    }

    [Fact]
    public void CachedResult_Failure_ContainsNoSnapshot()
    {
        RuntimeHostCachedPropertyResult result =
            RuntimeHostCachedPropertyResult.Failed(
                RuntimeHostPropertyOperationStatus.AttachmentNotCurrent,
                "  Attachment ended.  ");

        Assert.False(
            result.IsSuccess);

        Assert.Null(
            result.Snapshot);

        Assert.Equal(
            "Attachment ended.",
            result.Diagnostic);
    }

    [Fact]
    public void CachedResult_SuccessFailureStatus_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => RuntimeHostCachedPropertyResult.Failed(
                RuntimeHostPropertyOperationStatus.Success));
    }

    [Fact]
    public void PropertyOperationResult_Success_ContainsConfirmedValue()
    {
        PropertyValue confirmedValue =
            new(
                42,
                DateTimeOffset.UnixEpoch);

        RuntimeHostPropertyOperationResult result =
            RuntimeHostPropertyOperationResult.Successful(
                confirmedValue);

        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            RuntimeHostPropertyOperationStatus.Success,
            result.Status);

        Assert.Same(
            confirmedValue,
            result.ConfirmedValue);

        Assert.Null(
            result.Diagnostic);
    }

    [Fact]
    public void PropertyOperationResult_NullConfirmedValue_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => RuntimeHostPropertyOperationResult.Successful(
                null!));
    }

    [Fact]
    public void PropertyOperationResult_Failure_ContainsNoValue()
    {
        RuntimeHostPropertyOperationResult result =
            RuntimeHostPropertyOperationResult.Failed(
                RuntimeHostPropertyOperationStatus.EndpointUnavailable,
                "  Endpoint is not Ready.  ");

        Assert.False(
            result.IsSuccess);

        Assert.Null(
            result.ConfirmedValue);

        Assert.Equal(
            "Endpoint is not Ready.",
            result.Diagnostic);
    }

    [Fact]
    public void PropertyOperationResult_UndefinedFailureStatus_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RuntimeHostPropertyOperationResult.Failed(
                (RuntimeHostPropertyOperationStatus)999));
    }

    private static RuntimeHostPropertyTarget CreateTarget()
    {
        return new RuntimeHostPropertyTarget(
            new EndpointId(
                "endpoint-one"),
            RuntimeEndpointAttachmentGeneration.CreateNew(),
            new InstrumentId(
                "instrument-one"),
            new PropertyId(
                "property-one"));
    }

    private static PropertyDescriptor CreateDescriptor(
        PropertyId propertyId)
    {
        return new PropertyDescriptor(
            propertyId,
            new DescriptorPath(
                "Instrument",
                "Property"),
            "Property",
            new StringDataDescriptor());
    }
}