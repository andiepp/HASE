using System.Diagnostics;
using Hase.Scpi;
using Hase.Scpi.Kel103;
using Hase.Transport.Serial;

namespace Hase.ProtocolExplorer.ScpiCharacterization;

internal sealed class Kel103InputControlCharacterizer
{
    private static readonly TimeSpan TotalResponseTimeout = TimeSpan.FromSeconds(3);
    private const int MaximumResponseBytes = 512;
    private const string InputOnCommand = ":INPut ON";
    private const string InputOffCommand = ":INPut OFF";

    private readonly ISerialByteStreamFactory serialByteStreamFactory;

    public Kel103InputControlCharacterizer(ISerialByteStreamFactory serialByteStreamFactory)
    {
        this.serialByteStreamFactory = serialByteStreamFactory
            ?? throw new ArgumentNullException(nameof(serialByteStreamFactory));
    }

    public async Task<Kel103InputControlCharacterizationResult> CharacterizeAsync(
        SerialTransportOptions transportOptions,
        bool activationConfirmed,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transportOptions);
        if (!activationConfirmed)
        {
            throw new ArgumentException(
                "KEL-103 input activation requires explicit Boolean confirmation.",
                nameof(activationConfirmed));
        }

        var byteStreamFactory = new Kel103SerialScpiByteStreamFactory(serialByteStreamFactory);
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

        await RequireInputAsync(
            session, "Off",
            "Initial input verification failed. No activation command was transmitted.",
            cancellationToken).ConfigureAwait(false);
        Kel103InputControlSnapshot original = await ReadSnapshotAsync(
            session,
            "Initial operating-state synchronization failed. No activation command was transmitted.",
            cancellationToken).ConfigureAwait(false);
        RequireCc(original,
            "Initial CC verification failed. No activation command was transmitted.");

        stopwatch.Restart();
        try
        {
            await session.SendCommandAsync(InputOnCommand, cancellationToken).ConfigureAwait(false);
        }
        catch (ScpiCommandTransmissionException exception)
        {
            throw new InvalidOperationException(
                "Activation command transmission is uncertain. No deactivation command was transmitted. Physically verify the input and restore CC/OFF.",
                exception);
        }

        await RequireInputAsync(
            session, "On",
            "Activation readback failed after one activation command transmission. No deactivation command was transmitted because activation was not authoritatively confirmed. Physically verify the input and restore CC/OFF.",
            cancellationToken).ConfigureAwait(false);
        stopwatch.Stop();
        TimeSpan activationDuration = stopwatch.Elapsed;

        Exception? activatedStateFailure = null;
        try
        {
            Kel103InputControlSnapshot activated = await ReadSnapshotAsync(
                session,
                "Activated-state verification failed after authoritative ON confirmation.",
                cancellationToken).ConfigureAwait(false);
            if (activated != original)
            {
                activatedStateFailure = new InvalidDataException(
                    "Activated-state comparison failed after authoritative ON confirmation. Values are not disclosed.");
            }
        }
        catch (Exception exception)
        {
            activatedStateFailure = exception;
        }

        stopwatch.Restart();
        try
        {
            await session.SendCommandAsync(InputOffCommand, cancellationToken).ConfigureAwait(false);
        }
        catch (ScpiCommandTransmissionException exception)
        {
            throw new InvalidOperationException(
                "Deactivation command transmission is uncertain after authoritative ON confirmation. Physically verify the input and restore CC/OFF.",
                exception);
        }

        await RequireInputAsync(
            session, "Off",
            "Deactivation readback failed after one activation and one deactivation command transmission. Physically verify the input and restore CC/OFF.",
            cancellationToken).ConfigureAwait(false);
        Kel103InputControlSnapshot final = await ReadSnapshotAsync(
            session,
            "Final operating-state verification failed after confirmed deactivation. Physically verify the input and restore CC/OFF.",
            cancellationToken).ConfigureAwait(false);
        stopwatch.Stop();

        RequireCc(final,
            "Final CC verification failed after confirmed deactivation. Physically verify the mode with input OFF.");
        if (final != original)
        {
            throw new InvalidDataException(
                "Final operating-state comparison failed after confirmed deactivation. Values are not disclosed. Physically verify the input, mode, and setpoints.");
        }

        if (activatedStateFailure is not null)
        {
            throw new InvalidOperationException(
                "Activated-state verification failed; input was subsequently authoritatively deactivated and final CC/OFF state was confirmed.",
                activatedStateFailure);
        }

        return new Kel103InputControlCharacterizationResult(
            identity, identityDuration, activationDuration, stopwatch.Elapsed);
    }

    private static async Task RequireInputAsync(
        IScpiTextSession session,
        string expected,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        string state;
        try
        {
            string response = await session.QueryAsync(
                Kel103StateCandidate.InputState.ToQueryText(), cancellationToken).ConfigureAwait(false);
            state = Kel103StateResponseParser.Parse(response, Kel103StateCandidate.InputState);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(failureMessage, exception);
        }

        if (!string.Equals(state, expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(failureMessage);
        }
    }

    private static async Task<Kel103InputControlSnapshot> ReadSnapshotAsync(
        IScpiTextSession session,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            return new Kel103InputControlSnapshot(
                await ReadAsync(session, Kel103StateCandidate.Mode, cancellationToken).ConfigureAwait(false),
                await ReadAsync(session, Kel103StateCandidate.TargetVoltage, cancellationToken).ConfigureAwait(false),
                await ReadAsync(session, Kel103StateCandidate.TargetCurrent, cancellationToken).ConfigureAwait(false),
                await ReadAsync(session, Kel103StateCandidate.TargetResistance, cancellationToken).ConfigureAwait(false),
                await ReadAsync(session, Kel103StateCandidate.TargetPower, cancellationToken).ConfigureAwait(false));
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(failureMessage, exception);
        }
    }

    private static async Task<string> ReadAsync(
        IScpiTextSession session,
        Kel103StateCandidate candidate,
        CancellationToken cancellationToken)
    {
        string response = await session.QueryAsync(candidate.ToQueryText(), cancellationToken)
            .ConfigureAwait(false);
        return Kel103StateResponseParser.Parse(response, candidate);
    }

    private static void RequireCc(Kel103InputControlSnapshot snapshot, string failureMessage)
    {
        if (!string.Equals(snapshot.Mode, "CC", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(failureMessage);
        }
    }
}
