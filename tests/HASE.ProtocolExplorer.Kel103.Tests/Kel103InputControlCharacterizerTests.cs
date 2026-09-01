using Hase.ProtocolExplorer.ScpiCharacterization;
using Hase.Scpi;
using Hase.Transport.Serial;
using Xunit;

namespace Hase.ProtocolExplorer.Tests.ScpiCharacterization;

public sealed class Kel103InputControlCharacterizerTests
{
    [Fact]
    public async Task CharacterizeAsync_ActivatesAndDeactivatesExactlyOnce()
    {
        var stream = new ScriptedSerialByteStream(SuccessfulResponses());
        var characterizer = CreateCharacterizer(stream);

        Kel103InputControlCharacterizationResult result = await characterizer
            .CharacterizeAsync(new SerialTransportOptions("TEST-TARGET", 115200), true);

        Assert.Equal(
            new[]
            {
                "*IDN?\r", ":INPut?\r",
                ":FUNCtion?\r", ":VOLTage?\r", ":CURRent?\r", ":RESistance?\r", ":POWer?\r",
                ":INPut ON\r", ":INPut?\r",
                ":FUNCtion?\r", ":VOLTage?\r", ":CURRent?\r", ":RESistance?\r", ":POWer?\r",
                ":INPut OFF\r", ":INPut?\r",
                ":FUNCtion?\r", ":VOLTage?\r", ":CURRent?\r", ":RESistance?\r", ":POWer?\r"
            },
            stream.Writes);
        Assert.Equal("KEL-103", result.Identity.ProductIdentity);
        Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task CharacterizeAsync_RejectsConfirmationBeforeOpening()
    {
        var factory = new CountingSerialByteStreamFactory();
        var characterizer = new Kel103InputControlCharacterizer(factory);

        await Assert.ThrowsAsync<ArgumentException>(() => characterizer.CharacterizeAsync(
            new SerialTransportOptions("TEST-TARGET", 115200), false));

        Assert.Equal(0, factory.OpenCount);
    }

    [Theory]
    [InlineData("ON", "Initial input verification failed")]
    [InlineData("OFF", "Initial CC verification failed")]
    public async Task CharacterizeAsync_RejectsInvalidBaselineWithoutMutation(
        string input,
        string expectedMessage)
    {
        string[] responses = input == "ON"
            ? [Identity, "ON\n"]
            : [Identity, "OFF\n", "CV\n", Voltage, Current, Resistance, Power];
        var stream = new ScriptedSerialByteStream(responses);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateCharacterizer(stream).CharacterizeAsync(
                new SerialTransportOptions("TEST-TARGET", 115200), true));

        Assert.StartsWith(expectedMessage, exception.Message);
        Assert.DoesNotContain(":INPut ON\r", stream.Writes);
        Assert.DoesNotContain(":INPut OFF\r", stream.Writes);
    }

    [Fact]
    public async Task CharacterizeAsync_DoesNotRetryOrDeactivateAfterUncertainActivationTransmission()
    {
        var stream = new ScriptedSerialByteStream(BaselineResponses()) { FailOnWriteNumber = 8 };

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateCharacterizer(stream).CharacterizeAsync(
                new SerialTransportOptions("TEST-TARGET", 115200), true));

