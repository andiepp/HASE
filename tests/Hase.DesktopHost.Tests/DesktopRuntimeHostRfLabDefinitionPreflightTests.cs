using System.IO;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.DesktopHost.App.Hosting;
using Hase.DesktopHost.Configuration;
using Hase.Mcnf.RfLab;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Hase.Transport.Serial;

namespace Hase.DesktopHost.Tests;

public sealed class DesktopRuntimeHostRfLabDefinitionPreflightTests
{
    [Fact]
    public async Task ResolveAsync_ExactVersionOne_ShouldReturnRepositoryDefinition()
    {
        EndpointDescriptorDefinition definition =
            RfLabReadOnlyDefinition.EndpointDefinition;
        var repository = new RecordingRepository(definition);

        DesktopRuntimeHostRfLabEndpointPlan plan =
            await DesktopRuntimeHostRfLabDefinitionPreflight.ResolveAsync(
                Profile("rflab-01", "external-target"),
                repository);

        Assert.Equal(new EndpointId("rflab-01"), plan.ExpectedEndpointId);
        Assert.Same(definition, plan.Definition);
        Assert.Equal(RfLabReadOnlyDefinition.Reference, repository.Reference);
    }

    [Fact]
    public async Task ResolveAsync_ExactVersionTwo_ShouldReturnRepositoryDefinition()
    {
        EndpointDescriptorDefinition definition =
            RfLabControlledSignalDefinition.EndpointDefinition;
        var repository = new RecordingRepository(definition);
        var profile = new DesktopRuntimeHostRfLabSerialEndpointProfile(
            "rflab-01",
            RfLabControlledSignalDefinition.Reference.Id.Value,
            RfLabControlledSignalDefinition.Reference.Version,
            "external-target",
            115200);

        DesktopRuntimeHostRfLabEndpointPlan plan =
            await DesktopRuntimeHostRfLabDefinitionPreflight.ResolveAsync(
                profile,
                repository);

        Assert.Same(definition, plan.Definition);
        Assert.Equal(RfLabControlledSignalDefinition.Reference, repository.Reference);
    }

