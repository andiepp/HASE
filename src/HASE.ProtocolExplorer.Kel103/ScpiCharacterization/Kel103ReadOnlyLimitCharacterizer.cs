using System.Diagnostics;
using Hase.Scpi;
using Hase.Scpi.Kel103;
using Hase.Transport.Serial;

namespace Hase.ProtocolExplorer.ScpiCharacterization;

internal sealed class Kel103ReadOnlyLimitCharacterizer
{
    private static readonly TimeSpan TotalResponseTimeout = TimeSpan.FromSeconds(3);
    private const int MaximumResponseBytes = 512;

    private readonly ISerialByteStreamFactory serialByteStreamFactory;

    public Kel103ReadOnlyLimitCharacterizer(
        ISerialByteStreamFactory serialByteStreamFactory)
    {
        this.serialByteStreamFactory = serialByteStreamFactory
            ?? throw new ArgumentNullException(nameof(serialByteStreamFactory));
    }

    public async Task<Kel103LimitCharacterizationResult> CharacterizeAsync(
        SerialTransportOptions transportOptions,
        Kel103StateCandidate candidate,
        Kel103SetpointLimit limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transportOptions);
        Kel103SetpointLimitExtensions.EnsureSetpointCandidate(candidate);
        string queryText = limit.ToQueryText(candidate);

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
        string limitResponse = await session
            .QueryAsync(queryText, cancellationToken)
            .ConfigureAwait(false);
        stopwatch.Stop();

        string? normalizedValue = null;
        Kel103UnrecognizedStateResponseObservation? unrecognizedResponse = null;

        try
        {
            normalizedValue = Kel103StateResponseParser.Parse(
                limitResponse,
                candidate);
        }
        catch (InvalidDataException)
        {
            unrecognizedResponse = Kel103UnrecognizedStateResponseObservation.Create(
                limitResponse,
                candidate);
        }

        return new Kel103LimitCharacterizationResult(
            identity,
            candidate,
            limit,
            normalizedValue,
            candidate.ToUnitSymbol()
                ?? throw new InvalidOperationException("A setpoint candidate must define a unit."),
            unrecognizedResponse,
            identityDuration,
            stopwatch.Elapsed);
    }
}
