using Hase.Runtime.Northbound;

namespace Hase.Runtime.Tests.Northbound;

public sealed class RuntimeHostObservationServiceContractTests
{
    [Fact]
    public void ServiceContract_ExposesSubscriptionOpening()
    {
        Type serviceType =
            typeof(IRuntimeHostObservationService);

        Assert.True(
            serviceType.IsInterface);

        var openMethod =
            serviceType.GetMethod(
                nameof(
                    IRuntimeHostObservationService
                        .OpenSubscriptionAsync));

        Assert.NotNull(
            openMethod);

        Assert.Equal(
            typeof(Task<RuntimeHostObservationSubscription>),
            openMethod.ReturnType);

        Assert.Collection(
            openMethod.GetParameters(),
            options =>
            {
                Assert.Equal(
                    "options",
                    options.Name);

                Assert.Equal(
                    typeof(RuntimeHostObservationSubscriptionOptions),
                    options.ParameterType);
            },
            cancellationToken =>
            {
                Assert.Equal(
                    "cancellationToken",
                    cancellationToken.Name);

                Assert.Equal(
                    typeof(CancellationToken),
                    cancellationToken.ParameterType);

                Assert.True(
                    cancellationToken.HasDefaultValue);
            });
    }

    [Fact]
    public void SubscriptionContract_IsAsynchronouslyDisposable()
    {
        Assert.True(
            typeof(IAsyncDisposable)
                .IsAssignableFrom(
                    typeof(RuntimeHostObservationSubscription)));
    }

    [Fact]
    public void SubscriptionContract_ExposesAsynchronousObservationStream()
    {
        var readMethod =
            typeof(RuntimeHostObservationSubscription)
                .GetMethod(
                    nameof(
                        RuntimeHostObservationSubscription
                            .ReadAllAsync));

        Assert.NotNull(
            readMethod);

        Assert.Equal(
            typeof(IAsyncEnumerable<RuntimeHostObservation>),
            readMethod.ReturnType);

        var cancellationToken =
            Assert.Single(
                readMethod.GetParameters());

        Assert.Equal(
            "cancellationToken",
            cancellationToken.Name);

        Assert.Equal(
            typeof(CancellationToken),
            cancellationToken.ParameterType);

        Assert.True(
            cancellationToken.HasDefaultValue);
    }

    [Fact]
    public void Subscription_StoresSnapshotBoundary()
    {
        PublishedRuntimeHostSnapshot snapshot =
            CreateSnapshot();

        RuntimeHostObservationSequence sequence =
            new(
                17);

        var subscription =
            new TestObservationSubscription(
                snapshot,
                sequence);

        Assert.Same(
            snapshot,
            subscription.InitialSnapshot);

        Assert.Same(
            sequence,
            subscription.SnapshotSequence);
    }

    [Fact]
    public void Subscription_NullInitialSnapshot_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new TestObservationSubscription(
                null!,
                new RuntimeHostObservationSequence(
                    0)));
    }

    [Fact]
    public void Subscription_NullSnapshotSequence_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new TestObservationSubscription(
                CreateSnapshot(),
                null!));
    }

    [Fact]
    public async Task Subscription_DisposeAsync_IsPartOfConcreteContract()
    {
        var subscription =
            new TestObservationSubscription(
                CreateSnapshot(),
                new RuntimeHostObservationSequence(
                    0));

        await subscription.DisposeAsync();
        await subscription.DisposeAsync();

        Assert.Equal(
            2,
            subscription.DisposeCallCount);
    }

    [Fact]
    public async Task Subscription_ReadAllAsync_PropagatesEnumerationCancellation()
    {
        var subscription =
            new TestObservationSubscription(
                CreateSnapshot(),
                new RuntimeHostObservationSequence(
                    0));

        using var cancellationSource =
            new CancellationTokenSource();

        cancellationSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () =>
            {
                await foreach (
                    RuntimeHostObservation observation
                    in subscription.ReadAllAsync(
                        cancellationSource.Token))
                {
                    _ =
                        observation;
                }
            });
    }

    private static PublishedRuntimeHostSnapshot CreateSnapshot()
    {
        return new PublishedRuntimeHostSnapshot(
            new RuntimeHostId(
                "runtime-host-one"),
            RuntimeHostApiVersion.Current,
            Array.Empty<PublishedRuntimeEndpointSnapshot>());
    }

    private sealed class TestObservationSubscription
        : RuntimeHostObservationSubscription
    {
        public TestObservationSubscription(
            PublishedRuntimeHostSnapshot initialSnapshot,
            RuntimeHostObservationSequence snapshotSequence)
            : base(
                initialSnapshot,
                snapshotSequence)
        {
        }

        public int DisposeCallCount
        {
            get;
            private set;
        }

        public override async IAsyncEnumerable<RuntimeHostObservation>
            ReadAllAsync(
                [System.Runtime.CompilerServices.EnumeratorCancellation]
                CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await Task.CompletedTask;

            yield break;
        }

        public override ValueTask DisposeAsync()
        {
            DisposeCallCount++;

            return ValueTask.CompletedTask;
        }
    }
}