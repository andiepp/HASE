using System.Diagnostics;
using Hase.Scpi;
using Hase.Scpi.Kel103;
using Hase.Transport.Serial;

namespace Hase.ProtocolExplorer.ScpiCharacterization;

internal sealed class Kel103SetpointWriteCharacterizer
{
    private static readonly TimeSpan TotalResponseTimeout = TimeSpan.FromSeconds(3);
    private const int MaximumResponseBytes = 512;

    private readonly ISerialByteStreamFactory serialByteStreamFactory;

    public Kel103SetpointWriteCharacterizer(
        ISerialByteStreamFactory serialByteStreamFactory)
    {
        this.serialByteStreamFactory = serialByteStreamFactory
            ?? throw new ArgumentNullException(nameof(serialByteStreamFactory));
    }

    public async Task<Kel103SetpointWriteCharacterizationResult> CharacterizeAsync(
        SerialTransportOptions transportOptions,
        Kel103StateCandidate candidate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transportOptions);
        EnsureSetpointCandidate(candidate);

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
                "Initial input verification failed. No setpoint setter was transmitted.",
                cancellationToken)
            .ConfigureAwait(false);
        string originalMode = await ReadModeAsync(
            session,
            "Initial mode synchronization failed. No setpoint setter was transmitted.",
            cancellationToken).ConfigureAwait(false);
        if (!string.Equals(
                originalMode,
                Kel103ModeSelection.ConstantCurrent.ToReadbackToken(),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Initial CC verification failed. No setpoint setter was transmitted.");
        }
        Kel103ModeSelectionSnapshot originalTargets = await ReadTargetsAsync(
            session,
            "Initial setpoint synchronization failed. No setpoint setter was transmitted.",
            cancellationToken).ConfigureAwait(false);

        string originalValue = SelectTarget(originalTargets, candidate);
        string setterText = candidate.ToSetterText(originalValue);

        stopwatch.Restart();
        try
        {
            await session.SendCommandAsync(setterText, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ScpiCommandTransmissionException exception)
        {
            throw new InvalidOperationException(
                "Same-value setpoint setter transmission is uncertain. No additional command was transmitted. Physically verify input, mode, and setpoints.",
                exception);
        }

        await RequireInputOffAsync(
                session,
                "Post-setter input verification failed after one same-value setter transmission. No additional command was transmitted. Physically verify input, mode, and setpoints.",
                cancellationToken)
            .ConfigureAwait(false);
        string resultingMode = await ReadModeAsync(
            session,
            "Post-setter mode verification failed after one same-value setter transmission. No additional command was transmitted. Physically verify input, mode, and setpoints.",
            cancellationToken).ConfigureAwait(false);
        Kel103ModeSelectionSnapshot resultingTargets = await ReadTargetsAsync(
            session,
            "Post-setter setpoint verification failed after one same-value setter transmission. No additional command was transmitted. Physically verify input, mode, and setpoints.",
            cancellationToken).ConfigureAwait(false);
        stopwatch.Stop();
        TimeSpan setterVerificationDuration = stopwatch.Elapsed;

        Kel103ModeSelection expectedMode = ExpectedMode(candidate);
        if (!string.Equals(
                resultingMode,
                expectedMode.ToReadbackToken(),
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Post-setter expected-mode comparison failed after one same-value setter transmission. No additional command was transmitted and no values are disclosed. Physically verify input, mode, and setpoints.");
        }

        if (resultingTargets != originalTargets)
        {
            throw new InvalidDataException(
                "Post-setter setpoint comparison failed after one same-value setter transmission. No additional command was transmitted and no values are disclosed. Physically verify input, mode, and setpoints.");
        }

        bool restorationCommandTransmitted = expectedMode is not Kel103ModeSelection.ConstantCurrent;
        TimeSpan restorationVerificationDuration = TimeSpan.Zero;

        if (restorationCommandTransmitted)
        {
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
                    "CC restoration transmission is uncertain after one confirmed same-value setter transmission. Physically verify input, mode, and setpoints, then restore CC with input OFF.",
                    exception);
            }

            await RequireInputOffAsync(
                    session,
                    "Restoration input verification failed after one setter and one CC restoration transmission. Physically verify input, mode, and setpoints, then restore CC with input OFF.",
                    cancellationToken)
                .ConfigureAwait(false);
            string restoredMode = await ReadModeAsync(
                session,
                "Restoration mode verification failed after one setter and one CC restoration transmission. Physically verify input, mode, and setpoints, then restore CC with input OFF.",
                cancellationToken).ConfigureAwait(false);
            Kel103ModeSelectionSnapshot restoredTargets = await ReadTargetsAsync(
                session,
                "Restoration setpoint verification failed after one setter and one CC restoration transmission. Physically verify input, mode, and setpoints, then restore CC with input OFF.",
                cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            restorationVerificationDuration = stopwatch.Elapsed;

            if (!string.Equals(
                    restoredMode,
                    Kel103ModeSelection.ConstantCurrent.ToReadbackToken(),
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Restoration CC comparison failed after one setter and one restoration transmission. No values are disclosed. Physically verify input, mode, and setpoints, then restore CC with input OFF.");
            }

            if (restoredTargets != originalTargets)
            {
                throw new InvalidDataException(
                    "Restoration setpoint comparison failed after one setter and one restoration transmission. No values are disclosed. Physically verify input, mode, and setpoints, then restore CC with input OFF.");
            }
        }

        return new Kel103SetpointWriteCharacterizationResult(
            identity,
            candidate,
            identityDuration,
            setterVerificationDuration,
            restorationCommandTransmitted,
            restorationVerificationDuration);
    }

    private static void EnsureSetpointCandidate(Kel103StateCandidate candidate)
    {
        if (candidate is not (Kel103StateCandidate.TargetVoltage
            or Kel103StateCandidate.TargetCurrent
            or Kel103StateCandidate.TargetResistance
            or Kel103StateCandidate.TargetPower))
        {
            throw new ArgumentOutOfRangeException(
                nameof(candidate),
                "The characterization candidate must identify one supported setpoint.");
        }
    }

    private static string SelectTarget(
        Kel103ModeSelectionSnapshot snapshot,
        Kel103StateCandidate candidate) =>
        candidate switch
        {
            Kel103StateCandidate.TargetVoltage => snapshot.Voltage,
            Kel103StateCandidate.TargetCurrent => snapshot.Current,
            Kel103StateCandidate.TargetResistance => snapshot.Resistance,
            Kel103StateCandidate.TargetPower => snapshot.Power,
            _ => throw new ArgumentOutOfRangeException(nameof(candidate))
        };

    private static Kel103ModeSelection ExpectedMode(Kel103StateCandidate candidate) =>
        candidate switch
        {
            Kel103StateCandidate.TargetVoltage => Kel103ModeSelection.ConstantVoltage,
            Kel103StateCandidate.TargetCurrent => Kel103ModeSelection.ConstantCurrent,
            Kel103StateCandidate.TargetResistance => Kel103ModeSelection.ConstantResistance,
            Kel103StateCandidate.TargetPower => Kel103ModeSelection.ConstantPower,
            _ => throw new ArgumentOutOfRangeException(nameof(candidate))
        };

    private static async Task RequireInputOffAsync(
        IScpiTextSession session,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            string response = await session
                .QueryAsync(Kel103StateCandidate.InputState.ToQueryText(), cancellationToken)
                .ConfigureAwait(false);
            string state = Kel103StateResponseParser.Parse(
                response,
                Kel103StateCandidate.InputState);

            if (!string.Equals(state, "Off", StringComparison.Ordinal))
            {
                throw new InvalidDataException();
            }
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(failureMessage, exception);
        }
    }

    private static async Task<string> ReadModeAsync(
        IScpiTextSession session,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            string response = await session
                .QueryAsync(Kel103StateCandidate.Mode.ToQueryText(), cancellationToken)
                .ConfigureAwait(false);
            return Kel103StateResponseParser.Parse(response, Kel103StateCandidate.Mode);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(failureMessage, exception);
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
