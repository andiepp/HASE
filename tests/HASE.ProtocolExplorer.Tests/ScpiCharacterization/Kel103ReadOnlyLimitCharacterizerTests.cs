using Hase.ProtocolExplorer.ScpiCharacterization;
using Hase.Transport.Serial;
using Xunit;

namespace Hase.ProtocolExplorer.Tests.ScpiCharacterization;

public sealed class Kel103ReadOnlyLimitCharacterizerTests
{
    [Theory]
    [InlineData(2, 0, ":VOLT:LOW?\r", "1.2345V", "1.2345", "V")]
    [InlineData(2, 1, ":VOLT:UPP?\r", "9.8765V", "9.8765", "V")]
    [InlineData(3, 0, ":CURR:LOW?\r", "1.2345A", "1.2345", "A")]
    [InlineData(3, 1, ":CURR:UPP?\r", "9.8765A", "9.8765", "A")]
    [InlineData(4, 0, ":RES:LOW?\r", "1.2345OHM", "1.2345", "OHM")]
    [InlineData(4, 1, ":RES:UPP?\r", "9.8765OHM", "9.8765", "OHM")]
    [InlineData(5, 0, ":POW:LOW?\r", "1.2345W", "1.2345", "W")]
    [InlineData(5, 1, ":POW:UPP?\r", "9.8765W", "9.8765", "W")]
    public async Task CharacterizeAsync_VerifiesIdentityThenSendsOneSelectedLimitQuery(
        int candidateValue,
        int limitValue,
        string expectedQuery,
        string response,
        string expectedValue,
        string expectedUnit)
    {
        var stream = new ScriptedSerialByteStream(
            "RND 320-KEL103 V3.30 SN:REDACTED\n",
            response + "\n");
        var characterizer = new Kel103ReadOnlyLimitCharacterizer(
            new StubSerialByteStreamFactory(stream));

        Kel103LimitCharacterizationResult result = await characterizer
            .CharacterizeAsync(
                new SerialTransportOptions("TEST-TARGET", 115200),
                (Kel103StateCandidate)candidateValue,
                (Kel103SetpointLimit)limitValue);

        Assert.Equal(new[] { "*IDN?\r", expectedQuery }, stream.Writes);
        Assert.Equal("KEL-103", result.Identity.ProductIdentity);
        Assert.Equal(expectedValue, result.NormalizedValue);
        Assert.Equal(expectedUnit, result.UnitSymbol);
        Assert.Null(result.UnrecognizedResponse);
        Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task CharacterizeAsync_DoesNotSendLimitQueryAfterIdentityRejection()
    {
        var stream = new ScriptedSerialByteStream(
            "RND OTHER-MODEL V3.30 SN:REDACTED\n");
        var characterizer = new Kel103ReadOnlyLimitCharacterizer(
            new StubSerialByteStreamFactory(stream));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            characterizer.CharacterizeAsync(
                new SerialTransportOptions("TEST-TARGET", 115200),
                Kel103StateCandidate.TargetVoltage,
                Kel103SetpointLimit.Lower));

        Assert.Equal(new[] { "*IDN?\r" }, stream.Writes);
        Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task CharacterizeAsync_ObservesUnrecognizedLimitWithoutRawValue()
    {
        var stream = new ScriptedSerialByteStream(
            "RND 320-KEL103 V3.30 SN:REDACTED\n",
            "12.5000VDC\n");
        var characterizer = new Kel103ReadOnlyLimitCharacterizer(
            new StubSerialByteStreamFactory(stream));

        Kel103LimitCharacterizationResult result = await characterizer
            .CharacterizeAsync(
                new SerialTransportOptions("TEST-TARGET", 115200),
                Kel103StateCandidate.TargetVoltage,
                Kel103SetpointLimit.Upper);

        Assert.Null(result.NormalizedValue);
        Assert.Equal("VDC", result.UnrecognizedResponse?.Suffix);
        Assert.DoesNotContain(
            "12.5000",
            result.UnrecognizedResponse?.ToString(),
            StringComparison.Ordinal);
        Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task CharacterizeAsync_RejectsNonSetpointCandidateBeforeOpeningStream()
    {
        var factory = new CountingSerialByteStreamFactory();
        var characterizer = new Kel103ReadOnlyLimitCharacterizer(factory);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            characterizer.CharacterizeAsync(
                new SerialTransportOptions("TEST-TARGET", 115200),
                Kel103StateCandidate.Mode,
                Kel103SetpointLimit.Lower));

        Assert.Equal(0, factory.OpenCount);
    }

    [Fact]
    public async Task CharacterizeAsync_RejectsUnsupportedLimitBeforeOpeningStream()
    {
        var factory = new CountingSerialByteStreamFactory();
        var characterizer = new Kel103ReadOnlyLimitCharacterizer(factory);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            characterizer.CharacterizeAsync(
                new SerialTransportOptions("TEST-TARGET", 115200),
                Kel103StateCandidate.TargetVoltage,
                (Kel103SetpointLimit)99));

        Assert.Equal(0, factory.OpenCount);
    }

    [Fact]
    public void Constructor_RejectsNullFactory()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new Kel103ReadOnlyLimitCharacterizer(null!));
    }

    private sealed class StubSerialByteStreamFactory(ISerialByteStream stream)
        : ISerialByteStreamFactory
    {
        public ValueTask<ISerialByteStream> OpenAsync(
            SerialTransportOptions options,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(stream);
        }
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

        public bool Disposed { get; private set; }

        public ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            byte[] response = pendingResponses.Dequeue();
            response.AsSpan().CopyTo(buffer.Span);
            return ValueTask.FromResult(response.Length);
        }

        public ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Writes.Add(System.Text.Encoding.ASCII.GetString(buffer.Span));
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
