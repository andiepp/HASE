using System.Diagnostics;
using System.Globalization;
using Hase.Scpi;
using Hase.Scpi.Kel103;
using Hase.Transport.Serial;

namespace Hase.ProtocolExplorer.ScpiCharacterization;

internal sealed class Kel103SetpointChangeCharacterizer
{
    private static readonly TimeSpan TotalResponseTimeout = TimeSpan.FromSeconds(3);
    private const int MaximumResponseBytes = 512;

    private readonly ISerialByteStreamFactory serialByteStreamFactory;

    public Kel103SetpointChangeCharacterizer(
        ISerialByteStreamFactory serialByteStreamFactory)
    {
        this.serialByteStreamFactory = serialByteStreamFactory
            ?? throw new ArgumentNullException(nameof(serialByteStreamFactory));
    }

    public async Task<Kel103SetpointChangeCharacterizationResult> CharacterizeAsync(
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
                "Initial input verification failed. No changed-value setter was transmitted.",
                cancellationToken)
            .ConfigureAwait(false);
        await RequireModeAsync(
                session,
                Kel103ModeSelection.ConstantCurrent,
                "Initial CC verification failed. No changed-value setter was transmitted.",
                cancellationToken)
            .ConfigureAwait(false);
        Kel103ModeSelectionSnapshot originalTargets = await ReadTargetsAsync(
            session,
            "Initial setpoint synchronization failed. No changed-value setter was transmitted.",
            cancellationToken).ConfigureAwait(false);
        string lowerBound = await ReadLimitAsync(
            session,
            candidate,
            Kel103SetpointLimit.Lower,
            "Lower-bound synchronization failed. No changed-value setter was transmitted.",
            cancellationToken).ConfigureAwait(false);
        string upperBound = await ReadLimitAsync(
            session,
            candidate,
            Kel103SetpointLimit.Upper,
            "Upper-bound synchronization failed. No changed-value setter was transmitted.",
            cancellationToken).ConfigureAwait(false);

        var change = Kel103SetpointChangeCandidate.Create(
            SelectTarget(originalTargets, candidate),
            lowerBound,
            upperBound);
        Kel103ModeSelection expectedMode = ExpectedMode(candidate);

        stopwatch.Restart();
        await SendCommandAsync(
            session,
            candidate.ToSetterText(change.ChangedValue),
            "Changed-value setter transmission is uncertain. No restoration command was transmitted. Physically verify input, mode, and setpoints, then restore the original setpoint and CC with input OFF.",
            cancellationToken).ConfigureAwait(false);

        await RequireInputOffAsync(
                session,
                "Changed-value input verification failed after one setter transmission. No restoration command was transmitted. Physically verify input, mode, and setpoints.",
                cancellationToken)
            .ConfigureAwait(false);
        await RequireModeAsync(
                session,
                expectedMode,
                "Changed-value mode verification failed after one setter transmission. No restoration command was transmitted. Physically verify input, mode, and setpoints.",
                cancellationToken)
            .ConfigureAwait(false);
        Kel103ModeSelectionSnapshot changedTargets = await ReadTargetsAsync(
            session,
            "Changed-value setpoint verification failed after one setter transmission. No restoration command was transmitted. Physically verify input, mode, and setpoints.",
            cancellationToken).ConfigureAwait(false);
        stopwatch.Stop();
        TimeSpan changedValueVerificationDuration = stopwatch.Elapsed;

        bool changedValueMatched = NumericEquals(
            SelectTarget(changedTargets, candidate),
            change.ChangedValue);
        bool unrelatedTargetsUnchanged = UnrelatedTargetsEqual(
            originalTargets,
            changedTargets,
            candidate);

        stopwatch.Restart();
        await SendCommandAsync(
            session,
            candidate.ToSetterText(change.OriginalValue),
            "Original-setpoint restoration transmission is uncertain after one completed changed-value setter. Physically verify input, mode, and setpoints, then restore the original setpoint and CC with input OFF.",
            cancellationToken).ConfigureAwait(false);

        await RequireInputOffAsync(
                session,
                "Original-setpoint restoration input verification failed after one changed-value and one restoration setter transmission. Physically verify input, mode, and setpoints.",
                cancellationToken)
            .ConfigureAwait(false);
        await RequireModeAsync(
                session,
                expectedMode,
                "Original-setpoint restoration mode verification failed after one changed-value and one restoration setter transmission. Physically verify input, mode, and setpoints.",
                cancellationToken)
            .ConfigureAwait(false);
        Kel103ModeSelectionSnapshot restoredTargets = await ReadTargetsAsync(
            session,
            "Original-setpoint restoration verification failed after one changed-value and one restoration setter transmission. Physically verify input, mode, and setpoints.",
            cancellationToken).ConfigureAwait(false);
        stopwatch.Stop();
        TimeSpan setpointRestorationDuration = stopwatch.Elapsed;

        bool originalTargetsRestored = restoredTargets == originalTargets;

