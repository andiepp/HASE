using Hase.ProtocolExplorer.ScpiCharacterization;
using Hase.Scpi;
using Hase.Transport.Serial;
using Xunit;

namespace Hase.ProtocolExplorer.Tests.ScpiCharacterization;

public sealed class Kel103ModeSelectionCharacterizerTests
{
    [Theory]
    [InlineData(1, ":FUNCtion CV\r", "CV")]
    [InlineData(2, ":FUNCtion CR\r", "CR")]
    [InlineData(3, ":FUNCtion CW\r", "CW")]
    [InlineData(4, ":FUNCtion SHORt\r", "SHORt")]
    public async Task CharacterizeAsync_SelectsConfirmsAndRestoresExactlyOnce(
        int destinationValue,
        string destinationCommand,
        string destinationReadback)
    {
        var stream = CreateSuccessfulStream(destinationReadback);
        var characterizer = new Kel103ModeSelectionCharacterizer(
            new StubSerialByteStreamFactory(stream));

        Kel103ModeSelectionCharacterizationResult result = await characterizer
            .CharacterizeAsync(
                new SerialTransportOptions("TEST-TARGET", 115200),
                (Kel103ModeSelection)destinationValue);

        Assert.Equal(
            new[]
            {
                "*IDN?\r", ":INPut?\r", ":FUNCtion?\r",
                ":VOLTage?\r", ":CURRent?\r", ":RESistance?\r", ":POWer?\r",
                destinationCommand, ":INPut?\r", ":FUNCtion?\r",
                ":FUNCtion CC\r", ":INPut?\r", ":FUNCtion?\r",
                ":VOLTage?\r", ":CURRent?\r", ":RESistance?\r", ":POWer?\r"
            },
            stream.Writes);
        Assert.Equal((Kel103ModeSelection)destinationValue, result.RequestedMode);
        Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task CharacterizeAsync_DoesNotSendModeCommandAfterBaselineInputOn()
    {
        var stream = new ScriptedSerialByteStream(
            "RND 320-KEL103 V3.30 SN:REDACTED\n",
            "ON\n");
        var characterizer = new Kel103ModeSelectionCharacterizer(
            new StubSerialByteStreamFactory(stream));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            characterizer.CharacterizeAsync(
                new SerialTransportOptions("TEST-TARGET", 115200),
                Kel103ModeSelection.ConstantVoltage));

        Assert.StartsWith("Initial input verification failed.", exception.Message);
        Assert.Contains("No mode-selection command was transmitted.", exception.Message);
        Assert.Equal(new[] { "*IDN?\r", ":INPut?\r" }, stream.Writes);
        Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task CharacterizeAsync_DoesNotRestoreAfterDestinationMismatch()
    {
        var stream = new ScriptedSerialByteStream(
            BaselineResponses().Concat(new[] { "OFF\n", "CR\n" }).ToArray());
        var characterizer = new Kel103ModeSelectionCharacterizer(
            new StubSerialByteStreamFactory(stream));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            characterizer.CharacterizeAsync(
                new SerialTransportOptions("TEST-TARGET", 115200),
                Kel103ModeSelection.ConstantVoltage));

        Assert.StartsWith("Destination mode verification failed", exception.Message);
        Assert.Contains("one destination command transmission", exception.Message);
        Assert.Contains("No restoration command was transmitted", exception.Message);
        Assert.Equal(10, stream.Writes.Count);
        Assert.DoesNotContain(":FUNCtion CC\r", stream.Writes);
        Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task CharacterizeAsync_DoesNotRetryUncertainDestinationTransmission()
    {
        var stream = new ScriptedSerialByteStream(
            BaselineResponses().ToArray())
        {
            FailOnWriteNumber = 8
        };
        var characterizer = new Kel103ModeSelectionCharacterizer(
            new StubSerialByteStreamFactory(stream));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            characterizer.CharacterizeAsync(
                new SerialTransportOptions("TEST-TARGET", 115200),
                Kel103ModeSelection.ConstantVoltage));

        Assert.StartsWith("Destination command transmission is uncertain.", exception.Message);
        Assert.IsType<ScpiCommandTransmissionException>(exception.InnerException);
        Assert.Equal(8, stream.Writes.Count);
        Assert.DoesNotContain(":FUNCtion CC\r", stream.Writes);
        Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task CharacterizeAsync_DoesNotRetryUncertainRestorationTransmission()
    {
        var stream = new ScriptedSerialByteStream(
            BaselineResponses().Concat(new[] { "OFF\n", "CV\n" }).ToArray())
        {
            FailOnWriteNumber = 11
        };
        var characterizer = new Kel103ModeSelectionCharacterizer(
            new StubSerialByteStreamFactory(stream));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            characterizer.CharacterizeAsync(
                new SerialTransportOptions("TEST-TARGET", 115200),
                Kel103ModeSelection.ConstantVoltage));

        Assert.StartsWith("Restoration command transmission is uncertain", exception.Message);
        Assert.IsType<ScpiCommandTransmissionException>(exception.InnerException);
        Assert.Equal(11, stream.Writes.Count);
        Assert.Equal(1, stream.Writes.Count(write => write == ":FUNCtion CC\r"));
        Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task CharacterizeAsync_IdentifiesInitialCcFailureBeforeMutation()
    {
        var stream = new ScriptedSerialByteStream(
            "RND 320-KEL103 V3.30 SN:REDACTED\n",
            "OFF\n",
            "CV\n");
        var characterizer = new Kel103ModeSelectionCharacterizer(
            new StubSerialByteStreamFactory(stream));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            characterizer.CharacterizeAsync(
                new SerialTransportOptions("TEST-TARGET", 115200),
                Kel103ModeSelection.ConstantVoltage));

        Assert.StartsWith("Initial CC verification failed.", exception.Message);
        Assert.Contains("No mode-selection command was transmitted.", exception.Message);
        Assert.Equal(3, stream.Writes.Count);
        Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task CharacterizeAsync_IdentifiesDestinationInputFailureAndSuppressesRestoration()
    {
        var stream = new ScriptedSerialByteStream(
            BaselineResponses().Concat(new[] { "ON\n" }).ToArray());
        var characterizer = new Kel103ModeSelectionCharacterizer(
            new StubSerialByteStreamFactory(stream));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            characterizer.CharacterizeAsync(
                new SerialTransportOptions("TEST-TARGET", 115200),
                Kel103ModeSelection.ConstantVoltage));

        Assert.StartsWith("Destination input verification failed", exception.Message);
        Assert.Contains("No restoration command was transmitted", exception.Message);
        Assert.Equal(9, stream.Writes.Count);
        Assert.DoesNotContain(":FUNCtion CC\r", stream.Writes);
        Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task CharacterizeAsync_IdentifiesInitialSetpointSynchronizationFailure()
    {
        var stream = new ScriptedSerialByteStream(
            "RND 320-KEL103 V3.30 SN:REDACTED\n",
            "OFF\n", "CC\n", "MALFORMED\n");
        var characterizer = new Kel103ModeSelectionCharacterizer(
            new StubSerialByteStreamFactory(stream));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            characterizer.CharacterizeAsync(
                new SerialTransportOptions("TEST-TARGET", 115200),
                Kel103ModeSelection.ConstantVoltage));

        Assert.StartsWith("Initial setpoint synchronization failed.", exception.Message);
        Assert.Contains("No mode-selection command was transmitted.", exception.Message);
        Assert.Equal(4, stream.Writes.Count);
        Assert.True(stream.Disposed);
    }

