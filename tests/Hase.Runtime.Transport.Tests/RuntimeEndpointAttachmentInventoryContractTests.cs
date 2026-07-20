using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class RuntimeEndpointAttachmentInventoryContractTests
{
    [Fact]
    public void Inventory_ShouldSupportAsyncDisposal()
    {
        bool supportsAsyncDisposal =
            typeof(IAsyncDisposable)
                .IsAssignableFrom(
                    typeof(IRuntimeEndpointAttachmentInventory));

        Assert.True(
            supportsAsyncDisposal);
    }

    [Fact]
    public void Inventory_ShouldExposeAttachmentOperation()
    {
        System.Reflection.MethodInfo? method =
            typeof(IRuntimeEndpointAttachmentInventory)
                .GetMethod(
                    nameof(IRuntimeEndpointAttachmentInventory.AttachAsync));

        Assert.NotNull(
            method);

        Assert.Equal(
            typeof(Task<RuntimeEndpointAttachmentInventoryEntry>),
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

    [Fact]
    public void Inventory_ShouldExposeFindAndSnapshotListOperations()
    {
        System.Reflection.MethodInfo? findMethod =
            typeof(IRuntimeEndpointAttachmentInventory)
                .GetMethod(
                    nameof(IRuntimeEndpointAttachmentInventory.Find));

        System.Reflection.MethodInfo? listMethod =
            typeof(IRuntimeEndpointAttachmentInventory)
                .GetMethod(
                    nameof(IRuntimeEndpointAttachmentInventory.List));

        Assert.NotNull(
            findMethod);

        Assert.Equal(
            typeof(RuntimeEndpointAttachmentInventoryEntry),
            findMethod.ReturnType);

        System.Reflection.ParameterInfo findParameter =
            Assert.Single(
                findMethod.GetParameters());

        Assert.Equal(
            typeof(EndpointId),
            findParameter.ParameterType);

        Assert.NotNull(
            listMethod);

        Assert.Equal(
            typeof(IReadOnlyList<RuntimeEndpointAttachmentInventoryEntry>),
            listMethod.ReturnType);

        Assert.Empty(
            listMethod.GetParameters());
    }

    [Fact]
    public void Inventory_ShouldExposeDetachmentOperation()
    {
        System.Reflection.MethodInfo? method =
            typeof(IRuntimeEndpointAttachmentInventory)
                .GetMethod(
                    nameof(IRuntimeEndpointAttachmentInventory.DetachAsync));

        Assert.NotNull(
            method);

        Assert.Equal(
            typeof(Task<bool>),
            method.ReturnType);

        System.Reflection.ParameterInfo[] parameters =
            method.GetParameters();

        Assert.Equal(
            2,
            parameters.Length);

        Assert.Equal(
            typeof(EndpointId),
            parameters[0].ParameterType);

        Assert.Equal(
            typeof(CancellationToken),
            parameters[1].ParameterType);

        Assert.True(
            parameters[1].IsOptional);
    }

    [Fact]
    public void InventoryEntry_ShouldUseRuntimeEndpointAuthoritativeIdentity()
    {
        var authoritativeEndpointId =
            new EndpointId(
                "authoritative-endpoint");

        var runtimeEndpoint =
            new RuntimeEndpoint(
                new RuntimeContext(),
                new EndpointDescriptor(
                    authoritativeEndpointId));

        var attachmentSession =
            new TestEndpointAttachmentSession(
                runtimeEndpoint);

        var entry =
            new RuntimeEndpointAttachmentInventoryEntry(
                attachmentSession);

        Assert.Equal(
            authoritativeEndpointId,
            entry.EndpointId);

        Assert.Same(
            runtimeEndpoint,
            entry.RuntimeEndpoint);

        Assert.Same(
            attachmentSession,
            entry.AttachmentSession);
    }

    [Fact]
    public void InventoryEntry_ShouldExposeGetOnlyState()
    {
        Type entryType =
            typeof(RuntimeEndpointAttachmentInventoryEntry);

        Assert.False(
            entryType.GetProperty(
                nameof(RuntimeEndpointAttachmentInventoryEntry.EndpointId))!
                .CanWrite);

        Assert.False(
            entryType.GetProperty(
                nameof(RuntimeEndpointAttachmentInventoryEntry.RuntimeEndpoint))!
                .CanWrite);

        Assert.False(
            entryType.GetProperty(
                nameof(RuntimeEndpointAttachmentInventoryEntry.AttachmentSession))!
                .CanWrite);
    }

    private sealed class TestEndpointAttachmentSession
        : IEndpointAttachmentSession
    {
        public TestEndpointAttachmentSession(
            RuntimeEndpoint runtimeEndpoint)
        {
            RuntimeEndpoint =
                runtimeEndpoint;

            Request =
                null!;
        }

        public EndpointAttachmentRequest Request
        {
            get;
        }

        public RuntimeEndpoint RuntimeEndpoint
        {
            get;
        }

        public Task ShutdownAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}