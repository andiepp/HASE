using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;

namespace Hase.Scpi.Kel103.Hosting.Tests;

public sealed class Kel103EndpointAttachmentPropertyOperationsTests
{
    [Theory]
    [InlineData("product-identity", "KEL-103")]
    [InlineData("firmware-version", "V3.30")]
    [InlineData("measured-voltage", "9.8864")]
    [InlineData("measured-current", "0.1000")]
    [InlineData("measured-power", "0.9893")]
    public async Task Read_ReturnsExactAuthoritativeValue(string propertyId, string valueText)
    {
        RuntimeProperty property = FindProperty(propertyId);
        object value = propertyId.StartsWith("measured-", StringComparison.Ordinal)
            ? decimal.Parse(valueText, System.Globalization.CultureInfo.InvariantCulture)
            : valueText;
        var expected = new PropertyValue(value, DateTimeOffset.UnixEpoch);
        property.UpdateValue(expected);
        var operations = new Kel103EndpointAttachmentPropertyOperations(
            (instrument, requestedProperty, token) => Task.FromResult(property));

        EndpointAttachmentPropertyOperationResult result = await operations.ReadAsync(
            InstrumentId(), new PropertyId(propertyId));

        Assert.True(result.IsSuccess);
        Assert.Same(expected, result.ConfirmedValue);
        Assert.Null(result.Diagnostic);
    }

