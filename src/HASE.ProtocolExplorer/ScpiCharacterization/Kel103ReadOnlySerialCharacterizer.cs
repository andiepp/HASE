using System.Diagnostics;
using System.Text;
using Hase.Transport.Serial;

namespace Hase.ProtocolExplorer.ScpiCharacterization;

internal sealed class Kel103ReadOnlySerialCharacterizer
{
    private const string IdentificationQuery =
        "*IDN?";

    private readonly ISerialByteStreamFactory _byteStreamFactory;

    public Kel103ReadOnlySerialCharacterizer(
        ISerialByteStreamFactory byteStreamFactory)
    {
        _byteStreamFactory =
            byteStreamFactory
            ?? throw new ArgumentNullException(
                nameof(byteStreamFactory));
    }

    public async Task<Kel103CharacterizationResult> CharacterizeAsync(
        SerialTransportOptions transportOptions,
        Kel103CharacterizationOptions characterizationOptions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            transportOptions);

        ArgumentNullException.ThrowIfNull(
            characterizationOptions);

        cancellationToken.ThrowIfCancellationRequested();

        await using ISerialByteStream byteStream =
            await _byteStreamFactory.OpenAsync(
                transportOptions,
                cancellationToken);

        byte[] request =
            CreateRequest(
                characterizationOptions.CommandTerminator);

        await byteStream.WriteAsync(
            request,
            cancellationToken);

        var stopwatch =
            Stopwatch.StartNew();

        BoundedResponse boundedResponse =
            await ReadBoundedResponseAsync(
                byteStream,
                characterizationOptions,
                stopwatch,
                cancellationToken);

        stopwatch.Stop();

