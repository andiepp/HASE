using System.Text.Json;

namespace Hase.Runtime.Remote.Grpc.Hosting;

/// <summary>
/// Shared bounded reading and document-kind probing for external runtime-host
/// client configuration files.
/// </summary>
public static class RuntimeHostClientConfigurationDocument
{
    private const int ProbeMaximumDocumentByteCount =
        64 * 1024;

    /// <summary>
    /// Reads one bounded configuration document from a fully qualified path.
    /// </summary>
    public static async Task<byte[]> ReadBoundedAsync(
        string filePath,
        int maximumByteCount,
        string role,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            filePath);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            maximumByteCount);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            role);

        if (string.IsNullOrWhiteSpace(
                filePath))
        {
            throw new ArgumentException(
                $"The {role} file path must not be empty or whitespace.",
                nameof(filePath));
        }

        if (!Path.IsPathFullyQualified(
                filePath))
        {
            throw new ArgumentException(
                $"The {role} file path must be fully qualified.",
                nameof(filePath));
        }

        cancellationToken.ThrowIfCancellationRequested();

        await using FileStream stream =
            new(
                Path.GetFullPath(
                    filePath),
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.Asynchronous
                | FileOptions.SequentialScan);

        byte[] document =
            new byte[
                maximumByteCount
                + 1];
        int documentLength =
            0;

        while (documentLength < document.Length)
        {
            int readLength =
                await stream.ReadAsync(
                        document.AsMemory(
                            documentLength),
                        cancellationToken)
                    .ConfigureAwait(
                        false);

            if (readLength == 0)
            {
                break;
            }

            documentLength +=
                readLength;
        }

        if (documentLength > maximumByteCount)
        {
            throw new InvalidDataException(
                $"The {role} document exceeds the supported size.");
        }

        return document[..documentLength];
    }

    /// <summary>
    /// Removes one leading UTF-8 byte-order mark when present.
    /// </summary>
    public static ReadOnlySpan<byte> StripByteOrderMark(
        ReadOnlySpan<byte> document)
    {
        return document.Length >= 3
            && document[0] == 0xEF
            && document[1] == 0xBB
            && document[2] == 0xBF
                ? document[3..]
                : document;
    }

    /// <summary>
    /// Determines whether the referenced configuration document declares the
    /// explicit development-loopback profile kind. A document that is not a
    /// JSON object, or that carries no profile kind, is not a development
    /// document; its validation stays with its own strict loader.
    /// </summary>
    public static async Task<bool> IsDevelopmentLoopbackAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        byte[] document =
            await ReadBoundedAsync(
                    filePath,
                    ProbeMaximumDocumentByteCount,
                    "runtime-host client",
                    cancellationToken)
                .ConfigureAwait(
                    false);

        try
        {
            using var parsedDocument =
                JsonDocument.Parse(
                    StripByteOrderMark(
                            document)
                        .ToArray());

            return parsedDocument.RootElement.ValueKind
                == JsonValueKind.Object
                && parsedDocument.RootElement.TryGetProperty(
                    "profileKind",
                    out JsonElement profileKind)
                && profileKind.ValueKind == JsonValueKind.String
                && profileKind.GetString()
                    == RuntimeHostDevelopmentLoopbackClientOptionsFile
                        .RequiredProfileKind;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
