using System.Diagnostics;
using Hase.Scpi;
using Hase.Scpi.Kel103;
using Hase.Transport.Serial;

namespace Hase.ProtocolExplorer.ScpiCharacterization;

internal sealed class Kel103ModeSelectionCharacterizer
{
    private static readonly TimeSpan TotalResponseTimeout = TimeSpan.FromSeconds(3);
    private const int MaximumResponseBytes = 512;

    private readonly ISerialByteStreamFactory serialByteStreamFactory;

    public Kel103ModeSelectionCharacterizer(
        ISerialByteStreamFactory serialByteStreamFactory)
    {
        this.serialByteStreamFactory = serialByteStreamFactory
            ?? throw new ArgumentNullException(nameof(serialByteStreamFactory));
    }

    public async Task<Kel103ModeSelectionCharacterizationResult> CharacterizeAsync(
        SerialTransportOptions transportOptions,
        Kel103ModeSelection destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transportOptions);
        EnsureDestination(destination);

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

        await RequireInputOffAsync(
                session,
                "Initial input verification failed. No mode-selection command was transmitted.",
                cancellationToken)
            .ConfigureAwait(false);
        await RequireModeAsync(
                session,
                Kel103ModeSelection.ConstantCurrent,
                "Initial CC verification failed. No mode-selection command was transmitted.",
                cancellationToken)
            .ConfigureAwait(false);
        Kel103ModeSelectionSnapshot originalTargets = await ReadTargetsAsync(
            session,
            "Initial setpoint synchronization failed. No mode-selection command was transmitted.",
            cancellationToken).ConfigureAwait(false);

        stopwatch.Restart();
        try
        {
            await session.SendCommandAsync(destination.ToCommandText(), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ScpiCommandTransmissionException exception)
        {
            throw new InvalidOperationException(
                "Destination command transmission is uncertain. No restoration command was transmitted. Physically verify input and mode, then restore CC with input OFF.",
                exception);
        }

        await RequireInputOffAsync(
                session,
                "Destination input verification failed after one destination command transmission. No restoration command was transmitted. Physically verify input and mode, then restore CC with input OFF.",
                cancellationToken)
            .ConfigureAwait(false);
        await RequireModeAsync(
                session,
                destination,
                "Destination mode verification failed after one destination command transmission. No restoration command was transmitted. Physically verify input and mode, then restore CC with input OFF.",
                cancellationToken)
            .ConfigureAwait(false);
        stopwatch.Stop();
        TimeSpan destinationDuration = stopwatch.Elapsed;

        stopwatch.Restart();
        try
        {
            await session.SendCommandAsync(
                    Kel103ModeSelection.ConstantCurrent.ToCommandText(),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ScpiCommandTransmissionException exception)
        {
            throw new InvalidOperationException(
                "Restoration command transmission is uncertain after one confirmed destination command transmission. Physically verify input and mode, then restore CC with input OFF.",
                exception);
        }

        await RequireInputOffAsync(
                session,
                "Restoration input verification failed after one destination and one restoration command transmission. Physically verify input and mode, then restore CC with input OFF.",
                cancellationToken)
            .ConfigureAwait(false);
        await RequireModeAsync(
                session,
                Kel103ModeSelection.ConstantCurrent,
                "Restoration CC verification failed after one destination and one restoration command transmission. Physically verify input and mode, then restore CC with input OFF.",
                cancellationToken)
            .ConfigureAwait(false);
        Kel103ModeSelectionSnapshot restoredTargets = await ReadTargetsAsync(
            session,
            "Final setpoint verification failed after one destination and one restoration command transmission. Physically verify input and mode, then restore CC with input OFF.",
            cancellationToken).ConfigureAwait(false);
        stopwatch.Stop();

        if (restoredTargets != originalTargets)
        {
            throw new InvalidDataException(
                "Final setpoint comparison failed after one destination and one restoration command transmission. Values are not disclosed. Physically verify input and mode, then restore CC with input OFF.");
        }

        return new Kel103ModeSelectionCharacterizationResult(
            identity,
            destination,
            identityDuration,
            destinationDuration,
            stopwatch.Elapsed);
    }

    private static void EnsureDestination(Kel103ModeSelection destination)
    {
        if (destination is Kel103ModeSelection.ConstantCurrent
            || !Enum.IsDefined(destination))
        {
            throw new ArgumentOutOfRangeException(
                nameof(destination),
                "The characterization destination must be CV, CR, CW, or SHORT.");
        }
    }

    private static async Task RequireInputOffAsync(
        IScpiTextSession session,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        string state;
        try
        {
            string response = await session
                .QueryAsync(Kel103StateCandidate.InputState.ToQueryText(), cancellationToken)
                .ConfigureAwait(false);
            state = Kel103StateResponseParser.Parse(
                response,
                Kel103StateCandidate.InputState);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(failureMessage, exception);
        }

        if (!string.Equals(state, "Off", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(failureMessage);
        }
    }

    private static async Task RequireModeAsync(
        IScpiTextSession session,
        Kel103ModeSelection expected,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        string mode;
        try
        {
            string response = await session
                .QueryAsync(Kel103StateCandidate.Mode.ToQueryText(), cancellationToken)
                .ConfigureAwait(false);
            mode = Kel103StateResponseParser.Parse(
                response,
                Kel103StateCandidate.Mode);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(failureMessage, exception);
        }

        if (!string.Equals(mode, expected.ToReadbackToken(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(failureMessage);
        }
    }

    private static async Task<Kel103ModeSelectionSnapshot> ReadTargetsAsync(
        IScpiTextSession session,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            return new Kel103ModeSelectionSnapshot(
                await ReadTargetAsync(session, Kel103StateCandidate.TargetVoltage, cancellationToken)
                    .ConfigureAwait(false),
                await ReadTargetAsync(session, Kel103StateCandidate.TargetCurrent, cancellationToken)
                    .ConfigureAwait(false),
                await ReadTargetAsync(session, Kel103StateCandidate.TargetResistance, cancellationToken)
                    .ConfigureAwait(false),
                await ReadTargetAsync(session, Kel103StateCandidate.TargetPower, cancellationToken)
                    .ConfigureAwait(false));
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(failureMessage, exception);
        }
    }

    private static async Task<string> ReadTargetAsync(
        IScpiTextSession session,
        Kel103StateCandidate candidate,
        CancellationToken cancellationToken)
    {
        string response = await session
            .QueryAsync(candidate.ToQueryText(), cancellationToken)
            .ConfigureAwait(false);
        return Kel103StateResponseParser.Parse(response, candidate);
    }
}