    [Theory]
    [InlineData("ON", "CC", "Restoration input verification failed")]
    [InlineData("OFF", "CV", "Restoration CC verification failed")]
    public async Task CharacterizeAsync_IdentifiesRestorationVerificationFailure(
        string restoredInput,
        string restoredMode,
        string expectedMessage)
    {
        var stream = new ScriptedSerialByteStream(
            BaselineResponses().Concat(
                new[] { "OFF\n", "CV\n", restoredInput + "\n", restoredMode + "\n" })
                .ToArray());
        var characterizer = new Kel103ModeSelectionCharacterizer(
            new StubSerialByteStreamFactory(stream));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            characterizer.CharacterizeAsync(
                new SerialTransportOptions("TEST-TARGET", 115200),
                Kel103ModeSelection.ConstantVoltage));

        Assert.StartsWith(expectedMessage, exception.Message);
        Assert.Contains("one destination and one restoration command transmission", exception.Message);
        Assert.Equal(1, stream.Writes.Count(write => write == ":FUNCtion CC\r"));
        Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task CharacterizeAsync_IdentifiesFinalSetpointVerificationFailure()
    {
        var stream = new ScriptedSerialByteStream(
            BaselineResponses().Concat(
                new[] { "OFF\n", "CV\n", "OFF\n", "CC\n", "MALFORMED\n" })
                .ToArray());
        var characterizer = new Kel103ModeSelectionCharacterizer(
            new StubSerialByteStreamFactory(stream));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            characterizer.CharacterizeAsync(
                new SerialTransportOptions("TEST-TARGET", 115200),
                Kel103ModeSelection.ConstantVoltage));

