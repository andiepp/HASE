using System.IO;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.DesktopHost.App.Hosting;
using Hase.DesktopHost.Configuration;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Hase.Scpi.Kel103;
using Hase.Scpi.Kel103.DesktopHost;
using Hase.Transport.Serial;
using Hase.DesktopHost.Hosting;
using Hase.Mcnf.RfLab.DesktopHost;

namespace Hase.DesktopHost.Tests;

public sealed class DesktopRuntimeHostKel103DefinitionPreflightTests
{
    [Fact]
    public async Task ResolveAsync_ExactVersionTwo_ShouldReturnRepositoryDefinition()
    {
        EndpointDescriptorDefinition definition =
            Kel103ReadOnlyMeasurementDefinition.EndpointDefinition;
        var repository = new RecordingRepository(definition);

        DesktopRuntimeHostKel103EndpointPlan plan =
            await DesktopRuntimeHostKel103DefinitionPreflight.ResolveAsync(
                Profile("kel-01", "external-target"),
                repository);

        Assert.Equal(new EndpointId("kel-01"), plan.ExpectedEndpointId);
        Assert.Same(definition, plan.Definition);
        Assert.Equal(Kel103ReadOnlyMeasurementDefinition.Reference, repository.Reference);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task ResolveAsync_ExactLaterVersionReturnsRepositoryDefinition(ushort version)
    {
        DescriptorReference reference = version switch
        {
            3 => Kel103OperatingStateDefinition.Reference,
            4 => Kel103ControlledSetpointDefinition.Reference,
            _ => Kel103ControlledInputDefinition.Reference
        };
        EndpointDescriptorDefinition definition = version switch
        {
            3 => Kel103OperatingStateDefinition.EndpointDefinition,
            4 => Kel103ControlledSetpointDefinition.EndpointDefinition,
            _ => Kel103ControlledInputDefinition.EndpointDefinition
        };
        var repository = new RecordingRepository(definition);
        var profile = new DesktopRuntimeHostKel103SerialEndpointProfile(
            "kel-01",
            reference.Id.Value,
            reference.Version,
            "external-target",
            115200);

        DesktopRuntimeHostKel103EndpointPlan plan =
            await DesktopRuntimeHostKel103DefinitionPreflight.ResolveAsync(
                profile,
                repository);

        Assert.Same(definition, plan.Definition);
        Assert.Equal(reference, repository.Reference);
    }

    [Fact]
    public async Task ResolveAsync_DefinitionReferenceMismatchRejects()
    {
        var repository = new RecordingRepository(
            Kel103ControlledSetpointDefinition.EndpointDefinition);
        var profile = new DesktopRuntimeHostKel103SerialEndpointProfile(
            "kel-01",
            Kel103ControlledInputDefinition.Reference.Id.Value,
            Kel103ControlledInputDefinition.Reference.Version,
            "external-target",
            115200);

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
            await DesktopRuntimeHostKel103DefinitionPreflight.ResolveAsync(
                profile,
                repository));
    }

    [Fact]
    public async Task ResolveAsync_IdentityOnlyVersionOne_ShouldRejectBeforeRepositoryAccess()
    {
        var repository = new RecordingRepository(
            Kel103IdentityDefinition.EndpointDefinition);
        var profile = new DesktopRuntimeHostKel103SerialEndpointProfile(
            "kel-01",
            Kel103IdentityDefinition.Reference.Id.Value,
            Kel103IdentityDefinition.Reference.Version,
            "external-target",
            115200);

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
            await DesktopRuntimeHostKel103DefinitionPreflight.ResolveAsync(
                profile,
                repository));

