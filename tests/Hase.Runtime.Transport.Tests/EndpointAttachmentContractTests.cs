using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class EndpointAttachmentContractTests
{
    [Fact]
    public void AttachmentSession_ShouldSupportAsyncDisposal()
    {
        // Act
        bool supportsAsyncDisposal =
            typeof(IAsyncDisposable)
                .IsAssignableFrom(
                    typeof(IEndpointAttachmentSession));

        // Assert
        Assert.True(
            supportsAsyncDisposal);
    }

    [Fact]
    public void AttachmentSession_ShouldExposeRequestAndRuntimeEndpoint()
    {
        // Act
        Type? requestType =
            typeof(IEndpointAttachmentSession)
                .GetProperty(
                    nameof(IEndpointAttachmentSession.Request))
                ?.PropertyType;

        Type? runtimeEndpointType =
            typeof(IEndpointAttachmentSession)
                .GetProperty(
                    nameof(IEndpointAttachmentSession.RuntimeEndpoint))
                ?.PropertyType;

        // Assert
        Assert.Equal(
            typeof(EndpointAttachmentRequest),
            requestType);

        Assert.Equal(
            typeof(RuntimeEndpoint),
            runtimeEndpointType);
    }

    [Fact]
    public void AttachmentSession_ShouldExposeCancellableShutdown()
    {
        // Act
        System.Reflection.MethodInfo? method =
            typeof(IEndpointAttachmentSession)
                .GetMethod(
                    nameof(IEndpointAttachmentSession.ShutdownAsync));

        // Assert
        Assert.NotNull(
            method);

        Assert.Equal(
            typeof(Task),
            method.ReturnType);

        System.Reflection.ParameterInfo parameter =
            Assert.Single(
                method.GetParameters());

        Assert.Equal(
            typeof(CancellationToken),
            parameter.ParameterType);

        Assert.True(
            parameter.IsOptional);
    }

    [Fact]
    public void AttachmentService_ShouldExposeCancellableAttachment()
    {
        // Act
        System.Reflection.MethodInfo? method =
            typeof(IEndpointAttachmentService)
                .GetMethod(
                    nameof(IEndpointAttachmentService.AttachAsync));

        // Assert
        Assert.NotNull(
            method);

        Assert.Equal(
            typeof(Task<IEndpointAttachmentSession>),
            method.ReturnType);

        System.Reflection.ParameterInfo[] parameters =
            method.GetParameters();

        Assert.Equal(
            2,
            parameters.Length);

        Assert.Equal(
            typeof(EndpointAttachmentRequest),
            parameters[0].ParameterType);

        Assert.Equal(
            typeof(CancellationToken),
            parameters[1].ParameterType);

        Assert.True(
            parameters[1].IsOptional);
    }
}