        Assert.StartsWith("Final setpoint verification failed", exception.Message);
        Assert.Contains("one destination and one restoration command transmission", exception.Message);
        Assert.DoesNotContain("MALFORMED", exception.Message, StringComparison.Ordinal);
        Assert.Equal(14, stream.Writes.Count);
        Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task CharacterizeAsync_RejectsChangedTargetWithoutDisclosingValues()
    {
        var stream = new ScriptedSerialByteStream(
            BaselineResponses().Concat(
                new[]
                {
                    "OFF\n", "CV\n", "OFF\n", "CC\n",
                    "0.2000V\n", "0.1000A\n", "0.1000OHM\n", "0.1000W\n"
                }).ToArray());
        var characterizer = new Kel103ModeSelectionCharacterizer(
            new StubSerialByteStreamFactory(stream));

        InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            characterizer.CharacterizeAsync(
                new SerialTransportOptions("TEST-TARGET", 115200),
                Kel103ModeSelection.ConstantVoltage));

        Assert.DoesNotContain("0.1000", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("0.2000", exception.Message, StringComparison.Ordinal);
        Assert.Equal(17, stream.Writes.Count);
        Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task CharacterizeAsync_RejectsCcAndUndefinedDestinationBeforeOpening()
    {
        var factory = new CountingSerialByteStreamFactory();
        var characterizer = new Kel103ModeSelectionCharacterizer(factory);
        var options = new SerialTransportOptions("TEST-TARGET", 115200);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            characterizer.CharacterizeAsync(options, Kel103ModeSelection.ConstantCurrent));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            characterizer.CharacterizeAsync(options, (Kel103ModeSelection)99));

        Assert.Equal(0, factory.OpenCount);
    }

    private static ScriptedSerialByteStream CreateSuccessfulStream(string mode) =>
        new(BaselineResponses().Concat(
            new[]
            {
                "OFF\n", mode + "\n", "OFF\n", "CC\n",
                "0.1000V\n", "0.1000A\n", "0.1000OHM\n", "0.1000W\n"
            }).ToArray());

    private static IEnumerable<string> BaselineResponses() =>
    [
        "RND 320-KEL103 V3.30 SN:REDACTED\n",
        "OFF\n", "CC\n",
        "0.1000V\n", "0.1000A\n", "0.1000OHM\n", "0.1000W\n"
    ];

    private sealed class StubSerialByteStreamFactory(ISerialByteStream stream)
        : ISerialByteStreamFactory
    {
        public ValueTask<ISerialByteStream> OpenAsync(
            SerialTransportOptions options,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(stream);
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

    private sealed class ScriptedSerialByteStream(params string[] responses)
        : ISerialByteStream
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