        return CreateResult(
            boundedResponse.Bytes,
            boundedResponse.TimeToFirstByte,
            stopwatch.Elapsed);
    }

    internal static byte[] CreateRequest(
        Kel103CommandTerminator commandTerminator)
    {
        byte[] commandBytes =
            Encoding.ASCII.GetBytes(
                IdentificationQuery);

        byte[] terminatorBytes =
            commandTerminator.ToBytes();

        byte[] request =
            new byte[
                commandBytes.Length
                + terminatorBytes.Length];

        commandBytes.CopyTo(
            request,
            0);

        terminatorBytes.CopyTo(
            request,
            commandBytes.Length);

        return request;
    }

    private static async Task<BoundedResponse> ReadBoundedResponseAsync(
        ISerialByteStream byteStream,
        Kel103CharacterizationOptions options,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        byte[] response =
            new byte[
                options.MaximumResponseBytes];

        int responseLength =
            0;

        bool receivedAnyByte =
            false;

        TimeSpan timeToFirstByte =
            TimeSpan.Zero;

        while (true)
        {
            TimeSpan remainingTotalTimeout =
                options.TotalResponseTimeout
                - stopwatch.Elapsed;

            if (remainingTotalTimeout <= TimeSpan.Zero)
            {
                await byteStream.DisposeAsync();

                if (!receivedAnyByte)
                {
                    throw new TimeoutException(
                        "The KEL-103 did not return any identification bytes within the configured timeout.");
                }

                break;
            }

            TimeSpan currentReadTimeout =
                receivedAnyByte
                    ? Min(
                        remainingTotalTimeout,
                        options.PostFirstByteIdleInterval)
                    : remainingTotalTimeout;

            SerialReadAttempt readAttempt =
                await ReadWithExplicitTimeoutAsync(
                    byteStream,
                    response.AsMemory(
                        responseLength,
                        response.Length - responseLength),
                    currentReadTimeout,
                    cancellationToken);

            if (readAttempt.TimedOut)
            {
                await byteStream.DisposeAsync();

                if (!receivedAnyByte)
                {
                    throw new TimeoutException(
                        "The KEL-103 did not return any identification bytes within the configured timeout.");
                }

                break;
            }

            int bytesRead =
                readAttempt.BytesRead;

            if (bytesRead == 0)
            {
                break;
            }

            if (!receivedAnyByte)
            {
                receivedAnyByte =
                    true;

                timeToFirstByte =
                    stopwatch.Elapsed;
            }

            responseLength +=
                bytesRead;

            if (responseLength == response.Length)
            {
                throw new InvalidDataException(
                    "The KEL-103 identification response reached the configured maximum response size.");
            }
        }

        if (!receivedAnyByte)
        {
            throw new EndOfStreamException(
                "The KEL-103 serial stream ended before returning identification bytes.");
        }

        return new BoundedResponse(
            response[
                ..responseLength],
            timeToFirstByte);
    }

    private static async Task<SerialReadAttempt> ReadWithExplicitTimeoutAsync(
        ISerialByteStream byteStream,
        Memory<byte> buffer,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        Task<int> readTask =
            byteStream
                .ReadAsync(
                    buffer,
                    cancellationToken)
                .AsTask();

        Task timeoutTask =
            Task.Delay(
                timeout,
                cancellationToken);

        Task completedTask =
            await Task.WhenAny(
                readTask,
                timeoutTask);

        if (ReferenceEquals(
                completedTask,
                readTask))
        {
            return new SerialReadAttempt(
                TimedOut: false,
                BytesRead: await readTask);
        }

        cancellationToken.ThrowIfCancellationRequested();

        ObserveFault(
            readTask);

        return new SerialReadAttempt(
            TimedOut: true,
            BytesRead: 0);
    }

    private static void ObserveFault(
        Task task)
    {
        _ = task.ContinueWith(
            static completedTask =>
            {
                _ = completedTask.Exception;
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously
            | TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    private static TimeSpan Min(
        TimeSpan left,
        TimeSpan right)
    {
        return left <= right
            ? left
            : right;
    }

    private static Kel103CharacterizationResult CreateResult(
        byte[] response,
        TimeSpan timeToFirstByte,
        TimeSpan totalDuration)
    {
        ValidateAscii(
            response);

        Kel103ResponseTerminator responseTerminator =
            DetectResponseTerminator(
                response);

        string responseText =
            Encoding.ASCII.GetString(
                response);

        string normalizedText =
            responseText
                .Replace(
                    "\r\n",
                    "\n",
                    StringComparison.Ordinal)
                .Replace(
                    '\r',
                    '\n');

        string[] lines =
            normalizedText
                .Split(
                    '\n',
                    StringSplitOptions.RemoveEmptyEntries
                    | StringSplitOptions.TrimEntries);

        bool commandEchoDetected =
            lines.Length > 0
            && string.Equals(
                lines[0],
                IdentificationQuery,
                StringComparison.Ordinal);

        string? identityLine =
            lines.FirstOrDefault(line =>
                !string.Equals(
                    line,
                    IdentificationQuery,
                    StringComparison.Ordinal));

        if (string.IsNullOrWhiteSpace(
                identityLine))
        {
            throw new InvalidDataException(
                "The KEL-103 identification response did not contain a product identity line.");
        }

        string canonicalIdentity =
            new(
                identityLine
                    .Where(
                        char.IsLetterOrDigit)
                    .Select(
                        char.ToUpperInvariant)
                    .ToArray());

        bool identityVerified =
            canonicalIdentity.Contains(
                "KEL103",
                StringComparison.Ordinal);

        if (!identityVerified)
        {
            throw new InvalidDataException(
                "The identification response does not identify a KEL-103 instrument.");
        }

        string firmware =
            ExtractFirmware(
                identityLine);

        return new Kel103CharacterizationResult(
            response,
            responseTerminator,
            commandEchoDetected,
            timeToFirstByte,
            totalDuration,
            productIdentity: "KEL-103",
            firmware,
            identityVerified);
    }

    private static string ExtractFirmware(
        string identityLine)
    {
        string[] tokens =
            identityLine.Split(
                [' ', ',', ';'],
                StringSplitOptions.RemoveEmptyEntries
                | StringSplitOptions.TrimEntries);

        string? firmware =
            tokens.FirstOrDefault(token =>
                token.Length >= 2
                && (token[0] == 'V'
                    || token[0] == 'v')
                && token
                    .Skip(1)
                    .Any(
                        char.IsDigit));

        return firmware
            ?? "<unreported>";
    }

    private static Kel103ResponseTerminator DetectResponseTerminator(
        IReadOnlyList<byte> response)
    {
        if (response.Count >= 2
            && response[^2] == 0x0D
            && response[^1] == 0x0A)
        {
            return Kel103ResponseTerminator
                .CarriageReturnLineFeed;
        }

        if (response.Count >= 1
            && response[^1] == 0x0D)
        {
            return Kel103ResponseTerminator
                .CarriageReturn;
        }

        if (response.Count >= 1
            && response[^1] == 0x0A)
        {
            return Kel103ResponseTerminator
                .LineFeed;
        }

        return Kel103ResponseTerminator.None;
    }

    private static void ValidateAscii(
        IReadOnlyList<byte> response)
    {
        bool containsInvalidByte =
            response.Any(value =>
                value != 0x0A
                && value != 0x0D
                && (value < 0x20
                    || value > 0x7E));

        if (containsInvalidByte)
        {
            throw new InvalidDataException(
                "The KEL-103 identification response contains non-ASCII bytes.");
        }
    }

    private sealed record BoundedResponse(
        byte[] Bytes,
        TimeSpan TimeToFirstByte);

    private sealed record SerialReadAttempt(
        bool TimedOut,
        int BytesRead);
}
