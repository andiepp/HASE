using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
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
        await Assert.ThrowsAsync<ArgumentNullException>(() => operations.ReadAsync(null!, new PropertyId("p")));
        await Assert.ThrowsAsync<ArgumentNullException>(() => operations.ReadAsync(InstrumentId(), null!));
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

    private static InstrumentId InstrumentId() => new("electronic-load-01");

    private static RuntimeProperty FindProperty(string id)
    {
        RuntimeEndpoint endpoint = new RuntimeContext().CreateEndpoint(
            Kel103ReadOnlyMeasurementDefinition.EndpointDefinition.Materialize(new EndpointId("test-endpoint")));
        return endpoint.Instruments.Single().Properties.Single(property => property.Descriptor.Id == new PropertyId(id));
    }
}
