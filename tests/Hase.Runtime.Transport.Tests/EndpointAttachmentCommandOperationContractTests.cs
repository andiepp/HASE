using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Transport.Attachment;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class EndpointAttachmentCommandOperationContractTests
{
    [Fact]
    public void OperationPort_ExposesCancellableLogicalExecution()
    {
        System.Reflection.MethodInfo method =
            Assert.Single(
                typeof(IEndpointAttachmentCommandOperations)
                    .GetMethods(),
                candidate =>
                    candidate.Name
                    == nameof(
                        IEndpointAttachmentCommandOperations.ExecuteAsync));

        Assert.Equal(
            typeof(Task<EndpointAttachmentCommandOperationResult>),
            method.ReturnType);

        System.Reflection.ParameterInfo[] parameters =
            method.GetParameters();

        Assert.Collection(
            parameters,
            instrument =>
                Assert.Equal(
                    typeof(InstrumentId),
                    instrument.ParameterType),
            command =>
                Assert.Equal(
                    typeof(DescriptorPath),
                    command.ParameterType),
            argument =>
                Assert.Equal(
                    typeof(object),
                    argument.ParameterType),
            cancellation =>
            {
                Assert.Equal(
                    typeof(CancellationToken),
                    cancellation.ParameterType);

                Assert.True(
                    cancellation.IsOptional);
            });
    }

    [Fact]
    public void OperationResult_SuccessWithoutReturnValue_IsSuccessful()
    {
        EndpointAttachmentCommandOperationResult result =
            EndpointAttachmentCommandOperationResult.Successful();

        Assert.Equal(
            EndpointAttachmentCommandOperationStatus.Success,
            result.Status);

        Assert.True(
            result.IsSuccess);

        Assert.Null(
            result.ReturnValue);

        Assert.Null(
            result.Diagnostic);
    }

    [Fact]
    public void OperationResult_SuccessWithReturnValue_ContainsValue()
    {
        object returnValue =
            42;

        EndpointAttachmentCommandOperationResult result =
            EndpointAttachmentCommandOperationResult.Successful(
                returnValue);

        Assert.True(
            result.IsSuccess);

        Assert.Same(
            returnValue,
            result.ReturnValue);
    }

    [Fact]
    public void OperationResult_Failure_ContainsNoValueAndTrimsDiagnostic()
    {
        EndpointAttachmentCommandOperationResult result =
            EndpointAttachmentCommandOperationResult.Failed(
                EndpointAttachmentCommandOperationStatus.Unavailable,
                " endpoint unavailable ");

        Assert.Equal(
            EndpointAttachmentCommandOperationStatus.Unavailable,
            result.Status);

        Assert.False(
            result.IsSuccess);

        Assert.Null(
            result.ReturnValue);

        Assert.Equal(
            "endpoint unavailable",
            result.Diagnostic);
    }

    [Theory]
    [InlineData(
        EndpointAttachmentCommandOperationStatus.ArgumentNotSupported)]
    [InlineData(EndpointAttachmentCommandOperationStatus.Rejected)]
    [InlineData(EndpointAttachmentCommandOperationStatus.Failure)]
    [InlineData(EndpointAttachmentCommandOperationStatus.Unavailable)]
    [InlineData(EndpointAttachmentCommandOperationStatus.TimedOut)]
    public void OperationResult_DefinedFailureStatus_IsAccepted(
        EndpointAttachmentCommandOperationStatus status)
    {
        EndpointAttachmentCommandOperationResult result =
            EndpointAttachmentCommandOperationResult.Failed(
                status);

        Assert.Equal(
            status,
            result.Status);
    }

    [Fact]
    public void OperationResult_WhitespaceDiagnostic_IsRemoved()
    {
        EndpointAttachmentCommandOperationResult result =
            EndpointAttachmentCommandOperationResult.Failed(
                EndpointAttachmentCommandOperationStatus.Failure,
                "   ");

        Assert.Null(
            result.Diagnostic);
    }

    [Fact]
    public void OperationResult_SuccessFailureStatus_Throws()
    {
        Assert.Throws<ArgumentException>(
            () =>
                EndpointAttachmentCommandOperationResult.Failed(
                    EndpointAttachmentCommandOperationStatus.Success));
    }

    [Fact]
    public void OperationResult_UndefinedFailureStatus_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                EndpointAttachmentCommandOperationResult.Failed(
                    (EndpointAttachmentCommandOperationStatus)999));
    }
}