    [Fact]
    public async Task ResolveAsync_DefinitionReferenceMismatchRejects()
    {
        var repository = new RecordingRepository(
            RfLabReadOnlyDefinition.EndpointDefinition);
        var profile = new DesktopRuntimeHostRfLabSerialEndpointProfile(
            "rflab-01",
            RfLabControlledSignalDefinition.Reference.Id.Value,
            RfLabControlledSignalDefinition.Reference.Version,
            "external-target",
            115200);

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
            await DesktopRuntimeHostRfLabDefinitionPreflight.ResolveAsync(
                profile,
                repository));
    }

    [Theory]
    [InlineData("unknown-definition", 1)]
    [InlineData("rflab-signal-lab", 3)]
    public async Task ResolveAsync_UnsupportedReference_ShouldRejectBeforeRepositoryAccess(
        string definitionId,
        ushort definitionVersion)
    {
        var repository = new RecordingRepository(null);
        var profile = new DesktopRuntimeHostRfLabSerialEndpointProfile(
            "rflab-01", definitionId, definitionVersion, "external-target", 115200);

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
            await DesktopRuntimeHostRfLabDefinitionPreflight.ResolveAsync(
                profile,
                repository));

        Assert.Null(repository.Reference);
    }

    [Fact]
    public async Task ResolveAsync_MissingExactDefinition_ShouldReject()
    {
        var repository = new RecordingRepository(null);

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
            await DesktopRuntimeHostRfLabDefinitionPreflight.ResolveAsync(
                Profile("rflab-01", "external-target"),
                repository));

        Assert.Equal(RfLabReadOnlyDefinition.Reference, repository.Reference);
    }

    [Fact]
    public async Task ResolveAsync_PreCancelled_ShouldNotAccessRepository()
    {
        var repository = new RecordingRepository(
            RfLabReadOnlyDefinition.EndpointDefinition);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await DesktopRuntimeHostRfLabDefinitionPreflight.ResolveAsync(
                Profile("rflab-01", "external-target"),
                repository,
                cancellation.Token));

        Assert.Null(repository.Reference);
    }

    [Fact]
    public async Task ResolveAsync_FailureAndPlanText_ShouldNotExposeSerialTarget()
    {
        const string serialTarget = "sensitive-external-target";
        InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(async () =>
            await DesktopRuntimeHostRfLabDefinitionPreflight.ResolveAsync(
                Profile("rflab-01", serialTarget),
                new RecordingRepository(null)));

        Assert.DoesNotContain(serialTarget, exception.ToString(), StringComparison.Ordinal);

        DesktopRuntimeHostRfLabEndpointPlan plan =
            await DesktopRuntimeHostRfLabDefinitionPreflight.ResolveAsync(
                Profile("rflab-01", serialTarget),
                new RecordingRepository(RfLabReadOnlyDefinition.EndpointDefinition));

        Assert.DoesNotContain(serialTarget, plan.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResolveAllAsync_ValidProfiles_ShouldPreserveProfileOrder()
    {
        var repository = new RecordingRepository(
            RfLabReadOnlyDefinition.EndpointDefinition);

        IReadOnlyList<DesktopRuntimeHostRfLabEndpointPlan> plans =
            await DesktopRuntimeHostRfLabDefinitionPreflight.ResolveAllAsync(
                [Profile("first", "first-target"), Profile("second", "second-target")],
                repository);

        Assert.Equal(
            new[] { "first", "second" },
            plans.Select(plan => plan.ExpectedEndpointId.Value));
    }

    [Fact]
    public async Task ResolveAllAsync_EmptyComposition_ShouldNotAccessRepository()
    {
        var repository = new RecordingRepository(
            RfLabReadOnlyDefinition.EndpointDefinition);

        IReadOnlyList<DesktopRuntimeHostRfLabEndpointPlan> plans =
            await DesktopRuntimeHostRfLabDefinitionPreflight.ResolveAllAsync(
                [],
                repository);

        Assert.Empty(plans);
        Assert.Null(repository.Reference);
    }

    [Fact]
    public async Task ResolveAllAsync_InvalidProfile_ShouldStopBeforeLaterProfiles()
    {
        var repository = new CountingRepository();
        var invalid = new DesktopRuntimeHostRfLabSerialEndpointProfile(
            "invalid",
            "rflab-signal-lab",
            9,
            "invalid-target",
            115200);

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
            await DesktopRuntimeHostRfLabDefinitionPreflight.ResolveAllAsync(
                [Profile("first", "first-target"), invalid, Profile("third", "third-target")],
                repository));

        Assert.Equal(1, repository.CallCount);
    }

    [Fact]
    public async Task ProductionStart_RfLabFailure_ShouldCleanRuntimeStateWithoutTargetLeak()
    {
        const string serialTarget = "sensitive-external-target";
        using var files = new BackendFiles();
        DesktopRuntimeHostInstallationProfile installation = files.Installation;
        var configuration = new DesktopRuntimeHostStartupConfiguration(
            installation.PrivateNetworkConfigurationFilePath,
            Esp32Host: null,
            DeploymentOptions: null!)
        {
            InstallationProfile = installation,
            EndpointCompositionProfile = new DesktopRuntimeHostEndpointCompositionProfile(
                [],
                [],
                [],
                [Profile("rflab-01", serialTarget)])
        };
        int providerCalls = 0;
        var backend = new ProductionPrivateNetworkRuntimeHostBackend(
            configuration,
            _ => throw new InvalidOperationException("KEL-103 service was not expected."),
            runtimeContext =>
            {
                providerCalls++;
                return new DesktopRuntimeHostRfLabAttachmentService(
                    new ThrowingAttachmentFactory(serialTarget));
            });

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => backend.StartAsync(CancellationToken.None));

        Assert.Equal(1, providerCalls);
        Assert.DoesNotContain(serialTarget, exception.ToString(), StringComparison.Ordinal);
        Assert.Empty(backend.Capture());
        Assert.Empty(backend.CaptureDiagnostics());
        await backend.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ProductionStart_PublishedRfLab_ShouldReachCountThenCleanAfterDeploymentFailure()
    {
        using var files = new BackendFiles();
        DesktopRuntimeHostInstallationProfile installation = files.Installation;
        var configuration = new DesktopRuntimeHostStartupConfiguration(
            installation.PrivateNetworkConfigurationFilePath,
            Esp32Host: null,
            DeploymentOptions: null!)
        {
            InstallationProfile = installation,
            EndpointCompositionProfile = new DesktopRuntimeHostEndpointCompositionProfile(
                [],
                [],
                [],
                [Profile("rflab-01", "external-target")])
        };
        PublishingAttachmentFactory? factory = null;
        var backend = new ProductionPrivateNetworkRuntimeHostBackend(
            configuration,
            _ => throw new InvalidOperationException("KEL-103 service was not expected."),
            runtimeContext =>
            {
                factory = new PublishingAttachmentFactory(runtimeContext);
                return new DesktopRuntimeHostRfLabAttachmentService(factory);
            });

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => backend.StartAsync(CancellationToken.None));

        Assert.NotNull(factory);
        Assert.Equal(1, factory.DisposeCount);
        Assert.Empty(factory.RuntimeContext.Endpoints);
        Assert.Empty(backend.Capture());
        Assert.Empty(backend.CaptureDiagnostics());
        await backend.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ProductionStart_RfLabFreeFailure_ShouldNotRequestRfLabService()
    {
        using var files = new BackendFiles();
        Directory.CreateDirectory(files.Installation.IdentityFilePath);
        var configuration = new DesktopRuntimeHostStartupConfiguration(
            files.Installation.PrivateNetworkConfigurationFilePath,
            "configured.local",
            DeploymentOptions: null!)
        {
            InstallationProfile = files.Installation,
            EndpointCompositionProfile = new DesktopRuntimeHostEndpointCompositionProfile(
                [new DesktopRuntimeHostNativeNetworkEndpointProfile(
                    "native-01", "configured.local", 5000)],
                [])
        };
        int providerCalls = 0;
        var backend = new ProductionPrivateNetworkRuntimeHostBackend(
            configuration,
            _ => throw new InvalidOperationException("KEL-103 service was not expected."),
            _ =>
            {
                providerCalls++;
                throw new InvalidOperationException("RF-Lab service was not expected.");
            });

        Exception? exception = await Record.ExceptionAsync(
            () => backend.StartAsync(CancellationToken.None));

        Assert.NotNull(exception);
        Assert.Equal(0, providerCalls);
        Assert.Empty(backend.Capture());
        await backend.StopAsync(CancellationToken.None);
    }

    private static DesktopRuntimeHostRfLabSerialEndpointProfile Profile(
        string endpointId,
        string serialTarget) =>
        new(
            endpointId,
            RfLabReadOnlyDefinition.Reference.Id.Value,
            RfLabReadOnlyDefinition.Reference.Version,
            serialTarget,
            115200);

    private sealed class ThrowingAttachmentFactory(string sensitiveTarget)
        : IDesktopRuntimeHostRfLabAttachmentFactory
    {
        public Task<IDesktopRuntimeHostRfLabAttachment> OpenAsync(
            EndpointId endpointId,
            EndpointDescriptorDefinition definition,
            SerialTransportOptions serialOptions,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                $"Opening {sensitiveTarget} failed.");
    }

    private sealed class PublishingAttachmentFactory(RuntimeContext runtimeContext)
        : IDesktopRuntimeHostRfLabAttachmentFactory
    {
        public RuntimeContext RuntimeContext => runtimeContext;
        public int DisposeCount { get; private set; }

        public Task<IDesktopRuntimeHostRfLabAttachment> OpenAsync(
            EndpointId endpointId,
            EndpointDescriptorDefinition definition,
            SerialTransportOptions serialOptions,
            CancellationToken cancellationToken = default)
        {
            RuntimeEndpoint endpoint = runtimeContext.CreateEndpoint(
                definition.Materialize(endpointId));
            runtimeContext.PublishEndpoint(endpoint);
            return Task.FromResult<IDesktopRuntimeHostRfLabAttachment>(
                new PublishingAttachment(
                    runtimeContext,
                    endpoint,
                    () => DisposeCount++));
        }
    }

    private sealed class PublishingAttachment(
        RuntimeContext runtimeContext,
        RuntimeEndpoint runtimeEndpoint,
        Action onDispose) : IDesktopRuntimeHostRfLabAttachment
    {
        private bool disposed;

        public RuntimeEndpoint RuntimeEndpoint => runtimeEndpoint;
        public IEndpointAttachmentPropertyOperations PropertyOperations { get; } =
            new ThrowingPropertyOperations();

        public IEndpointAttachmentCommandOperations CommandOperations { get; } =
            new ThrowingCommandOperations();

        public ValueTask DisposeAsync()
        {
            if (!disposed)
            {
                disposed = true;
                runtimeContext.RemoveEndpoint(runtimeEndpoint);
                onDispose();
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingPropertyOperations
        : IEndpointAttachmentPropertyOperations
    {
        public Task<EndpointAttachmentPropertyOperationResult> ReadAsync(
            InstrumentId instrumentId,
            PropertyId propertyId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<EndpointAttachmentPropertyOperationResult> WriteAsync(
            InstrumentId instrumentId,
            PropertyId propertyId,
            object? requestedValue,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class ThrowingCommandOperations
        : IEndpointAttachmentCommandOperations
    {
        public Task<EndpointAttachmentCommandOperationResult> ExecuteAsync(
            InstrumentId instrumentId,
            DescriptorPath commandPath,
            object? argument,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class BackendFiles : IDisposable
    {
        private readonly string directory = Path.Combine(
            Path.GetTempPath(),
            "hase-rflab-i2",
            Guid.NewGuid().ToString("N"));

        public BackendFiles()
        {
            Directory.CreateDirectory(directory);
            Installation = new DesktopRuntimeHostInstallationProfile(
                Path.Combine(directory, "identity.json"),
                Path.Combine(directory, "private-network.json"),
                Path.Combine(directory, "endpoints.json"));
        }

        public DesktopRuntimeHostInstallationProfile Installation { get; }

        public void Dispose() => Directory.Delete(directory, recursive: true);
    }

    private sealed class RecordingRepository(
        EndpointDescriptorDefinition? definition) : IEndpointDescriptorRepository
    {
        public DescriptorReference? Reference { get; private set; }

        public ValueTask<EndpointDescriptorDefinition?> FindAsync(
            DescriptorReference reference,
            CancellationToken cancellationToken = default)
        {
            Reference = reference;
            return ValueTask.FromResult(definition);
        }
    }

    private sealed class CountingRepository : IEndpointDescriptorRepository
    {
        public int CallCount { get; private set; }

        public ValueTask<EndpointDescriptorDefinition?> FindAsync(
            DescriptorReference reference,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return ValueTask.FromResult<EndpointDescriptorDefinition?>(
                RfLabReadOnlyDefinition.EndpointDefinition);
        }
    }
}
