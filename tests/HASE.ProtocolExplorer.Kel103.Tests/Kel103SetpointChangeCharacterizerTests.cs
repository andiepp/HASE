using Hase.ProtocolExplorer.ScpiCharacterization;
using Hase.Scpi;
using Hase.Transport.Serial;
using Xunit;

namespace Hase.ProtocolExplorer.Tests.ScpiCharacterization;

public sealed class Kel103SetpointChangeCharacterizerTests
{
    [Theory]
    [InlineData(2, "CV", ":VOLT:LOW?\r", ":VOLT:UPP?\r", ":VOLTage 1.0001V\r", ":VOLTage 1.0000V\r", true)]
    [InlineData(3, "CC", ":CURR:LOW?\r", ":CURR:UPP?\r", ":CURRent 2.0001A\r", ":CURRent 2.0000A\r", false)]
    [InlineData(4, "CR", ":RES:LOW?\r", ":RES:UPP?\r", ":RESistance 3.0001OHM\r", ":RESistance 3.0000OHM\r", true)]
    [InlineData(5, "CW", ":POW:LOW?\r", ":POW:UPP?\r", ":POWer 4.0001W\r", ":POWer 4.0000W\r", true)]
    public async Task CharacterizeAsync_ChangesRestoresAndVerifiesExactlyOnce(
        int candidateValue,
        string expectedMode,
        string lowerQuery,
        string upperQuery,
        string changedSetter,
        string originalSetter,
        bool expectedModeRestoration)
    {
        var candidate = (Kel103StateCandidate)candidateValue;
        var stream = CreateSuccessfulStream(candidate, expectedMode, expectedModeRestoration);
        var characterizer = new Kel103SetpointChangeCharacterizer(
            new StubSerialByteStreamFactory(stream));

        Kel103SetpointChangeCharacterizationResult result = await characterizer
            .CharacterizeAsync(
                new SerialTransportOptions("TEST-TARGET", 115200),
                candidate);

        var expectedWrites = new List<string>
        {
            "*IDN?\r", ":INPut?\r", ":FUNCtion?\r",
            ":VOLTage?\r", ":CURRent?\r", ":RESistance?\r", ":POWer?\r",
            lowerQuery, upperQuery, changedSetter,
            ":INPut?\r", ":FUNCtion?\r",
            ":VOLTage?\r", ":CURRent?\r", ":RESistance?\r", ":POWer?\r",
            originalSetter,
            ":INPut?\r", ":FUNCtion?\r",
            ":VOLTage?\r", ":CURRent?\r", ":RESistance?\r", ":POWer?\r"
        };
        if (expectedModeRestoration)
        {
            expectedWrites.AddRange(
            [
                ":FUNCtion CC\r", ":INPut?\r", ":FUNCtion?\r",
                ":VOLTage?\r", ":CURRent?\r", ":RESistance?\r", ":POWer?\r"
            ]);
        }

        Assert.Equal(expectedWrites, stream.Writes);
        Assert.Equal(candidate, result.Candidate);
        Assert.Equal(expectedModeRestoration, result.ModeRestorationCommandTransmitted);
        Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task CharacterizeAsync_DoesNotSendWhenInitialInputIsOn()
    {
        var stream = new ScriptedSerialByteStream(
            "RND 320-KEL103 V3.30 SN:REDACTED\n",
            "ON\n");
        var characterizer = new Kel103SetpointChangeCharacterizer(
            new StubSerialByteStreamFactory(stream));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            characterizer.CharacterizeAsync(
                new SerialTransportOptions("TEST-TARGET", 115200),
                Kel103StateCandidate.TargetVoltage));

        Assert.StartsWith("Initial input verification failed.", exception.Message);
        Assert.Equal(2, stream.Writes.Count);
        Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task CharacterizeAsync_DoesNotRetryUncertainChangedSetter()
    {
        var stream = new ScriptedSerialByteStream(InitialResponses("V").ToArray())
        {
            FailOnWriteNumber = 10
        };
        var characterizer = new Kel103SetpointChangeCharacterizer(
            new StubSerialByteStreamFactory(stream));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            characterizer.CharacterizeAsync(
                new SerialTransportOptions("TEST-TARGET", 115200),
                Kel103StateCandidate.TargetVoltage));

        Assert.StartsWith("Changed-value setter transmission is uncertain.", exception.Message);
        Assert.IsType<ScpiCommandTransmissionException>(exception.InnerException);
        Assert.Equal(10, stream.Writes.Count);
        Assert.Equal(1, stream.Writes.Count(write => write == ":VOLTage 1.0001V\r"));
        Assert.DoesNotContain(":VOLTage 1.0000V\r", stream.Writes);
        Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task CharacterizeAsync_RestoresBeforeReportingQuantizedMismatch()
    {
        var stream = new ScriptedSerialByteStream(
            InitialResponses("V").Concat(
                PostSetterResponses(Kel103StateCandidate.TargetVoltage, "CV", "1.0002V"))
                .Concat(PostSetterResponses(Kel103StateCandidate.TargetVoltage, "CV", "1.0000V"))
                .Concat(FinalResponses())
                .ToArray());
        var characterizer = new Kel103SetpointChangeCharacterizer(
            new StubSerialByteStreamFactory(stream));

        InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            characterizer.CharacterizeAsync(
                new SerialTransportOptions("TEST-TARGET", 115200),
                Kel103StateCandidate.TargetVoltage));

        Assert.StartsWith("Changed-value readback did not match", exception.Message);
        Assert.Contains("original setpoint and CC state were restored", exception.Message);
        Assert.DoesNotContain("1.0001", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("1.0002", exception.Message, StringComparison.Ordinal);
        Assert.Equal(30, stream.Writes.Count);
        Assert.Equal(1, stream.Writes.Count(write => write == ":VOLTage 1.0001V\r"));
        Assert.Equal(1, stream.Writes.Count(write => write == ":VOLTage 1.0000V\r"));
        Assert.Equal(1, stream.Writes.Count(write => write == ":FUNCtion CC\r"));
        Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task CharacterizeAsync_DoesNotRetryUncertainOriginalSetpointRestoration()
    {
        var stream = new ScriptedSerialByteStream(
            InitialResponses("V").Concat(
                PostSetterResponses(Kel103StateCandidate.TargetVoltage, "CV", "1.0001V"))
                .ToArray())
        {
            FailOnWriteNumber = 17
        };
        var characterizer = new Kel103SetpointChangeCharacterizer(
            new StubSerialByteStreamFactory(stream));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            characterizer.CharacterizeAsync(
                new SerialTransportOptions("TEST-TARGET", 115200),
                Kel103StateCandidate.TargetVoltage));

        Assert.StartsWith("Original-setpoint restoration transmission is uncertain", exception.Message);
        Assert.IsType<ScpiCommandTransmissionException>(exception.InnerException);
        Assert.Equal(17, stream.Writes.Count);
        Assert.DoesNotContain(":FUNCtion CC\r", stream.Writes);
        Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task CharacterizeAsync_DoesNotRetryUncertainCcRestoration()
    {
        var stream = new ScriptedSerialByteStream(
            InitialResponses("V")
                .Concat(PostSetterResponses(Kel103StateCandidate.TargetVoltage, "CV", "1.0001V"))
                .Concat(PostSetterResponses(Kel103StateCandidate.TargetVoltage, "CV", "1.0000V"))
                .ToArray())
        {
            FailOnWriteNumber = 24
        };
        var characterizer = new Kel103SetpointChangeCharacterizer(
            new StubSerialByteStreamFactory(stream));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            characterizer.CharacterizeAsync(
                new SerialTransportOptions("TEST-TARGET", 115200),
                Kel103StateCandidate.TargetVoltage));

        Assert.StartsWith("CC restoration transmission is uncertain", exception.Message);
        Assert.IsType<ScpiCommandTransmissionException>(exception.InnerException);
        Assert.Equal(24, stream.Writes.Count);
        Assert.Equal(1, stream.Writes.Count(write => write == ":FUNCtion CC\r"));
        Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task CharacterizeAsync_DoesNotMutateWhenBoundsContainNoInteriorQuantum()
    {
        var stream = new ScriptedSerialByteStream(
            "RND 320-KEL103 V3.30 SN:REDACTED\n",
            "OFF\n", "CC\n",
            "0V\n", "2A\n", "3OHM\n", "4W\n",
            "0V\n", "1V\n");
        var characterizer = new Kel103SetpointChangeCharacterizer(
            new StubSerialByteStreamFactory(stream));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            characterizer.CharacterizeAsync(
                new SerialTransportOptions("TEST-TARGET", 115200),
                Kel103StateCandidate.TargetVoltage));

        Assert.Equal(9, stream.Writes.Count);
        Assert.DoesNotContain(":VOLTage 1V\r", stream.Writes);
        Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task CharacterizeAsync_DoesNotRestoreAfterIncompleteChangedState()
    {
        var stream = new ScriptedSerialByteStream(
            InitialResponses("V").Concat(new[] { "OFF\n", "BAD\n" }).ToArray());
        var characterizer = new Kel103SetpointChangeCharacterizer(
            new StubSerialByteStreamFactory(stream));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            characterizer.CharacterizeAsync(
                new SerialTransportOptions("TEST-TARGET", 115200),
                Kel103StateCandidate.TargetVoltage));

        Assert.StartsWith("Changed-value mode verification failed", exception.Message);
        Assert.Equal(12, stream.Writes.Count);
        Assert.DoesNotContain(":VOLTage 1.0000V\r", stream.Writes);
        Assert.DoesNotContain(":FUNCtion CC\r", stream.Writes);
        Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task CharacterizeAsync_RejectsNonSetpointBeforeOpening()
    {
        var factory = new CountingSerialByteStreamFactory();
        var characterizer = new Kel103SetpointChangeCharacterizer(factory);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            characterizer.CharacterizeAsync(
                new SerialTransportOptions("TEST-TARGET", 115200),
                Kel103StateCandidate.InputState));

        Assert.Equal(0, factory.OpenCount);
    }

    private static ScriptedSerialByteStream CreateSuccessfulStream(
        Kel103StateCandidate candidate,
        string expectedMode,
        bool modeRestoration)
    {
        string unit = Unit(candidate);
        IEnumerable<string> responses = InitialResponses(unit)
            .Concat(PostSetterResponses(candidate, expectedMode, ChangedTarget(candidate)))
            .Concat(PostSetterResponses(candidate, expectedMode, OriginalTarget(candidate)));
        if (modeRestoration)
        {
            responses = responses.Concat(FinalResponses());
        }

        return new ScriptedSerialByteStream(responses.ToArray());
    }

    private static IEnumerable<string> InitialResponses(string selectedUnit) =>
    [
        "RND 320-KEL103 V3.30 SN:REDACTED\n",
        "OFF\n", "CC\n",
        "1.0000V\n", "2.0000A\n", "3.0000OHM\n", "4.0000W\n",
        "0.0000" + selectedUnit + "\n",
        "10.0000" + selectedUnit + "\n"
    ];

    private static IEnumerable<string> PostSetterResponses(
        Kel103StateCandidate candidate,
        string mode,
        string selectedResponse)
    {
        string[] targets =
        [
            "1.0000V\n", "2.0000A\n", "3.0000OHM\n", "4.0000W\n"
        ];
        targets[(int)candidate - 2] = selectedResponse + "\n";

        return new[] { "OFF\n", mode + "\n" }.Concat(targets);
    }

    private static IEnumerable<string> FinalResponses() =>
    [
        "OFF\n", "CC\n",
        "1.0000V\n", "2.0000A\n", "3.0000OHM\n", "4.0000W\n"
    ];

    private static string OriginalTarget(Kel103StateCandidate candidate) =>
        candidate switch
        {
            Kel103StateCandidate.TargetVoltage => "1.0000V",
            Kel103StateCandidate.TargetCurrent => "2.0000A",
            Kel103StateCandidate.TargetResistance => "3.0000OHM",
            Kel103StateCandidate.TargetPower => "4.0000W",
            _ => throw new ArgumentOutOfRangeException(nameof(candidate))
        };

    private static string ChangedTarget(Kel103StateCandidate candidate) =>
        candidate switch
        {
            Kel103StateCandidate.TargetVoltage => "1.0001V",
            Kel103StateCandidate.TargetCurrent => "2.0001A",
            Kel103StateCandidate.TargetResistance => "3.0001OHM",
            Kel103StateCandidate.TargetPower => "4.0001W",
            _ => throw new ArgumentOutOfRangeException(nameof(candidate))
        };

    private static string Unit(Kel103StateCandidate candidate) =>
        candidate.ToUnitSymbol()
        ?? throw new InvalidOperationException("A setpoint must define a unit.");

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