    [Theory]
    [InlineData(0, EndpointAttachmentPropertyOperationStatus.NotSupported)]
    [InlineData(1, EndpointAttachmentPropertyOperationStatus.TimedOut)]
    [InlineData(2, EndpointAttachmentPropertyOperationStatus.Failure)]
    [InlineData(3, EndpointAttachmentPropertyOperationStatus.Unavailable)]
    [InlineData(4, EndpointAttachmentPropertyOperationStatus.Unavailable)]
    public async Task Read_MapsSafeFailureOutcomes(int failure, EndpointAttachmentPropertyOperationStatus expected)
    {
        const string sensitive = "sensitive failure detail";
        Exception exception = failure switch
        {
            0 => new KeyNotFoundException(sensitive),
            1 => new TimeoutException(sensitive),
            2 => new InvalidDataException(sensitive),
            3 => new InvalidOperationException(sensitive),
            _ => new IOException(sensitive)
        };
        var operations = new Kel103EndpointAttachmentPropertyOperations(
            (instrument, property, token) => Task.FromException<RuntimeProperty>(exception));

        EndpointAttachmentPropertyOperationResult result = await operations.ReadAsync(
            InstrumentId(), new PropertyId("measured-voltage"));

        Assert.Equal(expected, result.Status);
        Assert.Null(result.ConfirmedValue);
        Assert.DoesNotContain(sensitive, result.Diagnostic ?? string.Empty, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("ignored")]
    [InlineData(42)]
    public async Task Write_IsAlwaysUnsupportedWithoutCallingRead(object? requestedValue)
    {
        var called = false;
        var operations = new Kel103EndpointAttachmentPropertyOperations(
            (instrument, property, token) =>
            {
                called = true;
                throw new InvalidOperationException();
            });

        EndpointAttachmentPropertyOperationResult result = await operations.WriteAsync(
            InstrumentId(), new PropertyId("measured-voltage"), requestedValue);

        Assert.Equal(EndpointAttachmentPropertyOperationStatus.NotSupported, result.Status);
        Assert.False(called);
    }

    [Fact]
    public async Task Write_ForwardsRequestAndReturnsExactConfirmedValue()
    {
        RuntimeProperty property = FindProperty("target-current");
        var confirmed = new PropertyValue(0.25m, DateTimeOffset.UnixEpoch);
        property.UpdateValue(confirmed);
        InstrumentId? observedInstrument = null;
        PropertyId? observedProperty = null;
        object? observedValue = null;
        var operations = CreateWritableOperations(
            (instrument, requestedProperty, value, token) =>
            {
                observedInstrument = instrument;
                observedProperty = requestedProperty;
                observedValue = value;
                return Task.FromResult(property);
            });

        EndpointAttachmentPropertyOperationResult result = await operations.WriteAsync(
            InstrumentId(),
            new PropertyId("target-current"),
            0.25m);

        Assert.True(result.IsSuccess);
        Assert.Same(confirmed, result.ConfirmedValue);
        Assert.Equal(InstrumentId(), observedInstrument);
        Assert.Equal(new PropertyId("target-current"), observedProperty);
        Assert.Equal(0.25m, observedValue);
        Assert.Null(result.Diagnostic);
    }

    [Theory]
    [InlineData(0, EndpointAttachmentPropertyOperationStatus.NotSupported)]
    [InlineData(1, EndpointAttachmentPropertyOperationStatus.Failure)]
    [InlineData(2, EndpointAttachmentPropertyOperationStatus.TimedOut)]
    [InlineData(3, EndpointAttachmentPropertyOperationStatus.Failure)]
    [InlineData(4, EndpointAttachmentPropertyOperationStatus.Unavailable)]
    [InlineData(5, EndpointAttachmentPropertyOperationStatus.Unavailable)]
    public async Task Write_MapsSafeFailureOutcomes(
        int failure,
        EndpointAttachmentPropertyOperationStatus expected)
    {
        const string sensitive = "sensitive write detail";
        Exception exception = failure switch
        {
            0 => new KeyNotFoundException(sensitive),
            1 => new ArgumentOutOfRangeException("requestedValue", sensitive),
            2 => new TimeoutException(sensitive),
            3 => new InvalidDataException(sensitive),
            4 => new InvalidOperationException(sensitive),
            _ => new IOException(sensitive)
        };
        var operations = CreateWritableOperations(
            (instrument, property, value, token) =>
                Task.FromException<RuntimeProperty>(exception));

        EndpointAttachmentPropertyOperationResult result = await operations.WriteAsync(
            InstrumentId(),
            new PropertyId("target-current"),
            0.25m);

        Assert.Equal(expected, result.Status);
        Assert.Null(result.ConfirmedValue);
        Assert.DoesNotContain(
            sensitive,
            result.Diagnostic ?? string.Empty,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Write_WithoutConfirmedValueReturnsFailure()
    {
        RuntimeProperty property = FindProperty("target-current");
        var operations = CreateWritableOperations(
            (instrument, requestedProperty, value, token) => Task.FromResult(property));

        EndpointAttachmentPropertyOperationResult result = await operations.WriteAsync(
            InstrumentId(),
            property.Descriptor.Id,
            0.25m);

        Assert.Equal(EndpointAttachmentPropertyOperationStatus.Failure, result.Status);
        Assert.Null(result.ConfirmedValue);
    }

    [Fact]
    public async Task CallerCancellation_PropagatesWithoutCallingAdapter()
    {
        var called = false;
        var operations = new Kel103EndpointAttachmentPropertyOperations(
            (instrument, property, token) =>
            {
                called = true;
                return Task.FromResult(FindProperty("measured-voltage"));
            });
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operations.ReadAsync(
            InstrumentId(), new PropertyId("measured-voltage"), cancellation.Token));
        Assert.False(called);
    }

    [Fact]
    public async Task NullArguments_AreRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new Kel103EndpointAttachmentPropertyOperations(
                (Func<InstrumentId, PropertyId, CancellationToken, Task<RuntimeProperty>>)null!));
        var operations = new Kel103EndpointAttachmentPropertyOperations(
            (instrument, property, token) => Task.FromResult(FindProperty("measured-voltage")));
        Assert.Throws<ArgumentNullException>(() =>
            new Kel103EndpointAttachmentPropertyOperations(
                (instrument, property, token) => Task.FromResult(FindProperty("measured-voltage")),
                null!,
                static () => false,
                null,
                TimeProvider.System));
        await Assert.ThrowsAsync<ArgumentNullException>(() => operations.ReadAsync(null!, new PropertyId("p")));
        await Assert.ThrowsAsync<ArgumentNullException>(() => operations.ReadAsync(InstrumentId(), null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => operations.WriteAsync(
            null!,
            new PropertyId("p"),
            0.25m));
        await Assert.ThrowsAsync<ArgumentNullException>(() => operations.WriteAsync(
            InstrumentId(),
            null!,
            0.25m));
    }

    [Fact]
    public void Assembly_DoesNotReferencePresentationOrRemoteLayers()
    {
        string[] references = typeof(Kel103EndpointAttachmentPropertyOperations).Assembly
            .GetReferencedAssemblies().Select(value => value.Name ?? string.Empty).ToArray();
        Assert.DoesNotContain(references, name => name.Contains("Grpc", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.Contains("Wpf", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name == "Hase.Client");
        Assert.DoesNotContain(references, name => name == "Hase.DesktopHost");
    }

    [Theory]
    [InlineData(0, EndpointAttachmentPropertyOperationStatus.TimedOut)]
    [InlineData(1, EndpointAttachmentPropertyOperationStatus.Failure)]
    [InlineData(2, EndpointAttachmentPropertyOperationStatus.Unavailable)]
    [InlineData(3, EndpointAttachmentPropertyOperationStatus.Unavailable)]
    public async Task FaultedSession_ProjectsSanitizedFaultAndPreservesOperationResult(
        int failure,
        EndpointAttachmentPropertyOperationStatus expectedStatus)
    {
        const string sensitive = "sensitive transport detail";
        RuntimeEndpoint endpoint = ReadyEndpoint();
        Exception exception = failure switch
        {
            0 => new TimeoutException(sensitive),
            1 => new InvalidDataException(sensitive),
            2 => new InvalidOperationException(sensitive),
            _ => new IOException(sensitive)
        };
        var operations = new Kel103EndpointAttachmentPropertyOperations(
            (instrument, property, token) => Task.FromException<RuntimeProperty>(exception),
            static () => true,
            endpoint,
            new FixedTimeProvider());

        EndpointAttachmentPropertyOperationResult result = await operations.ReadAsync(
            InstrumentId(),
            new PropertyId("measured-voltage"));

        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal(EndpointConnectionState.Faulted, endpoint.ConnectionStatus.State);
        Assert.Equal(FixedTimeProvider.Timestamp, endpoint.ConnectionStatus.ChangedAtUtc);
        Assert.Equal("The KEL-103 communication session is faulted.", endpoint.ConnectionStatus.Detail);
        Assert.DoesNotContain(
            sensitive,
            endpoint.ConnectionStatus.Detail ?? string.Empty,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task FailedRead_WithUsableSession_DoesNotChangeReadyState()
    {
        RuntimeEndpoint endpoint = ReadyEndpoint();
        var operations = new Kel103EndpointAttachmentPropertyOperations(
            (instrument, property, token) =>
                Task.FromException<RuntimeProperty>(new TimeoutException()),
            static () => false,
            endpoint,
            new FixedTimeProvider());

        EndpointAttachmentPropertyOperationResult result = await operations.ReadAsync(
            InstrumentId(),
            new PropertyId("measured-voltage"));

        Assert.Equal(EndpointAttachmentPropertyOperationStatus.TimedOut, result.Status);
        Assert.Equal(EndpointConnectionState.Ready, endpoint.ConnectionStatus.State);
    }

    [Fact]
    public async Task InputOnWrite_WithUsableSessionDoesNotChangeReadyState()
    {
        RuntimeEndpoint endpoint = ReadyEndpoint();
        var operations = new Kel103EndpointAttachmentPropertyOperations(
            (instrument, property, token) => Task.FromResult(FindProperty("target-current")),
            (instrument, property, value, token) =>
                Task.FromException<RuntimeProperty>(new InvalidOperationException()),
            static () => false,
            endpoint,
            new FixedTimeProvider());

        EndpointAttachmentPropertyOperationResult result = await operations.WriteAsync(
            InstrumentId(),
            new PropertyId("target-current"),
            0.25m);

        Assert.Equal(EndpointAttachmentPropertyOperationStatus.Unavailable, result.Status);
        Assert.Equal(EndpointConnectionState.Ready, endpoint.ConnectionStatus.State);
    }

    [Theory]
    [InlineData(0, EndpointAttachmentPropertyOperationStatus.TimedOut)]
    [InlineData(1, EndpointAttachmentPropertyOperationStatus.Failure)]
    [InlineData(2, EndpointAttachmentPropertyOperationStatus.Unavailable)]
    [InlineData(3, EndpointAttachmentPropertyOperationStatus.Unavailable)]
    public async Task FaultedSession_WriteProjectsSanitizedFaultAndPreservesOperationResult(
        int failure,
        EndpointAttachmentPropertyOperationStatus expectedStatus)
    {
        const string sensitive = "sensitive write transport detail";
        RuntimeEndpoint endpoint = ReadyEndpoint();
        Exception exception = failure switch
        {
            0 => new TimeoutException(sensitive),
            1 => new InvalidDataException(sensitive),
            2 => new InvalidOperationException(sensitive),
            _ => new IOException(sensitive)
        };
        var operations = new Kel103EndpointAttachmentPropertyOperations(
            (instrument, property, token) => Task.FromResult(FindProperty("target-current")),
            (instrument, property, value, token) =>
                Task.FromException<RuntimeProperty>(exception),
            static () => true,
            endpoint,
            new FixedTimeProvider());

        EndpointAttachmentPropertyOperationResult result = await operations.WriteAsync(
            InstrumentId(),
            new PropertyId("target-current"),
            0.25m);

        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal(EndpointConnectionState.Faulted, endpoint.ConnectionStatus.State);
        Assert.Equal(FixedTimeProvider.Timestamp, endpoint.ConnectionStatus.ChangedAtUtc);
        Assert.Equal("The KEL-103 communication session is faulted.", endpoint.ConnectionStatus.Detail);
        Assert.DoesNotContain(
            sensitive,
            endpoint.ConnectionStatus.Detail ?? string.Empty,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreCanceledWrite_DoesNotCallAdapterOrProjectFault()
    {
        RuntimeEndpoint endpoint = ReadyEndpoint();
        var called = false;
        var operations = new Kel103EndpointAttachmentPropertyOperations(
            (instrument, property, token) => Task.FromResult(FindProperty("target-current")),
            (instrument, property, value, token) =>
            {
                called = true;
                return Task.FromResult(FindProperty("target-current"));
            },
            static () => true,
            endpoint,
            new FixedTimeProvider());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operations.WriteAsync(
            InstrumentId(),
            new PropertyId("target-current"),
            0.25m,
            cancellation.Token));

        Assert.False(called);
        Assert.Equal(EndpointConnectionState.Ready, endpoint.ConnectionStatus.State);
    }

    [Fact]
    public async Task UnsupportedRead_DoesNotProjectFaultEvenWhenPredicateIsTrue()
    {
        RuntimeEndpoint endpoint = ReadyEndpoint();
        var operations = new Kel103EndpointAttachmentPropertyOperations(
            (instrument, property, token) =>
                Task.FromException<RuntimeProperty>(new KeyNotFoundException()),
            static () => true,
            endpoint,
            new FixedTimeProvider());

        EndpointAttachmentPropertyOperationResult result = await operations.ReadAsync(
            InstrumentId(),
            new PropertyId("unsupported"));

        Assert.Equal(EndpointAttachmentPropertyOperationStatus.NotSupported, result.Status);
        Assert.Equal(EndpointConnectionState.Ready, endpoint.ConnectionStatus.State);
    }

    [Fact]
    public async Task InFlightCancellation_ProjectsFaultOnlyAfterSessionFaults()
    {
        RuntimeEndpoint endpoint = ReadyEndpoint();
        using var cancellation = new CancellationTokenSource();
        var operations = new Kel103EndpointAttachmentPropertyOperations(
            (instrument, property, token) =>
            {
                cancellation.Cancel();
                return Task.FromCanceled<RuntimeProperty>(token);
            },
            static () => true,
            endpoint,
            new FixedTimeProvider());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operations.ReadAsync(
            InstrumentId(),
            new PropertyId("measured-voltage"),
            cancellation.Token));

        Assert.Equal(EndpointConnectionState.Faulted, endpoint.ConnectionStatus.State);
    }

    [Fact]
    public async Task PreCanceledReadAndWrite_DoNotProjectFaultOrCallAdapter()
    {
        RuntimeEndpoint endpoint = ReadyEndpoint();
        var called = false;
        var operations = new Kel103EndpointAttachmentPropertyOperations(
            (instrument, property, token) =>
            {
                called = true;
                return Task.FromResult(FindProperty("measured-voltage"));
            },
            static () => true,
            endpoint,
            new FixedTimeProvider());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operations.ReadAsync(
            InstrumentId(),
            new PropertyId("measured-voltage"),
            cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operations.WriteAsync(
            InstrumentId(),
            new PropertyId("measured-voltage"),
            1m,
            cancellation.Token));

        Assert.False(called);
        Assert.Equal(EndpointConnectionState.Ready, endpoint.ConnectionStatus.State);
    }

    private static InstrumentId InstrumentId() => new("electronic-load-01");

    private static Kel103EndpointAttachmentPropertyOperations CreateWritableOperations(
        Func<InstrumentId, PropertyId, object?, CancellationToken, Task<RuntimeProperty>> writeAsync) =>
        new(
            (instrument, property, token) => Task.FromResult(FindProperty("target-current")),
            writeAsync,
            static () => false,
            null,
            TimeProvider.System);

    private static RuntimeProperty FindProperty(string id)
    {
        RuntimeEndpoint endpoint = new RuntimeContext().CreateEndpoint(
            Kel103ControlledSetpointDefinition.EndpointDefinition.Materialize(
                new EndpointId("test-endpoint")));
        return endpoint.Instruments.Single().Properties.Single(property => property.Descriptor.Id == new PropertyId(id));
    }

    private static RuntimeEndpoint ReadyEndpoint()
    {
        RuntimeEndpoint endpoint = new RuntimeContext().CreateEndpoint(
            Kel103ReadOnlyMeasurementDefinition.EndpointDefinition.Materialize(
                new EndpointId("fault-projection-test")));
        endpoint.UpdateConnectionStatus(
            new EndpointConnectionStatus(EndpointConnectionState.Ready));
        return endpoint;
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public static DateTimeOffset Timestamp { get; } =
            new(2026, 8, 3, 20, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Timestamp;
    }
}
