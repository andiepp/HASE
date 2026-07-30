using Hase.Runtime.Diagnostics;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class RuntimeEndpointReconnectDiagnosticPolicyTests
{
    [Fact]
    public void Constructor_NullInnerPolicy_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RuntimeEndpointReconnectDiagnosticPolicy(
                null!,
                new RuntimeDiagnosticPublisher(),
                "endpoint-01"));
    }

    [Fact]
    public void Constructor_NullDiagnostics_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RuntimeEndpointReconnectDiagnosticPolicy(
                new DefaultRuntimeEndpointReconnectPolicy(),
                null!,
                "endpoint-01"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Constructor_EmptyEndpointId_ThrowsArgumentException(
        string endpointId)
    {
        Assert.Throws<ArgumentException>(
            () => new RuntimeEndpointReconnectDiagnosticPolicy(
                new DefaultRuntimeEndpointReconnectPolicy(),
                new RuntimeDiagnosticPublisher(),
                endpointId));
    }

    [Fact]
    public void GetDelay_DefaultSchedule_PreservesDelaysAndPublishesAttempts()
    {
        BoundedRuntimeDiagnosticCollector collector =
            new(
                10);

        RuntimeEndpointReconnectDiagnosticPolicy policy =
            new(
                new DefaultRuntimeEndpointReconnectPolicy(),
                new RuntimeDiagnosticPublisher(
                    collector),
                " endpoint-01 ");

        TimeSpan[] delays =
        [
            policy.GetDelay(0),
            policy.GetDelay(1),
            policy.GetDelay(2),
            policy.GetDelay(3),
            policy.GetDelay(4),
            policy.GetDelay(5)
        ];

        Assert.Equal(
            [
                TimeSpan.Zero,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(10)
            ],
            delays);

        IReadOnlyList<RuntimeDiagnosticRecord> records =
            collector.GetSnapshot();

        Assert.Equal(
            6,
            records.Count);

        Assert.Equal(
            "1",
            records[0].Details["AttemptNumber"]);

        Assert.Equal(
            "0",
            records[0].Details["RetryIndex"]);

        Assert.Equal(
            "0",
            records[0].Details["DelayMilliseconds"]);

        Assert.Equal(
            "6",
            records[5].Details["AttemptNumber"]);

        Assert.Equal(
            "10000",
            records[5].Details["DelayMilliseconds"]);

        Assert.All(
            records,
            record =>
            {
                Assert.Equal(
                    RuntimeDiagnosticCategory.RuntimeRecovery,
                    record.Category);

                Assert.Equal(
                    "RecoveryScheduled",
                    record.EventName);

                Assert.Equal(
                    "endpoint-01",
                    record.EndpointId);
            });
    }

    [Fact]
    public void GetDelay_AttachmentGeneration_PreservesGeneration()
    {
        Guid generation =
            Guid.NewGuid();

        BoundedRuntimeDiagnosticCollector collector =
            new(
                10);

        RuntimeEndpointReconnectDiagnosticPolicy policy =
            new(
                new DefaultRuntimeEndpointReconnectPolicy(),
                new RuntimeDiagnosticPublisher(
                    collector),
                "endpoint-01",
                generation);

        _ = policy.GetDelay(
            0);

        RuntimeDiagnosticRecord record =
            Assert.Single(
                collector.GetSnapshot());

        Assert.Equal(
            generation,
            record.AttachmentGeneration);
    }

    [Fact]
    public void GetDelay_DisabledDiagnostics_PreservesWrappedDelay()
    {
        RuntimeEndpointReconnectDiagnosticPolicy policy =
            new(
                new FixedPolicy(
                    TimeSpan.FromMilliseconds(
                        125)),
                new RuntimeDiagnosticPublisher(),
                "endpoint-01");

        Assert.Equal(
            TimeSpan.FromMilliseconds(
                125),
            policy.GetDelay(
                3));
    }

    [Fact]
    public void GetDelay_ThrowingSink_PreservesWrappedDelay()
    {
        RuntimeEndpointReconnectDiagnosticPolicy policy =
            new(
                new FixedPolicy(
                    TimeSpan.FromSeconds(
                        2)),
                new RuntimeDiagnosticPublisher(
                    new ThrowingSink()),
                "endpoint-01");

        Exception? exception =
            Record.Exception(
                () => policy.GetDelay(
                    1));

        Assert.Null(
            exception);
    }

    [Fact]
    public void GetDelay_ThrowingInnerPolicy_PropagatesOriginalException()
    {
        InvalidOperationException expected =
            new(
                "Policy failure.");

        RuntimeEndpointReconnectDiagnosticPolicy policy =
            new(
                new ThrowingPolicy(
                    expected),
                new RuntimeDiagnosticPublisher(),
                "endpoint-01");

        InvalidOperationException actual =
            Assert.Throws<InvalidOperationException>(
                () => policy.GetDelay(
                    0));

        Assert.Same(
            expected,
            actual);
    }

    private sealed class FixedPolicy :
        IRuntimeEndpointReconnectPolicy
    {
        private readonly TimeSpan delay;

        public FixedPolicy(
            TimeSpan delay)
        {
            this.delay =
                delay;
        }

        public TimeSpan GetDelay(
            int retryAttempt)
        {
            return delay;
        }
    }

    private sealed class ThrowingPolicy :
        IRuntimeEndpointReconnectPolicy
    {
        private readonly Exception exception;

        public ThrowingPolicy(
            Exception exception)
        {
            this.exception =
                exception;
        }

        public TimeSpan GetDelay(
            int retryAttempt)
        {
            throw exception;
        }
    }

    private sealed class ThrowingSink :
        IRuntimeDiagnosticSink
    {
        public bool IsEnabled(
            RuntimeDiagnosticLevel level)
        {
            return true;
        }

        public void Publish(
            RuntimeDiagnosticRecord record)
        {
            throw new InvalidOperationException(
                "Test observer failure.");
        }
    }
}
