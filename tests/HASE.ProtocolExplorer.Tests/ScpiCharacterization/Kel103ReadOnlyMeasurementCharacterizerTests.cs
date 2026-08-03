using Hase.ProtocolExplorer.ScpiCharacterization;
using Hase.Transport.Serial;
using Xunit;

namespace Hase.ProtocolExplorer.Tests.ScpiCharacterization;

public sealed class Kel103ReadOnlyMeasurementCharacterizerTests
{
    [Theory]
    [InlineData(0, ":MEASure:VOLTage?\r", "1.25V", "1.25", "V")]
    [InlineData(1, ":MEASure:CURRent?\r", "0.50A", "0.50", "A")]
    [InlineData(2, ":MEASure:POWer?\r", "0.625W", "0.625", "W")]
    public async Task CharacterizeAsync_VerifiesIdentityThenSendsOneSelectedQuery(
        int candidateValue,
        string expectedQuery,
        string response,
        string expectedValue,
        string expectedUnit)
    {
        var candidate = (Kel103MeasurementCandidate)candidateValue;
        var stream = new ScriptedSerialByteStream(
            "RND 320-KEL103 V3.30 SN:REDACTED\n",
            response + "\n");
        var characterizer = new Kel103ReadOnlyMeasurementCharacterizer(
            new StubSerialByteStreamFactory(stream));

        Kel103MeasurementCharacterizationResult result = await characterizer
            .CharacterizeAsync(
                new SerialTransportOptions("TEST-TARGET", 115200),
                candidate);

        Assert.Equal(new[] { "*IDN?\r", expectedQuery }, stream.Writes);
        Assert.Equal("KEL-103", result.Identity.ProductIdentity);
        Assert.Equal("V3.30", result.Identity.FirmwareVersion);
        Assert.Equal(decimal.Parse(expectedValue, System.Globalization.CultureInfo.InvariantCulture), result.Value);
        Assert.Equal(expectedUnit, result.UnitSymbol);
        Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task CharacterizeAsync_DoesNotSendMeasurementAfterIdentityRejection()
    {
        var stream = new ScriptedSerialByteStream(
            "RND OTHER-MODEL V3.30 SN:REDACTED\n");
        var characterizer = new Kel103ReadOnlyMeasurementCharacterizer(
            new StubSerialByteStreamFactory(stream));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            characterizer.CharacterizeAsync(
                new SerialTransportOptions("TEST-TARGET", 115200),
                Kel103MeasurementCandidate.Voltage));

        Assert.Equal(new[] { "*IDN?\r" }, stream.Writes);
        Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task CharacterizeAsync_RejectsMalformedMeasurementAndDisposesStream()
    {
        var stream = new ScriptedSerialByteStream(
            "RND 320-KEL103 V3.30 SN:REDACTED\n",
            "1.25A\n");
        var characterizer = new Kel103ReadOnlyMeasurementCharacterizer(
            new StubSerialByteStreamFactory(stream));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            characterizer.CharacterizeAsync(
                new SerialTransportOptions("TEST-TARGET", 115200),
                Kel103MeasurementCandidate.Voltage));

        Assert.True(stream.Disposed);
    }

    [Fact]
    public void Constructor_RejectsNullFactory()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new Kel103ReadOnlyMeasurementCharacterizer(null!));
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
