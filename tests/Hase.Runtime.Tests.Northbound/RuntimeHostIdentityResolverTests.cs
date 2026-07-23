using Hase.Runtime.Northbound;

namespace Hase.Runtime.Tests.Northbound;

public sealed class RuntimeHostIdentityResolverTests
{
    [Fact]
    public void Constructor_NullStore_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RuntimeHostIdentityResolver(
                null!,
                new TestRuntimeHostIdGenerator()));
    }

    [Fact]
    public void Constructor_NullGenerator_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RuntimeHostIdentityResolver(
                new TestRuntimeHostIdentityStore(),
                null!));
    }

    [Fact]
    public async Task ResolveAsync_ConfiguredIdentity_HasPrecedence()
    {
        var configuredRuntimeHostId =
            new RuntimeHostId(
                "configured-runtime");

        var identityStore =
            new TestRuntimeHostIdentityStore();

        var identityGenerator =
            new TestRuntimeHostIdGenerator();

        var resolver =
            new RuntimeHostIdentityResolver(
                identityStore,
                identityGenerator);

        RuntimeHostIdentityResolution resolution =
            await resolver.ResolveAsync(
                configuredRuntimeHostId);

        Assert.Same(
            configuredRuntimeHostId,
            resolution.RuntimeHostId);

        Assert.Equal(
            RuntimeHostIdentityOrigin.ExplicitConfiguration,
            resolution.Origin);

        Assert.Equal(
            0,
            identityStore.ReadCallCount);

        Assert.Equal(
            0,
            identityStore.CreateCallCount);

        Assert.Equal(
            0,
            identityGenerator.CallCount);
    }

    [Fact]
    public async Task ResolveAsync_PersistedIdentity_SkipsGeneration()
    {
        var persistedRuntimeHostId =
            new RuntimeHostId(
                "persisted-runtime");

        var identityStore =
            new TestRuntimeHostIdentityStore
            {
                ReadResult =
                    persistedRuntimeHostId,
            };

        var identityGenerator =
            new TestRuntimeHostIdGenerator();

        var resolver =
            new RuntimeHostIdentityResolver(
                identityStore,
                identityGenerator);

        RuntimeHostIdentityResolution resolution =
            await resolver.ResolveAsync();

        Assert.Same(
            persistedRuntimeHostId,
            resolution.RuntimeHostId);

        Assert.Equal(
            RuntimeHostIdentityOrigin.Persisted,
            resolution.Origin);

        Assert.Equal(
            1,
            identityStore.ReadCallCount);

        Assert.Equal(
            0,
            identityStore.CreateCallCount);

        Assert.Equal(
            0,
            identityGenerator.CallCount);
    }

    [Fact]
    public async Task ResolveAsync_EmptyStore_CreatesGeneratedIdentity()
    {
        var generatedRuntimeHostId =
            new RuntimeHostId(
                "generated-runtime");

        var identityStore =
            new TestRuntimeHostIdentityStore
            {
                CreateResult =
                    new RuntimeHostIdentityStoreCreateResult(
                        generatedRuntimeHostId,
                        RuntimeHostIdentityStoreCreateOutcome.Created),
            };

        var identityGenerator =
            new TestRuntimeHostIdGenerator
            {
                Result =
                    generatedRuntimeHostId,
            };

        var resolver =
            new RuntimeHostIdentityResolver(
                identityStore,
                identityGenerator);

        RuntimeHostIdentityResolution resolution =
            await resolver.ResolveAsync();

        Assert.Same(
            generatedRuntimeHostId,
            resolution.RuntimeHostId);

        Assert.Equal(
            RuntimeHostIdentityOrigin.GeneratedAndPersisted,
            resolution.Origin);

        Assert.Same(
            generatedRuntimeHostId,
            identityStore.LastCandidate);
    }

    [Fact]
    public async Task ResolveAsync_ConcurrentCreatorWon_ReturnsStoreIdentity()
    {
        var generatedRuntimeHostId =
            new RuntimeHostId(
                "losing-generated-runtime");

        var persistedRuntimeHostId =
            new RuntimeHostId(
                "winning-persisted-runtime");

        var identityStore =
            new TestRuntimeHostIdentityStore
            {
                CreateResult =
                    new RuntimeHostIdentityStoreCreateResult(
                        persistedRuntimeHostId,
                        RuntimeHostIdentityStoreCreateOutcome.Existing),
            };

        var identityGenerator =
            new TestRuntimeHostIdGenerator
            {
                Result =
                    generatedRuntimeHostId,
            };

        var resolver =
            new RuntimeHostIdentityResolver(
                identityStore,
                identityGenerator);

        RuntimeHostIdentityResolution resolution =
            await resolver.ResolveAsync();

        Assert.Same(
            persistedRuntimeHostId,
            resolution.RuntimeHostId);

        Assert.Equal(
            RuntimeHostIdentityOrigin.Persisted,
            resolution.Origin);

        Assert.Same(
            generatedRuntimeHostId,
            identityStore.LastCandidate);
    }

    [Fact]
    public async Task ResolveAsync_ReadFailure_Propagates()
    {
        var expectedException =
            new IOException(
                "Identity store read failed.");

        var identityStore =
            new TestRuntimeHostIdentityStore
            {
                ReadException =
                    expectedException,
            };

        var identityGenerator =
            new TestRuntimeHostIdGenerator();

        var resolver =
            new RuntimeHostIdentityResolver(
                identityStore,
                identityGenerator);

        IOException actualException =
            await Assert.ThrowsAsync<IOException>(
                () => resolver.ResolveAsync());

        Assert.Same(
            expectedException,
            actualException);

        Assert.Equal(
            0,
            identityStore.CreateCallCount);

        Assert.Equal(
            0,
            identityGenerator.CallCount);
    }

    [Fact]
    public async Task ResolveAsync_GenerationFailure_Propagates()
    {
        var expectedException =
            new InvalidOperationException(
                "Identity generation failed.");

        var identityStore =
            new TestRuntimeHostIdentityStore();

        var identityGenerator =
            new TestRuntimeHostIdGenerator
            {
                Exception =
                    expectedException,
            };

        var resolver =
            new RuntimeHostIdentityResolver(
                identityStore,
                identityGenerator);

        InvalidOperationException actualException =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => resolver.ResolveAsync());

        Assert.Same(
            expectedException,
            actualException);

        Assert.Equal(
            0,
            identityStore.CreateCallCount);
    }

    [Fact]
    public async Task ResolveAsync_CreateFailure_Propagates()
    {
        var expectedException =
            new IOException(
                "Identity store creation failed.");

        var identityStore =
            new TestRuntimeHostIdentityStore
            {
                CreateException =
                    expectedException,
            };

        var resolver =
            new RuntimeHostIdentityResolver(
                identityStore,
                new TestRuntimeHostIdGenerator());

        IOException actualException =
            await Assert.ThrowsAsync<IOException>(
                () => resolver.ResolveAsync());

        Assert.Same(
            expectedException,
            actualException);
    }

    [Fact]
    public async Task ResolveAsync_PropagatesCancellationToken()
    {
        using var cancellationSource =
            new CancellationTokenSource();

        CancellationToken cancellationToken =
            cancellationSource.Token;

        var identityStore =
            new TestRuntimeHostIdentityStore();

        var resolver =
            new RuntimeHostIdentityResolver(
                identityStore,
                new TestRuntimeHostIdGenerator());

        await resolver.ResolveAsync(
            cancellationToken:
                cancellationToken);

        Assert.Equal(
            cancellationToken,
            identityStore.LastReadCancellationToken);

        Assert.Equal(
            cancellationToken,
            identityStore.LastCreateCancellationToken);
    }

    private sealed class TestRuntimeHostIdGenerator
        : IRuntimeHostIdGenerator
    {
        public RuntimeHostId Result
        {
            get;
            init;
        } =
            new(
                "generated-runtime");

        public Exception? Exception
        {
            get;
            init;
        }

        public int CallCount
        {
            get;
            private set;
        }

        public RuntimeHostId Generate()
        {
            CallCount++;

            if (Exception is not null)
            {
                throw Exception;
            }

            return Result;
        }
    }

    private sealed class TestRuntimeHostIdentityStore
        : IRuntimeHostIdentityStore
    {
        public RuntimeHostId? ReadResult
        {
            get;
            init;
        }

        public Exception? ReadException
        {
            get;
            init;
        }

        public RuntimeHostIdentityStoreCreateResult CreateResult
        {
            get;
            init;
        } =
            new(
                new RuntimeHostId(
                    "generated-runtime"),
                RuntimeHostIdentityStoreCreateOutcome.Created);

        public Exception? CreateException
        {
            get;
            init;
        }

        public int ReadCallCount
        {
            get;
            private set;
        }

        public int CreateCallCount
        {
            get;
            private set;
        }

        public RuntimeHostId? LastCandidate
        {
            get;
            private set;
        }

        public CancellationToken LastReadCancellationToken
        {
            get;
            private set;
        }

        public CancellationToken LastCreateCancellationToken
        {
            get;
            private set;
        }

        public Task<RuntimeHostId?> ReadAsync(
            CancellationToken cancellationToken = default)
        {
            ReadCallCount++;
            LastReadCancellationToken =
                cancellationToken;

            if (ReadException is not null)
            {
                throw ReadException;
            }

            return Task.FromResult(
                ReadResult);
        }

        public Task<RuntimeHostIdentityStoreCreateResult> CreateIfMissingAsync(
            RuntimeHostId candidate,
            CancellationToken cancellationToken = default)
        {
            CreateCallCount++;
            LastCandidate =
                candidate;
            LastCreateCancellationToken =
                cancellationToken;

            if (CreateException is not null)
            {
                throw CreateException;
            }

            return Task.FromResult(
                CreateResult);
        }
    }
}