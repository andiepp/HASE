using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostRemoteApiServiceTests
{
    [Fact]
    public void Constructor_NullDependency_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "snapshotProvider",
            () =>
                new RuntimeHostRemoteApiService(
                    null!,
                    RuntimeHostSnapshotMapperFactory.Create()));

        Assert.Throws<ArgumentNullException>(
            "snapshotMapper",
            () =>
                new RuntimeHostRemoteApiService(
                    new TestSnapshotProvider(
                        CreateSnapshot()),
                    null!));

        Assert.Throws<ArgumentNullException>(
            "propertyService",
            () =>
                new RuntimeHostRemoteApiService(
                    new TestSnapshotProvider(
                        CreateSnapshot()),
                    RuntimeHostSnapshotMapperFactory.Create(),
                    null!,
                    new TestPropertyTargetMapper(
                        CreateTarget()),
                    new TestCachedResultMapper(
                        new GrpcV1.CachedPropertyResult()),
                    new TestOperationResultMapper(
                        new GrpcV1.PropertyOperationResult())));

        Assert.Throws<ArgumentNullException>(
            "propertyTargetMapper",
            () =>
                new RuntimeHostRemoteApiService(
                    new TestSnapshotProvider(
                        CreateSnapshot()),
                    RuntimeHostSnapshotMapperFactory.Create(),
                    new TestPropertyService(
                        CreateCachedResult()),
                    null!,
                    new TestCachedResultMapper(
                        new GrpcV1.CachedPropertyResult()),
                    new TestOperationResultMapper(
                        new GrpcV1.PropertyOperationResult())));

        Assert.Throws<ArgumentNullException>(
            "cachedResultMapper",
            () =>
                new RuntimeHostRemoteApiService(
                    new TestSnapshotProvider(
                        CreateSnapshot()),
                    RuntimeHostSnapshotMapperFactory.Create(),
                    new TestPropertyService(
                        CreateCachedResult()),
                    new TestPropertyTargetMapper(
                        CreateTarget()),
                    null!,
                    new TestOperationResultMapper(
                        new GrpcV1.PropertyOperationResult())));

        Assert.Throws<ArgumentNullException>(
            "operationResultMapper",
            () =>
                new RuntimeHostRemoteApiService(
                    new TestSnapshotProvider(
                        CreateSnapshot()),
                    RuntimeHostSnapshotMapperFactory.Create(),
                    new TestPropertyService(
                        CreateCachedResult()),
                    new TestPropertyTargetMapper(
                        CreateTarget()),
                    new TestCachedResultMapper(
                        new GrpcV1.CachedPropertyResult()),
                    null!));
    }

    [Fact]
    public async Task GetSnapshot_NullRequest_ShouldThrow()
    {
        var service =
            new RuntimeHostRemoteApiService(
                new TestSnapshotProvider(
                    CreateSnapshot()),
                RuntimeHostSnapshotMapperFactory.Create());

        await Assert.ThrowsAsync<ArgumentNullException>(
            "request",
            () =>
                service.GetSnapshot(
                    null!,
                    null!));
    }

    [Fact]
    public async Task GetSnapshot_ShouldCaptureAndMapAuthoritativeSnapshot()
    {
        var provider =
            new TestSnapshotProvider(
                CreateSnapshot());

        var service =
            new RuntimeHostRemoteApiService(
                provider,
                RuntimeHostSnapshotMapperFactory.Create());

        GrpcV1.GetSnapshotResponse response =
            await service.GetSnapshot(
                new GrpcV1.GetSnapshotRequest(),
                null!);

        Assert.Equal(
            1,
            provider.CaptureCount);
        Assert.Equal(
            "runtime-host-1",
            response.RuntimeHostId);
        Assert.Equal(
            1U,
            response.ApiVersion.Major);
        Assert.Equal(
            0U,
            response.ApiVersion.Minor);
        Assert.Empty(
            response.Endpoints);
    }

    [Fact]
    public async Task GetSnapshot_ProviderReturnsNull_ShouldThrow()
    {
        var service =
            new RuntimeHostRemoteApiService(
                new TestSnapshotProvider(
                    null!),
                RuntimeHostSnapshotMapperFactory.Create());

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () =>
                    service.GetSnapshot(
                        new GrpcV1.GetSnapshotRequest(),
                        null!));

        Assert.Equal(
            "The runtime-host snapshot provider returned null.",
            exception.Message);
    }

    [Fact]
    public async Task ReadCachedProperty_NullRequest_ShouldThrow()
    {
        RuntimeHostRemoteApiService service =
            CreatePropertyServiceAdapter(
                new TestPropertyService(
                    CreateCachedResult()),
                new TestPropertyTargetMapper(
                    CreateTarget()),
                new TestCachedResultMapper(
                    new GrpcV1.CachedPropertyResult()));

        await Assert.ThrowsAsync<ArgumentNullException>(
            "request",
            () =>
                service.ReadCachedProperty(
                    null!,
                    null!));
    }

    [Fact]
    public async Task ReadCachedProperty_NotConfigured_ShouldThrow()
    {
        var service =
            new RuntimeHostRemoteApiService(
                new TestSnapshotProvider(
                    CreateSnapshot()),
                RuntimeHostSnapshotMapperFactory.Create());

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () =>
                    service.ReadCachedProperty(
                        new GrpcV1.ReadCachedPropertyRequest(),
                        null!));

        Assert.Equal(
            "Cached Property access is not configured.",
            exception.Message);
    }

    [Fact]
    public async Task ReadCachedProperty_ShouldMapQueryAndResult()
    {
        Northbound.RuntimeHostPropertyTarget target =
            CreateTarget();
        Northbound.RuntimeHostCachedPropertyResult cachedResult =
            CreateCachedResult();
        var mappedResult =
            new GrpcV1.CachedPropertyResult
            {
                Status =
                    GrpcV1.PropertyOperationStatus.PropertyNotFound,
                Diagnostic =
                    "Mapped result."
            };
        var propertyService =
            new TestPropertyService(
                cachedResult);
        var targetMapper =
            new TestPropertyTargetMapper(
                target);
        var resultMapper =
            new TestCachedResultMapper(
                mappedResult);
        RuntimeHostRemoteApiService service =
            CreatePropertyServiceAdapter(
                propertyService,
                targetMapper,
                resultMapper);
        var request =
            new GrpcV1.ReadCachedPropertyRequest
            {
                Target =
                    new GrpcV1.PropertyTarget
                    {
                        EndpointId =
                            "remote-endpoint"
                    }
            };

        GrpcV1.CachedPropertyResult response =
            await service.ReadCachedProperty(
                request,
                null!);

        Assert.Same(
            request.Target,
            targetMapper.Input);
        Assert.Same(
            target,
            propertyService.CachedTarget);
        Assert.Same(
            cachedResult,
            resultMapper.Input);
        Assert.Same(
            mappedResult,
            response);
    }

    [Fact]
    public async Task ReadCachedProperty_TargetMapperReturnsNull_ShouldThrow()
    {
        RuntimeHostRemoteApiService service =
            CreatePropertyServiceAdapter(
                new TestPropertyService(
                    CreateCachedResult()),
                new TestPropertyTargetMapper(
                    null!),
                new TestCachedResultMapper(
                    new GrpcV1.CachedPropertyResult()));

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () =>
                    service.ReadCachedProperty(
                        new GrpcV1.ReadCachedPropertyRequest
                        {
                            Target =
                                new GrpcV1.PropertyTarget()
                        },
                        null!));

        Assert.Equal(
            "The Property target mapper returned null.",
            exception.Message);
    }

    [Fact]
    public async Task ReadCachedProperty_PropertyServiceReturnsNull_ShouldThrow()
    {
        RuntimeHostRemoteApiService service =
            CreatePropertyServiceAdapter(
                new TestPropertyService(
                    null!),
                new TestPropertyTargetMapper(
                    CreateTarget()),
                new TestCachedResultMapper(
                    new GrpcV1.CachedPropertyResult()));

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () =>
                    service.ReadCachedProperty(
                        new GrpcV1.ReadCachedPropertyRequest
                        {
                            Target =
                                new GrpcV1.PropertyTarget()
                        },
                        null!));

        Assert.Equal(
            "The runtime-host Property service returned null.",
            exception.Message);
    }

    [Fact]
    public async Task ReadCachedProperty_ResultMapperReturnsNull_ShouldThrow()
    {
        RuntimeHostRemoteApiService service =
            CreatePropertyServiceAdapter(
                new TestPropertyService(
                    CreateCachedResult()),
                new TestPropertyTargetMapper(
                    CreateTarget()),
                new TestCachedResultMapper(
                    null!));

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () =>
                    service.ReadCachedProperty(
                        new GrpcV1.ReadCachedPropertyRequest
                        {
                            Target =
                                new GrpcV1.PropertyTarget()
                        },
                        null!));

        Assert.Equal(
            "The cached Property result mapper returned null.",
            exception.Message);
    }

    [Fact]
    public async Task ReadAuthoritativeProperty_NullRequest_ShouldThrow()
    {
        RuntimeHostRemoteApiService service =
            CreateAuthoritativeServiceAdapter(
                new TestPropertyService(
                    CreateCachedResult()),
                new TestPropertyTargetMapper(
                    CreateTarget()),
                new TestOperationResultMapper(
                    new GrpcV1.PropertyOperationResult()));

        await Assert.ThrowsAsync<ArgumentNullException>(
            "request",
            () =>
                service.ReadAuthoritativeProperty(
                    null!,
                    null!));
    }

    [Fact]
    public async Task ReadAuthoritativeProperty_NotConfigured_ShouldThrow()
    {
        var service =
            new RuntimeHostRemoteApiService(
                new TestSnapshotProvider(
                    CreateSnapshot()),
                RuntimeHostSnapshotMapperFactory.Create());

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () =>
                    service.ReadAuthoritativeProperty(
                        new GrpcV1.ReadAuthoritativePropertyRequest(),
                        null!));

        Assert.Equal(
            "Authoritative Property access is not configured.",
            exception.Message);
    }

    [Fact]
    public async Task ReadAuthoritativeProperty_ShouldAwaitAndMapResult()
    {
        Northbound.RuntimeHostPropertyTarget target =
            CreateTarget();
        Northbound.RuntimeHostPropertyOperationResult operationResult =
            CreateOperationResult();
        var mappedResult =
            new GrpcV1.PropertyOperationResult
            {
                Status =
                    GrpcV1.PropertyOperationStatus.TimedOut
            };
        var propertyService =
            new TestPropertyService(
                CreateCachedResult(),
                operationResult);
        var targetMapper =
            new TestPropertyTargetMapper(
                target);
        var resultMapper =
            new TestOperationResultMapper(
                mappedResult);
        RuntimeHostRemoteApiService service =
            CreateAuthoritativeServiceAdapter(
                propertyService,
                targetMapper,
                resultMapper);
        var request =
            new GrpcV1.ReadAuthoritativePropertyRequest
            {
                Target =
                    new GrpcV1.PropertyTarget()
            };

        GrpcV1.PropertyOperationResult response =
            await service.ReadAuthoritativeProperty(
                request,
                null!);

        Assert.Same(
            request.Target,
            targetMapper.Input);
        Assert.Same(
            target,
            propertyService.ReadTarget);
        Assert.Equal(
            CancellationToken.None,
            propertyService.ReadCancellationToken);
        Assert.Same(
            operationResult,
            resultMapper.Input);
        Assert.Same(
            mappedResult,
            response);
    }

    [Fact]
    public async Task ReadAuthoritativeProperty_PropertyServiceReturnsNull_ShouldThrow()
    {
        RuntimeHostRemoteApiService service =
            CreateAuthoritativeServiceAdapter(
                new TestPropertyService(
                    CreateCachedResult(),
                    null!),
                new TestPropertyTargetMapper(
                    CreateTarget()),
                new TestOperationResultMapper(
                    new GrpcV1.PropertyOperationResult()));

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () =>
                    service.ReadAuthoritativeProperty(
                        new GrpcV1.ReadAuthoritativePropertyRequest
                        {
                            Target =
                                new GrpcV1.PropertyTarget()
                        },
                        null!));

        Assert.Equal(
            "The runtime-host Property service returned null.",
            exception.Message);
    }

    [Fact]
    public async Task ReadAuthoritativeProperty_ResultMapperReturnsNull_ShouldThrow()
    {
        RuntimeHostRemoteApiService service =
            CreateAuthoritativeServiceAdapter(
                new TestPropertyService(
                    CreateCachedResult(),
                    CreateOperationResult()),
                new TestPropertyTargetMapper(
                    CreateTarget()),
                new TestOperationResultMapper(
                    null!));

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () =>
                    service.ReadAuthoritativeProperty(
                        new GrpcV1.ReadAuthoritativePropertyRequest
                        {
                            Target =
                                new GrpcV1.PropertyTarget()
                        },
                        null!));

        Assert.Equal(
            "The Property operation result mapper returned null.",
            exception.Message);
    }

    private static RuntimeHostRemoteApiService CreatePropertyServiceAdapter(
        Northbound.IRuntimeHostPropertyService propertyService,
        IRuntimeHostPropertyTargetMapper targetMapper,
        IRuntimeHostCachedPropertyResultMapper resultMapper)
    {
        return new RuntimeHostRemoteApiService(
            new TestSnapshotProvider(
                CreateSnapshot()),
            RuntimeHostSnapshotMapperFactory.Create(),
            propertyService,
            targetMapper,
            resultMapper,
            new TestOperationResultMapper(
                new GrpcV1.PropertyOperationResult()));
    }

    private static RuntimeHostRemoteApiService CreateAuthoritativeServiceAdapter(
        Northbound.IRuntimeHostPropertyService propertyService,
        IRuntimeHostPropertyTargetMapper targetMapper,
        IRuntimeHostPropertyOperationResultMapper resultMapper)
    {
        return new RuntimeHostRemoteApiService(
            new TestSnapshotProvider(
                CreateSnapshot()),
            RuntimeHostSnapshotMapperFactory.Create(),
            propertyService,
            targetMapper,
            new TestCachedResultMapper(
                new GrpcV1.CachedPropertyResult()),
            resultMapper);
    }

    private static Northbound.RuntimeHostPropertyTarget CreateTarget()
    {
        return new Northbound.RuntimeHostPropertyTarget(
            new Hase.Core.Domain.Identity.EndpointId(
                "endpoint-01"),
            new Northbound.RuntimeEndpointAttachmentGeneration(
                new Guid(
                    "868e79d4-b1a4-4a63-81cd-5a800d9ba3fd")),
            new Hase.Core.Domain.Identity.InstrumentId(
                "environment-sensor-01"),
            new Hase.Core.Domain.Identity.PropertyId(
                "temperature"));
    }

    private static Northbound.RuntimeHostCachedPropertyResult CreateCachedResult()
    {
        return Northbound.RuntimeHostCachedPropertyResult.Failed(
            Northbound.RuntimeHostPropertyOperationStatus.PropertyNotFound,
            "Property not found.");
    }

    private static Northbound.RuntimeHostPropertyOperationResult CreateOperationResult()
    {
        return Northbound.RuntimeHostPropertyOperationResult.Failed(
            Northbound.RuntimeHostPropertyOperationStatus.TimedOut,
            "Property read timed out.");
    }

    private static Northbound.PublishedRuntimeHostSnapshot CreateSnapshot()
    {
        return new Northbound.PublishedRuntimeHostSnapshot(
            new Northbound.RuntimeHostId(
                "runtime-host-1"),
            Northbound.RuntimeHostApiVersion.Current,
            Array.Empty<Northbound.PublishedRuntimeEndpointSnapshot>());
    }

    private sealed class TestSnapshotProvider
        : Northbound.IRuntimeHostSnapshotProvider
    {
        private readonly Northbound.PublishedRuntimeHostSnapshot snapshot;

        public TestSnapshotProvider(
            Northbound.PublishedRuntimeHostSnapshot snapshot)
        {
            this.snapshot =
                snapshot;
        }

        public int CaptureCount
        {
            get;
            private set;
        }

        public Northbound.PublishedRuntimeHostSnapshot Capture()
        {
            CaptureCount++;

            return snapshot;
        }
    }

    private sealed class TestPropertyService
        : Northbound.IRuntimeHostPropertyService
    {
        private readonly Northbound.RuntimeHostCachedPropertyResult cachedResult;
        private readonly Northbound.RuntimeHostPropertyOperationResult operationResult;

        public TestPropertyService(
            Northbound.RuntimeHostCachedPropertyResult cachedResult,
            Northbound.RuntimeHostPropertyOperationResult? operationResult = null)
        {
            this.cachedResult =
                cachedResult;
            this.operationResult =
                operationResult!;
        }

        public Northbound.RuntimeHostPropertyTarget? CachedTarget
        {
            get;
            private set;
        }

        public Northbound.RuntimeHostPropertyTarget? ReadTarget
        {
            get;
            private set;
        }

        public CancellationToken ReadCancellationToken
        {
            get;
            private set;
        }

        public Northbound.RuntimeHostCachedPropertyResult GetCached(
            Northbound.RuntimeHostPropertyTarget target)
        {
            CachedTarget =
                target;

            return cachedResult;
        }

        public Task<Northbound.RuntimeHostPropertyOperationResult> ReadAsync(
            Northbound.RuntimeHostPropertyTarget target,
            CancellationToken cancellationToken = default)
        {
            ReadTarget =
                target;
            ReadCancellationToken =
                cancellationToken;

            return Task.FromResult(
                operationResult);
        }

        public Task<Northbound.RuntimeHostPropertyOperationResult> WriteAsync(
            Northbound.RuntimeHostPropertyTarget target,
            object? requestedValue,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class TestPropertyTargetMapper
        : IRuntimeHostPropertyTargetMapper
    {
        private readonly Northbound.RuntimeHostPropertyTarget result;

        public TestPropertyTargetMapper(
            Northbound.RuntimeHostPropertyTarget result)
        {
            this.result =
                result;
        }

        public GrpcV1.PropertyTarget? Input
        {
            get;
            private set;
        }

        public Northbound.RuntimeHostPropertyTarget Map(
            GrpcV1.PropertyTarget source)
        {
            Input =
                source;

            return result;
        }
    }

    private sealed class TestCachedResultMapper
        : IRuntimeHostCachedPropertyResultMapper
    {
        private readonly GrpcV1.CachedPropertyResult result;

        public TestCachedResultMapper(
            GrpcV1.CachedPropertyResult result)
        {
            this.result =
                result;
        }

        public Northbound.RuntimeHostCachedPropertyResult? Input
        {
            get;
            private set;
        }

        public GrpcV1.CachedPropertyResult Map(
            Northbound.RuntimeHostCachedPropertyResult result)
        {
            Input =
                result;

            return this.result;
        }
    }

    private sealed class TestOperationResultMapper
        : IRuntimeHostPropertyOperationResultMapper
    {
        private readonly GrpcV1.PropertyOperationResult result;

        public TestOperationResultMapper(
            GrpcV1.PropertyOperationResult result)
        {
            this.result =
                result;
        }

        public Northbound.RuntimeHostPropertyOperationResult? Input
        {
            get;
            private set;
        }

        public GrpcV1.PropertyOperationResult Map(
            Northbound.RuntimeHostPropertyOperationResult result)
        {
            Input =
                result;

            return this.result;
        }
    }
}
