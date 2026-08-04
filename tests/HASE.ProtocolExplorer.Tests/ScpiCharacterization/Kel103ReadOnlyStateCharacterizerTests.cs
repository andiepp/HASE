using Hase.ProtocolExplorer.ScpiCharacterization;
using Hase.Transport.Serial;
using Xunit;

namespace Hase.ProtocolExplorer.Tests.ScpiCharacterization;

public sealed class Kel103ReadOnlyStateCharacterizerTests
{
    [Theory]
    [InlineData(0, ":FUNCtion?\r", "CC", "CC", null)]
    [InlineData(0, ":FUNCtion?\r", "SHORt", "SHORt", null)]
    [InlineData(1, ":INPut?\r", "OFF", "Off", null)]
    [InlineData(1, ":INPut?\r", "ON", "On", null)]
    [InlineData(2, ":VOLTage?\r", "12.500V", "12.500", "V")]
    [InlineData(3, ":CURRent?\r", "1.250A", "1.250", "A")]
    [InlineData(4, ":RESistance?\r", "100.00OHM", "100.00", "OHM")]
    [InlineData(5, ":POWer?\r", "25.000W", "25.000", "W")]
    public async Task CharacterizeAsync_VerifiesIdentityThenSendsOneSelectedQuery(
        int candidateValue,
        string expectedQuery,
        string response,
        string expectedValue,
        string? expectedUnit)
    {
        var stream = new ScriptedSerialByteStream(
            "RND 320-KEL103 V3.30 SN:REDACTED\n",
            response + "\n");
        var characterizer = new Kel103ReadOnlyStateCharacterizer(
            new StubSerialByteStreamFactory(stream));

        Kel103StateCharacterizationResult result = await characterizer
            .CharacterizeAsync(
                new SerialTransportOptions("TEST-TARGET", 115200),
                (Kel103StateCandidate)candidateValue);

        Assert.Equal(new[] { "*IDN?\r", expectedQuery }, stream.Writes);
        Assert.Equal("KEL-103", result.Identity.ProductIdentity);
        Assert.Equal("V3.30", result.Identity.FirmwareVersion);
        Assert.Equal(expectedValue, result.NormalizedValue);
        Assert.Equal(expectedUnit, result.UnitSymbol);
        Assert.Null(result.UnrecognizedResponse);
        Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task CharacterizeAsync_DoesNotSendStateQueryAfterIdentityRejection()
    {
        var stream = new ScriptedSerialByteStream(
            "RND OTHER-MODEL V3.30 SN:REDACTED\n");
        var characterizer = new Kel103ReadOnlyStateCharacterizer(
            new StubSerialByteStreamFactory(stream));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            characterizer.CharacterizeAsync(
                new SerialTransportOptions("TEST-TARGET", 115200),
                Kel103StateCandidate.Mode));

        Assert.Equal(new[] { "*IDN?\r" }, stream.Writes);
        Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task CharacterizeAsync_ObservesUnrecognizedStateAndDisposesStream()
    {
        var stream = new ScriptedSerialByteStream(
            "RND 320-KEL103 V3.30 SN:REDACTED\n",
            "UNSUPPORTED\n");
        var characterizer = new Kel103ReadOnlyStateCharacterizer(
            new StubSerialByteStreamFactory(stream));

        Kel103StateCharacterizationResult result = await characterizer
            .CharacterizeAsync(
                new SerialTransportOptions("TEST-TARGET", 115200),
                Kel103StateCandidate.Mode);

        Assert.Null(result.NormalizedValue);
        Assert.Equal("UNSUPPORTED", result.UnrecognizedResponse?.ObservedToken);
        Assert.True(stream.Disposed);
    }

    [Fact]
    public void Constructor_RejectsNullFactory()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new Kel103ReadOnlyStateCharacterizer(null!));
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
