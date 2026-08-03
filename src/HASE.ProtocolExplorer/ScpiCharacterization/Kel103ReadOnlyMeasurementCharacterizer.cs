using System.Diagnostics;
using Hase.Scpi;
using Hase.Scpi.Kel103;
using Hase.Transport.Serial;

namespace Hase.ProtocolExplorer.ScpiCharacterization;

internal sealed class Kel103ReadOnlyMeasurementCharacterizer
{
    private static readonly TimeSpan TotalResponseTimeout = TimeSpan.FromSeconds(3);
    private const int MaximumResponseBytes = 512;

    private readonly ISerialByteStreamFactory serialByteStreamFactory;

    public Kel103ReadOnlyMeasurementCharacterizer(
        ISerialByteStreamFactory serialByteStreamFactory)
    {
        this.serialByteStreamFactory = serialByteStreamFactory
            ?? throw new ArgumentNullException(nameof(serialByteStreamFactory));
    }

    public async Task<Kel103MeasurementCharacterizationResult> CharacterizeAsync(
        SerialTransportOptions transportOptions,
        Kel103MeasurementCandidate candidate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transportOptions);

        var byteStreamFactory = new Kel103SerialScpiByteStreamFactory(
            serialByteStreamFactory);
        IScpiByteStream byteStream = await byteStreamFactory
            .OpenAsync(transportOptions, cancellationToken)
            .ConfigureAwait(false);

        var framing = new ScpiTextFramingOptions(
            ScpiCommandTerminator.CarriageReturn,
            ScpiResponseTerminator.LineFeed,
            TotalResponseTimeout,
            MaximumResponseBytes);

        await using var session = new ScpiTextSession(byteStream, framing);

        var stopwatch = Stopwatch.StartNew();
        string identityResponse = await session
            .QueryAsync(Kel103IdentityQuery.CommandText, cancellationToken)
            .ConfigureAwait(false);
        stopwatch.Stop();

        Kel103Identity identity = Kel103IdentityQuery.ParseResponse(identityResponse);
        TimeSpan identityDuration = stopwatch.Elapsed;

        stopwatch.Restart();
        string measurementResponse = await session
            .QueryAsync(candidate.ToQueryText(), cancellationToken)
            .ConfigureAwait(false);
        stopwatch.Stop();

        decimal value = Kel103MeasurementResponseParser.Parse(
            measurementResponse,
            candidate);

        return new Kel103MeasurementCharacterizationResult(
            identity,
            candidate,
            value,
            candidate.ToUnitSymbol(),
            identityDuration,
            stopwatch.Elapsed);
    }
}
