using Hase.ProtocolExplorer.ScpiCharacterization;
using Hase.Scpi;
using Hase.Transport.Serial;
using Xunit;

namespace Hase.ProtocolExplorer.Tests.ScpiCharacterization;

public sealed class Kel103SetpointWriteCharacterizerTests
{
    [Theory]
    [InlineData(2, ":VOLTage 1V\r", "CV", true)]
    [InlineData(3, ":CURRent 2A\r", "CC", false)]
    [InlineData(4, ":RESistance 3OHM\r", "CR", true)]
    [InlineData(5, ":POWer 4W\r", "CW", true)]
    public async Task CharacterizeAsync_TransmitsCurrentValueOnceAndVerifiesUnchangedState(
        int candidateValue,
        string expectedSetter,
        string expectedMode,
        bool expectedRestoration)
    {
        var stream = CreateSuccessfulStream(expectedMode, expectedRestoration);
        var characterizer = new Kel103SetpointWriteCharacterizer(
            new StubSerialByteStreamFactory(stream));

        Kel103SetpointWriteCharacterizationResult result = await characterizer
            .CharacterizeAsync(
                new SerialTransportOptions("TEST-TARGET", 115200),
                (Kel103StateCandidate)candidateValue);

        var expectedWrites = new List<string>
        {
            "*IDN?\r", ":INPut?\r", ":FUNCtion?\r",
            ":VOLTage?\r", ":CURRent?\r", ":RESistance?\r", ":POWer?\r",
            expectedSetter,
            ":INPut?\r", ":FUNCtion?\r",
            ":VOLTage?\r", ":CURRent?\r", ":RESistance?\r", ":POWer?\r"
        };
        if (expectedRestoration)
        {
            expectedWrites.AddRange(
            [
                ":FUNCtion CC\r", ":INPut?\r", ":FUNCtion?\r",
                ":VOLTage?\r", ":CURRent?\r", ":RESistance?\r", ":POWer?\r"
            ]);
        }

        Assert.Equal(expectedWrites, stream.Writes);
        Assert.Equal((Kel103StateCandidate)candidateValue, result.Candidate);
        Assert.Equal(expectedRestoration, result.RestorationCommandTransmitted);
        Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task CharacterizeAsync_DoesNotSendSetterWhenInitialInputIsOn()
    {
        var stream = new ScriptedSerialByteStream(
            "RND 320-KEL103 V3.30 SN:REDACTED\n",
            "ON\n");
        var characterizer = new Kel103SetpointWriteCharacterizer(
            new StubSerialByteStreamFactory(stream));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            characterizer.CharacterizeAsync(
                new SerialTransportOptions("TEST-TARGET", 115200),
                Kel103StateCandidate.TargetVoltage));

        Assert.StartsWith("Initial input verification failed.", exception.Message);
        Assert.Contains("No setpoint setter was transmitted.", exception.Message);
        Assert.Equal(new[] { "*IDN?\r", ":INPut?\r" }, stream.Writes);
        Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task CharacterizeAsync_DoesNotRetryUncertainSetterTransmission()
    {
        var stream = new ScriptedSerialByteStream(InitialResponses().ToArray())
        {
            FailOnWriteNumber = 8
        };
        var characterizer = new Kel103SetpointWriteCharacterizer(
            new StubSerialByteStreamFactory(stream));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            characterizer.CharacterizeAsync(
                new SerialTransportOptions("TEST-TARGET", 115200),
                Kel103StateCandidate.TargetVoltage));

        Assert.StartsWith("Same-value setpoint setter transmission is uncertain.", exception.Message);
        Assert.IsType<ScpiCommandTransmissionException>(exception.InnerException);
        Assert.Equal(8, stream.Writes.Count);
        Assert.Equal(1, stream.Writes.Count(write => write == ":VOLTage 1V\r"));
        Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task CharacterizeAsync_DoesNotSendSetterWhenInitialModeIsNotCc()
    {
        var stream = new ScriptedSerialByteStream(
            "RND 320-KEL103 V3.30 SN:REDACTED\n",
            "OFF\n", "CV\n");
        var characterizer = new Kel103SetpointWriteCharacterizer(
            new StubSerialByteStreamFactory(stream));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            characterizer.CharacterizeAsync(
                new SerialTransportOptions("TEST-TARGET", 115200),
                Kel103StateCandidate.TargetVoltage));

        Assert.StartsWith("Initial CC verification failed.", exception.Message);
        Assert.Contains("No setpoint setter was transmitted.", exception.Message);
        Assert.Equal(3, stream.Writes.Count);
        Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task CharacterizeAsync_ReportsPostSetterMismatchWithoutAdditionalCommandOrValues()
    {
        var stream = new ScriptedSerialByteStream(
            InitialResponses().Concat(
                new[]
                {
                    "OFF\n", "CV\n",
                    "9V\n", "2A\n", "3OHM\n", "4W\n"
                }).ToArray());
        var characterizer = new Kel103SetpointWriteCharacterizer(
            new StubSerialByteStreamFactory(stream));

        InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            characterizer.CharacterizeAsync(
                new SerialTransportOptions("TEST-TARGET", 115200),
                Kel103StateCandidate.TargetVoltage));

        Assert.StartsWith("Post-setter setpoint comparison failed", exception.Message);
        Assert.DoesNotContain("1V", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("9V", exception.Message, StringComparison.Ordinal);
        Assert.Equal(14, stream.Writes.Count);
        Assert.Equal(1, stream.Writes.Count(write => write == ":VOLTage 1V\r"));
        Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task CharacterizeAsync_DoesNotRestoreAfterUnexpectedSetterMode()
    {
        var stream = new ScriptedSerialByteStream(
            InitialResponses().Concat(
                new[]
                {
                    "OFF\n", "CC\n",
                    "1V\n", "2A\n", "3OHM\n", "4W\n"
                }).ToArray());
        var characterizer = new Kel103SetpointWriteCharacterizer(
            new StubSerialByteStreamFactory(stream));

        InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            characterizer.CharacterizeAsync(
                new SerialTransportOptions("TEST-TARGET", 115200),
                Kel103StateCandidate.TargetVoltage));

        Assert.StartsWith("Post-setter expected-mode comparison failed", exception.Message);
        Assert.Contains("No additional command was transmitted", exception.Message);
        Assert.Equal(14, stream.Writes.Count);
        Assert.DoesNotContain(":FUNCtion CC\r", stream.Writes);
        Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task CharacterizeAsync_DoesNotRetryUncertainCcRestoration()
    {
        var stream = new ScriptedSerialByteStream(
            InitialResponses().Concat(
                new[]
                {
                    "OFF\n", "CV\n",
                    "1V\n", "2A\n", "3OHM\n", "4W\n"
                }).ToArray())
        {
            FailOnWriteNumber = 15
        };
        var characterizer = new Kel103SetpointWriteCharacterizer(
            new StubSerialByteStreamFactory(stream));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            characterizer.CharacterizeAsync(
                new SerialTransportOptions("TEST-TARGET", 115200),
                Kel103StateCandidate.TargetVoltage));

        Assert.StartsWith("CC restoration transmission is uncertain", exception.Message);
        Assert.IsType<ScpiCommandTransmissionException>(exception.InnerException);
        Assert.Equal(15, stream.Writes.Count);
        Assert.Equal(1, stream.Writes.Count(write => write == ":FUNCtion CC\r"));
        Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task CharacterizeAsync_RejectsNonSetpointBeforeOpeningStream()
    {
        var factory = new CountingSerialByteStreamFactory();
        var characterizer = new Kel103SetpointWriteCharacterizer(factory);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            characterizer.CharacterizeAsync(
                new SerialTransportOptions("TEST-TARGET", 115200),
                Kel103StateCandidate.Mode));

        Assert.Equal(0, factory.OpenCount);
    }

    [Fact]
    public void Constructor_RejectsNullFactory()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new Kel103SetpointWriteCharacterizer(null!));
    }

    private static ScriptedSerialByteStream CreateSuccessfulStream(
        string expectedMode,
        bool expectedRestoration)
    {
        IEnumerable<string> responses = InitialResponses().Concat(
            new[]
            {
                "OFF\n", expectedMode + "\n",
                "1V\n", "2A\n", "3OHM\n", "4W\n"
            });
        if (expectedRestoration)
        {
            responses = responses.Concat(
                new[]
                {
                    "OFF\n", "CC\n",
                    "1V\n", "2A\n", "3OHM\n", "4W\n"
                });
        }

        return new ScriptedSerialByteStream(responses.ToArray());
    }

    private static IEnumerable<string> InitialResponses() =>
    [
        "RND 320-KEL103 V3.30 SN:REDACTED\n",
        "OFF\n", "CC\n",
        "1V\n", "2A\n", "3OHM\n", "4W\n"
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
