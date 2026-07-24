using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Northbound;

namespace Hase.Runtime.Tests.Northbound;

public sealed class RuntimeHostCommandContractTests
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

        DescriptorPath commandPath =
            new(
                "Controller",
                "ToggleLed");

        var target =
            new RuntimeHostCommandTarget(
                endpointId,
                generation,
                instrumentId,
                commandPath);

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
            commandPath,
            target.CommandPath);
    }

    [Fact]
    public void Target_NullEndpointId_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RuntimeHostCommandTarget(
                null!,
                RuntimeEndpointAttachmentGeneration.CreateNew(),
                new InstrumentId(
                    "instrument-one"),
                CreateCommandPath()));
    }

    [Fact]
    public void Target_NullGeneration_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RuntimeHostCommandTarget(
                new EndpointId(
                    "endpoint-one"),
                null!,
                new InstrumentId(
                    "instrument-one"),
                CreateCommandPath()));
    }

    [Fact]
    public void Target_NullInstrumentId_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RuntimeHostCommandTarget(
                new EndpointId(
                    "endpoint-one"),
                RuntimeEndpointAttachmentGeneration.CreateNew(),
                null!,
                CreateCommandPath()));
    }

    [Fact]
    public void Target_NullCommandPath_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RuntimeHostCommandTarget(
                new EndpointId(
                    "endpoint-one"),
                RuntimeEndpointAttachmentGeneration.CreateNew(),
                new InstrumentId(
                    "instrument-one"),
                null!));
    }

    [Theory]
    [InlineData(RuntimeHostCommandOperationStatus.Success)]
    [InlineData(RuntimeHostCommandOperationStatus.AttachmentNotCurrent)]
    [InlineData(RuntimeHostCommandOperationStatus.InstrumentNotFound)]
    [InlineData(RuntimeHostCommandOperationStatus.CommandNotFound)]
    [InlineData(RuntimeHostCommandOperationStatus.ArgumentNotSupported)]
    [InlineData(RuntimeHostCommandOperationStatus.EndpointUnavailable)]
    [InlineData(RuntimeHostCommandOperationStatus.EndpointRejected)]
    [InlineData(RuntimeHostCommandOperationStatus.EndpointFailure)]
    [InlineData(RuntimeHostCommandOperationStatus.TimedOut)]
    public void Status_IsDefined(
        RuntimeHostCommandOperationStatus status)
    {
        Assert.True(
            Enum.IsDefined(
                status));
    }

    [Fact]
    public void CommandOperationResult_SuccessWithoutReturnValue_IsSuccessful()
    {
        RuntimeHostCommandOperationResult result =
            RuntimeHostCommandOperationResult.Successful();

        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            RuntimeHostCommandOperationStatus.Success,
            result.Status);

        Assert.Null(
            result.ReturnValue);

        Assert.Null(
            result.Diagnostic);
    }

    [Fact]
    public void CommandOperationResult_SuccessWithReturnValue_ContainsValue()
    {
        object returnValue =
            42;

        RuntimeHostCommandOperationResult result =
            RuntimeHostCommandOperationResult.Successful(
                returnValue);

        Assert.True(
            result.IsSuccess);

        Assert.Same(
            returnValue,
            result.ReturnValue);
    }

    [Fact]
    public void CommandOperationResult_Failure_ContainsNoReturnValue()
    {
        RuntimeHostCommandOperationResult result =
            RuntimeHostCommandOperationResult.Failed(
                RuntimeHostCommandOperationStatus.EndpointUnavailable,
                "  Endpoint is not Ready.  ");

        Assert.False(
            result.IsSuccess);

        Assert.Equal(
            RuntimeHostCommandOperationStatus.EndpointUnavailable,
            result.Status);

        Assert.Null(
            result.ReturnValue);

        Assert.Equal(
            "Endpoint is not Ready.",
            result.Diagnostic);
    }

    [Fact]
    public void CommandOperationResult_WhitespaceDiagnostic_IsRemoved()
    {
        RuntimeHostCommandOperationResult result =
            RuntimeHostCommandOperationResult.Failed(
                RuntimeHostCommandOperationStatus.EndpointFailure,
                "   ");

        Assert.Null(
            result.Diagnostic);
    }

    [Fact]
    public void CommandOperationResult_SuccessFailureStatus_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => RuntimeHostCommandOperationResult.Failed(
                RuntimeHostCommandOperationStatus.Success));
    }

    [Fact]
    public void CommandOperationResult_UndefinedFailureStatus_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RuntimeHostCommandOperationResult.Failed(
                (RuntimeHostCommandOperationStatus)999));
    }

    private static DescriptorPath CreateCommandPath()
    {
        return new DescriptorPath(
            "Controller",
            "ToggleLed");
    }
}