        Assert.StartsWith("Activation command transmission is uncertain", exception.Message);
        Assert.IsType<ScpiCommandTransmissionException>(exception.InnerException);
        Assert.Equal(1, stream.Writes.Count(write => write == ":INPut ON\r"));
        Assert.DoesNotContain(":INPut OFF\r", stream.Writes);
    }

    [Fact]
    public async Task CharacterizeAsync_DoesNotDeactivateWhenActivationIsNotConfirmed()
    {
        var stream = new ScriptedSerialByteStream(BaselineResponses().Concat(["OFF\n"]).ToArray());

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateCharacterizer(stream).CharacterizeAsync(
                new SerialTransportOptions("TEST-TARGET", 115200), true));

        Assert.StartsWith("Activation readback failed", exception.Message);
        Assert.Equal(1, stream.Writes.Count(write => write == ":INPut ON\r"));
        Assert.DoesNotContain(":INPut OFF\r", stream.Writes);
    }

    [Fact]
    public async Task CharacterizeAsync_DeactivatesAfterConfirmedOnEvenWhenActivatedStateChanged()
    {
        string[] responses = BaselineResponses()
            .Concat(["ON\n", "CV\n", Voltage, Current, Resistance, Power])
            .Concat(["OFF\n", "CC\n", Voltage, Current, Resistance, Power])
            .ToArray();
        var stream = new ScriptedSerialByteStream(responses);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateCharacterizer(stream).CharacterizeAsync(
                new SerialTransportOptions("TEST-TARGET", 115200), true));

        Assert.StartsWith("Activated-state verification failed", exception.Message);
        Assert.Equal(1, stream.Writes.Count(write => write == ":INPut OFF\r"));
        Assert.Equal(21, stream.Writes.Count);
    }

    [Fact]
    public async Task CharacterizeAsync_DoesNotRetryUncertainDeactivationTransmission()
    {
        string[] responses = BaselineResponses()
            .Concat(["ON\n", "CC\n", Voltage, Current, Resistance, Power])
            .ToArray();
        var stream = new ScriptedSerialByteStream(responses) { FailOnWriteNumber = 15 };

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateCharacterizer(stream).CharacterizeAsync(
                new SerialTransportOptions("TEST-TARGET", 115200), true));

        Assert.StartsWith("Deactivation command transmission is uncertain", exception.Message);
        Assert.IsType<ScpiCommandTransmissionException>(exception.InnerException);
        Assert.Equal(1, stream.Writes.Count(write => write == ":INPut OFF\r"));
    }

    [Fact]
    public async Task CharacterizeAsync_RejectsChangedFinalStateWithoutDisclosingValues()
    {
        string[] responses = BaselineResponses()
            .Concat(["ON\n", "CC\n", Voltage, Current, Resistance, Power])
            .Concat(["OFF\n", "CC\n", "0.2000V\n", Current, Resistance, Power])
            .ToArray();
        var stream = new ScriptedSerialByteStream(responses);

        InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            CreateCharacterizer(stream).CharacterizeAsync(
                new SerialTransportOptions("TEST-TARGET", 115200), true));

        Assert.DoesNotContain("0.1000", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("0.2000", exception.Message, StringComparison.Ordinal);
        Assert.Equal(1, stream.Writes.Count(write => write == ":INPut OFF\r"));
    }

    private const string Identity = "RND 320-KEL103 V3.30 SN:REDACTED\n";
    private const string Voltage = "0.1000V\n";
    private const string Current = "0.1000A\n";
    private const string Resistance = "0.1000OHM\n";
    private const string Power = "0.1000W\n";

    private static string[] SuccessfulResponses() => BaselineResponses()
        .Concat(["ON\n", "CC\n", Voltage, Current, Resistance, Power])
        .Concat(["OFF\n", "CC\n", Voltage, Current, Resistance, Power])
        .ToArray();

    private static string[] BaselineResponses() =>
        [Identity, "OFF\n", "CC\n", Voltage, Current, Resistance, Power];

    private static Kel103InputControlCharacterizer CreateCharacterizer(
        ISerialByteStream stream) =>
        new(new StubSerialByteStreamFactory(stream));

    private sealed class StubSerialByteStreamFactory(ISerialByteStream stream)
        : ISerialByteStreamFactory
    {
        public ValueTask<ISerialByteStream> OpenAsync(
            SerialTransportOptions options,
            CancellationToken cancellationToken = default) => ValueTask.FromResult(stream);
    }

    private sealed class CountingSerialByteStreamFactory : ISerialByteStreamFactory
    {
        public int OpenCount { get; private set; }

        public ValueTask<ISerialByteStream> OpenAsync(
            SerialTransportOptions options,
            CancellationToken cancellationToken = default)
        {
            OpenCount++;
            throw new InvalidOperationException("The stream must not be opened.");
        }
    }

    private sealed class ScriptedSerialByteStream(params string[] responses) : ISerialByteStream
    {
        private readonly Queue<byte[]> pendingResponses = new(
            responses.Select(System.Text.Encoding.ASCII.GetBytes));

        public List<string> Writes { get; } = [];
        public int? FailOnWriteNumber { get; init; }
        public bool Disposed { get; private set; }

        public ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            byte[] response = pendingResponses.Dequeue();
            response.AsSpan().CopyTo(buffer.Span);
            return ValueTask.FromResult(response.Length);
        }

        public ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            Writes.Add(System.Text.Encoding.ASCII.GetString(buffer.Span));
            if (Writes.Count == FailOnWriteNumber)
            {
                throw new IOException("Simulated transmission uncertainty.");
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