        bool modeRestorationCommandTransmitted = expectedMode is not Kel103ModeSelection.ConstantCurrent;
        TimeSpan modeRestorationDuration = TimeSpan.Zero;
        if (modeRestorationCommandTransmitted)
        {
            stopwatch.Restart();
            await SendCommandAsync(
                session,
                Kel103ModeSelection.ConstantCurrent.ToCommandText(),
                "CC restoration transmission is uncertain after changed-value and original-setpoint transmissions. Physically verify input, mode, and setpoints, then restore CC with input OFF.",
                cancellationToken).ConfigureAwait(false);
            await RequireInputOffAsync(
                    session,
                    "Final input verification failed after changed-value, original-setpoint, and CC restoration transmissions. Physically verify input, mode, and setpoints.",
                    cancellationToken)
                .ConfigureAwait(false);
            await RequireModeAsync(
                    session,
                    Kel103ModeSelection.ConstantCurrent,
                    "Final CC verification failed after changed-value, original-setpoint, and CC restoration transmissions. Physically verify input, mode, and setpoints.",
                    cancellationToken)
                .ConfigureAwait(false);
            Kel103ModeSelectionSnapshot finalTargets = await ReadTargetsAsync(
                session,
                "Final setpoint verification failed after changed-value, original-setpoint, and CC restoration transmissions. Physically verify input, mode, and setpoints.",
                cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            modeRestorationDuration = stopwatch.Elapsed;

            if (finalTargets != originalTargets)
            {
                throw new InvalidDataException(
                    "Final setpoint comparison failed after complete restoration. No values are disclosed. Physically verify input, mode, and setpoints.");
            }
        }

        if (!originalTargetsRestored)
        {
            throw new InvalidDataException(
                "Original-setpoint restoration comparison failed after planned restoration. CC state was restored when required, but setpoint restoration was not confirmed; no values are disclosed. Physically verify input, mode, and setpoints.");
        }

        if (!changedValueMatched)
        {
            throw new InvalidDataException(
                "Changed-value readback did not match the derived candidate. The original setpoint and CC state were restored and verified; no values are disclosed.");
        }

        if (!unrelatedTargetsUnchanged)
        {
            throw new InvalidDataException(
                "An unrelated setpoint changed during characterization. The original setpoints and CC state were restored and verified; no values are disclosed.");
        }

        return new Kel103SetpointChangeCharacterizationResult(
            identity,
            candidate,
            identityDuration,
            changedValueVerificationDuration,
            setpointRestorationDuration,
            modeRestorationCommandTransmitted,
            modeRestorationDuration);
    }

    private static async Task SendCommandAsync(
        IScpiTextSession session,
        string command,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            await session.SendCommandAsync(command, cancellationToken).ConfigureAwait(false);
        }
        catch (ScpiCommandTransmissionException exception)
        {
            throw new InvalidOperationException(failureMessage, exception);
        }
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

    private static Kel103ModeSelection ExpectedMode(Kel103StateCandidate candidate) =>
        candidate switch
        {
            Kel103StateCandidate.TargetVoltage => Kel103ModeSelection.ConstantVoltage,
            Kel103StateCandidate.TargetCurrent => Kel103ModeSelection.ConstantCurrent,
            Kel103StateCandidate.TargetResistance => Kel103ModeSelection.ConstantResistance,
            Kel103StateCandidate.TargetPower => Kel103ModeSelection.ConstantPower,
            _ => throw new ArgumentOutOfRangeException(nameof(candidate))
        };

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

    private static bool UnrelatedTargetsEqual(
        Kel103ModeSelectionSnapshot original,
        Kel103ModeSelectionSnapshot changed,
        Kel103StateCandidate selected) =>
        (selected is Kel103StateCandidate.TargetVoltage || original.Voltage == changed.Voltage)
        && (selected is Kel103StateCandidate.TargetCurrent || original.Current == changed.Current)
        && (selected is Kel103StateCandidate.TargetResistance || original.Resistance == changed.Resistance)
        && (selected is Kel103StateCandidate.TargetPower || original.Power == changed.Power);

    private static bool NumericEquals(string left, string right) =>
        decimal.Parse(left, CultureInfo.InvariantCulture)
        == decimal.Parse(right, CultureInfo.InvariantCulture);

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

    private static async Task RequireModeAsync(
        IScpiTextSession session,
        Kel103ModeSelection expected,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            string response = await session
                .QueryAsync(Kel103StateCandidate.Mode.ToQueryText(), cancellationToken)
                .ConfigureAwait(false);
            string mode = Kel103StateResponseParser.Parse(response, Kel103StateCandidate.Mode);
            if (!string.Equals(mode, expected.ToReadbackToken(), StringComparison.Ordinal))
            {
                throw new InvalidDataException();
            }
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

    private static async Task<string> ReadLimitAsync(
        IScpiTextSession session,
        Kel103StateCandidate candidate,
        Kel103SetpointLimit limit,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            string response = await session
                .QueryAsync(limit.ToQueryText(candidate), cancellationToken)
                .ConfigureAwait(false);
            return Kel103StateResponseParser.Parse(response, candidate);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(failureMessage, exception);
        }
    }
}
