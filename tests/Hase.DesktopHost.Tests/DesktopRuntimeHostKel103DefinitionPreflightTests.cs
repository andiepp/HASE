using System.IO;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Identity;
using Hase.DesktopHost.App.Hosting;
using Hase.DesktopHost.Configuration;
using Hase.Scpi.Kel103;

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
    [InlineData("kel103-identity", 3)]
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
    public async Task ProductionStart_ValidKel103_ShouldGateBeforeRuntimeStateWithoutTargetLeak()
    {
        const string serialTarget = "sensitive-external-target";
        var installation = new DesktopRuntimeHostInstallationProfile(
            AbsolutePath("identity.json"),
            AbsolutePath("private-network.json"),
            AbsolutePath("endpoints.json"));
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
        var backend = new ProductionPrivateNetworkRuntimeHostBackend(configuration);

        NotSupportedException exception = await Assert.ThrowsAsync<NotSupportedException>(
            () => backend.StartAsync(CancellationToken.None));

        Assert.DoesNotContain(serialTarget, exception.ToString(), StringComparison.Ordinal);
        Assert.Empty(backend.Capture());
        Assert.Empty(backend.CaptureDiagnostics());
        await backend.StopAsync(CancellationToken.None);
    }

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
