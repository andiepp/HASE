using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Transport.Attachment;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class EndpointAttachmentPropertyOperationContractTests
{
    [Fact]
    public void OperationPort_ExposesCancellableLogicalRead()
    {
        System.Reflection.MethodInfo method =
            Assert.Single(
                typeof(IEndpointAttachmentPropertyOperations)
                    .GetMethods(),
                candidate =>
                    candidate.Name
                    == nameof(
                        IEndpointAttachmentPropertyOperations.ReadAsync));

        Assert.Equal(
            typeof(Task<EndpointAttachmentPropertyOperationResult>),
            method.ReturnType);

        System.Reflection.ParameterInfo[] parameters =
            method.GetParameters();

        Assert.Collection(
            parameters,
            instrument =>
                Assert.Equal(
                    typeof(InstrumentId),
                    instrument.ParameterType),
            property =>
                Assert.Equal(
                    typeof(PropertyId),
                    property.ParameterType),
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
    public void OperationPort_ExposesCancellableLogicalWrite()
    {
        System.Reflection.MethodInfo method =
            Assert.Single(
                typeof(IEndpointAttachmentPropertyOperations)
                    .GetMethods(),
                candidate =>
                    candidate.Name
                    == nameof(
                        IEndpointAttachmentPropertyOperations.WriteAsync));

        Assert.Equal(
            typeof(Task<EndpointAttachmentPropertyOperationResult>),
            method.ReturnType);

        System.Reflection.ParameterInfo[] parameters =
            method.GetParameters();

        Assert.Collection(
            parameters,
            instrument =>
                Assert.Equal(
                    typeof(InstrumentId),
                    instrument.ParameterType),
            property =>
                Assert.Equal(
                    typeof(PropertyId),
                    property.ParameterType),
            requestedValue =>
                Assert.Equal(
                    typeof(object),
                    requestedValue.ParameterType),
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
    public void OperationResult_Success_ContainsConfirmedValue()
    {
        PropertyValue confirmedValue =
            new(
                23.5,
                DateTimeOffset.UtcNow);

        EndpointAttachmentPropertyOperationResult result =
            EndpointAttachmentPropertyOperationResult.Successful(
                confirmedValue);

        Assert.Equal(
            EndpointAttachmentPropertyOperationStatus.Success,
            result.Status);

        Assert.True(
            result.IsSuccess);

        Assert.Same(
            confirmedValue,
            result.ConfirmedValue);

        Assert.Null(
            result.Diagnostic);
    }

    [Fact]
    public void OperationResult_NullConfirmedValue_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () =>
                EndpointAttachmentPropertyOperationResult.Successful(
                    null!));
    }

    [Fact]
    public void OperationResult_Failure_ContainsNoValueAndTrimsDiagnostic()
    {
        EndpointAttachmentPropertyOperationResult result =
            EndpointAttachmentPropertyOperationResult.Failed(
                EndpointAttachmentPropertyOperationStatus.Unavailable,
                " endpoint unavailable ");

        Assert.Equal(
            EndpointAttachmentPropertyOperationStatus.Unavailable,
            result.Status);

        Assert.False(
            result.IsSuccess);

        Assert.Null(
            result.ConfirmedValue);

        Assert.Equal(
            "endpoint unavailable",
            result.Diagnostic);
    }

    [Theory]
    [InlineData(EndpointAttachmentPropertyOperationStatus.NotSupported)]
    [InlineData(EndpointAttachmentPropertyOperationStatus.InvalidValue)]
    [InlineData(EndpointAttachmentPropertyOperationStatus.Rejected)]
    [InlineData(EndpointAttachmentPropertyOperationStatus.Failure)]
    [InlineData(EndpointAttachmentPropertyOperationStatus.Unavailable)]
    [InlineData(EndpointAttachmentPropertyOperationStatus.TimedOut)]
    public void OperationResult_DefinedFailureStatus_IsAccepted(
        EndpointAttachmentPropertyOperationStatus status)
    {
        EndpointAttachmentPropertyOperationResult result =
            EndpointAttachmentPropertyOperationResult.Failed(
                status);

        Assert.Equal(
            status,
            result.Status);
    }

    [Fact]
    public void OperationResult_SuccessFailureStatus_Throws()
    {
        Assert.Throws<ArgumentException>(
            () =>
                EndpointAttachmentPropertyOperationResult.Failed(
                    EndpointAttachmentPropertyOperationStatus.Success));
    }

    [Fact]
    public void OperationResult_UndefinedFailureStatus_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                EndpointAttachmentPropertyOperationResult.Failed(
                    (EndpointAttachmentPropertyOperationStatus)999));
    }
}