        Assert.Null(repository.Reference);
    }

    [Theory]
    [InlineData("unknown-definition", 2)]
    [InlineData("kel103-identity", 6)]
    public async Task ResolveAsync_UnsupportedReference_ShouldReject(
        string definitionId,
        ushort definitionVersion)
    {
        var repository = new RecordingRepository(null);
        var profile = new DesktopRuntimeHostKel103SerialEndpointProfile(
            "kel-01", definitionId, definitionVersion, "external-target", 115200);

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
            await DesktopRuntimeHostKel103DefinitionPreflight.ResolveAsync(
                profile,
                repository));

        Assert.Null(repository.Reference);
    }

    [Fact]
    public async Task ResolveAsync_MissingExactDefinition_ShouldReject()
    {
        var repository = new RecordingRepository(null);

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
            await DesktopRuntimeHostKel103DefinitionPreflight.ResolveAsync(
                Profile("kel-01", "external-target"),
                repository));

        Assert.Equal(Kel103ReadOnlyMeasurementDefinition.Reference, repository.Reference);
    }

    [Fact]
    public async Task ResolveAsync_PreCancelled_ShouldNotAccessRepository()
    {
        var repository = new RecordingRepository(
            Kel103ReadOnlyMeasurementDefinition.EndpointDefinition);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await DesktopRuntimeHostKel103DefinitionPreflight.ResolveAsync(
                Profile("kel-01", "external-target"),
                repository,
                cancellation.Token));

        Assert.Null(repository.Reference);
    }

    [Fact]
    public async Task ResolveAsync_RepositoryCancellation_ShouldPropagate()
    {
        var repository = new CancellingRepository();
        using var cancellation = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await DesktopRuntimeHostKel103DefinitionPreflight.ResolveAsync(
                Profile("kel-01", "external-target"),
                repository,
                cancellation.Token));
    }

    [Fact]
    public async Task ResolveAsync_FailureAndPlanText_ShouldNotExposeSerialTarget()
    {
        const string serialTarget = "sensitive-external-target";
        InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(async () =>
            await DesktopRuntimeHostKel103DefinitionPreflight.ResolveAsync(
                Profile("kel-01", serialTarget),
                new RecordingRepository(null)));

        Assert.DoesNotContain(serialTarget, exception.ToString(), StringComparison.Ordinal);

        DesktopRuntimeHostKel103EndpointPlan plan =
            await DesktopRuntimeHostKel103DefinitionPreflight.ResolveAsync(
                Profile("kel-01", serialTarget),
                new RecordingRepository(Kel103ReadOnlyMeasurementDefinition.EndpointDefinition));

        Assert.DoesNotContain(serialTarget, plan.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResolveAllAsync_ValidProfiles_ShouldPreserveProfileOrder()
    {
        var repository = new RecordingRepository(
            Kel103ReadOnlyMeasurementDefinition.EndpointDefinition);

        IReadOnlyList<DesktopRuntimeHostKel103EndpointPlan> plans =
            await DesktopRuntimeHostKel103DefinitionPreflight.ResolveAllAsync(
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
            Kel103ReadOnlyMeasurementDefinition.EndpointDefinition);

        IReadOnlyList<DesktopRuntimeHostKel103EndpointPlan> plans =
            await DesktopRuntimeHostKel103DefinitionPreflight.ResolveAllAsync(
                [],
                repository);

        Assert.Empty(plans);
        Assert.Null(repository.Reference);
    }

    [Fact]
    public async Task ResolveAllAsync_InvalidProfile_ShouldStopBeforeLaterProfiles()
    {
        var repository = new CountingRepository();
        var invalid = new DesktopRuntimeHostKel103SerialEndpointProfile(
            "invalid",
            Kel103IdentityDefinition.Reference.Id.Value,
            Kel103IdentityDefinition.Reference.Version,
            "invalid-target",
            115200);

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
            await DesktopRuntimeHostKel103DefinitionPreflight.ResolveAllAsync(
                [Profile("first", "first-target"), invalid, Profile("third", "third-target")],
                repository));

        Assert.Equal(1, repository.CallCount);
    }

    [Fact]
    public async Task ProductionStart_Kel103Failure_ShouldCleanRuntimeStateWithoutTargetLeak()
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
                [Profile("kel-01", serialTarget)])
        };
        int providerCalls = 0;
        var backend = CreateBackend(
            configuration,
            runtimeContext =>
            {
                providerCalls++;
                return new DesktopRuntimeHostKel103AttachmentService(
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
    public async Task ProductionStart_PublishedKel103_ShouldReachCountThenCleanAfterDeploymentFailure()
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
                [Profile("kel-01", "external-target")])
        };
        PublishingAttachmentFactory? factory = null;
        var backend = CreateBackend(
            configuration,
            runtimeContext =>
            {
                factory = new PublishingAttachmentFactory(runtimeContext);
                return new DesktopRuntimeHostKel103AttachmentService(factory);
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
    public async Task ProductionStart_KelFreeFailure_ShouldNotRequestKel103Service()
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
        var backend = CreateBackend(
            configuration,
            _ =>
            {
                providerCalls++;
                throw new InvalidOperationException("KEL-103 service was not expected.");
            });

        Exception? exception = await Record.ExceptionAsync(
            () => backend.StartAsync(CancellationToken.None));

        Assert.NotNull(exception);
        Assert.Equal(0, providerCalls);
        Assert.Empty(backend.Capture());
        await backend.StopAsync(CancellationToken.None);
    }

    /// <summary>
    /// Composes the backend over the shipped generic providers plus a
    /// KEL-103 provider whose attachment service this test supplies.
    /// </summary>
    private static ProductionPrivateNetworkRuntimeHostBackend CreateBackend(
        DesktopRuntimeHostStartupConfiguration configuration,
        Func<RuntimeContext, IEndpointAttachmentService> kel103ServiceFactory) =>
        new(
            configuration,
            new DesktopRuntimeHostEndpointProviderRegistry(
                [
                    new DesktopRuntimeHostNativeNetworkEndpointProvider(),
                    new DesktopRuntimeHostCompactSerialEndpointProvider(),
                    new DesktopRuntimeHostKel103EndpointProvider(
                        kel103ServiceFactory),
                    new DesktopRuntimeHostRfLabEndpointProvider()
                ]));

    private static DesktopRuntimeHostKel103SerialEndpointProfile Profile(
        string endpointId,
        string serialTarget) =>
        new(
            endpointId,
            Kel103ReadOnlyMeasurementDefinition.Reference.Id.Value,
            Kel103ReadOnlyMeasurementDefinition.Reference.Version,
            serialTarget,
            115200);

    private static string AbsolutePath(string fileName) =>
        Path.Combine(Path.GetTempPath(), "hase-45h2", fileName);

    private sealed class ThrowingAttachmentFactory(string sensitiveTarget)
        : IDesktopRuntimeHostKel103AttachmentFactory
    {
        public Task<IDesktopRuntimeHostKel103Attachment> OpenAsync(
            EndpointId endpointId,
            EndpointDescriptorDefinition definition,
            SerialTransportOptions serialOptions,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                $"Opening {sensitiveTarget} failed.");
    }

    private sealed class PublishingAttachmentFactory(RuntimeContext runtimeContext)
        : IDesktopRuntimeHostKel103AttachmentFactory
    {
        public RuntimeContext RuntimeContext => runtimeContext;
        public int DisposeCount { get; private set; }

        public Task<IDesktopRuntimeHostKel103Attachment> OpenAsync(
            EndpointId endpointId,
            EndpointDescriptorDefinition definition,
            SerialTransportOptions serialOptions,
            CancellationToken cancellationToken = default)
        {
            RuntimeEndpoint endpoint = runtimeContext.CreateEndpoint(
                definition.Materialize(endpointId));
            runtimeContext.PublishEndpoint(endpoint);
            return Task.FromResult<IDesktopRuntimeHostKel103Attachment>(
                new PublishingAttachment(
                    runtimeContext,
                    endpoint,
                    () => DisposeCount++));
        }
    }

    private sealed class PublishingAttachment(
        RuntimeContext runtimeContext,
        RuntimeEndpoint runtimeEndpoint,
        Action onDispose) : IDesktopRuntimeHostKel103Attachment
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
            "hase-45i2a2",
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

    private sealed class CancellingRepository : IEndpointDescriptorRepository
    {
        public ValueTask<EndpointDescriptorDefinition?> FindAsync(
            DescriptorReference reference,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromCanceled<EndpointDescriptorDefinition?>(
                new CancellationToken(canceled: true));
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
                Kel103ReadOnlyMeasurementDefinition.EndpointDefinition);
        }
    }
}
