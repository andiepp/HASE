namespace Hase.Runtime.Northbound;

/// <summary>
/// Provides validated access to one runtime-host identity document file.
/// </summary>
internal sealed class RuntimeHostIdentityFile
{
    /// <summary>
    /// Initializes access to one fully qualified identity-document path.
    /// </summary>
    public RuntimeHostIdentityFile(
        string filePath)
    {
        ArgumentNullException.ThrowIfNull(
            filePath);

        if (string.IsNullOrWhiteSpace(
                filePath))
        {
            throw new ArgumentException(
                "The runtime-host identity file path must not be empty or whitespace.",
                nameof(filePath));
        }

        if (!Path.IsPathFullyQualified(
                filePath))
        {
            throw new ArgumentException(
                "The runtime-host identity file path must be fully qualified.",
                nameof(filePath));
        }

        FilePath =
            Path.GetFullPath(
                filePath);
    }

    /// <summary>
    /// Gets the normalized, fully qualified identity-document path.
    /// </summary>
    public string FilePath
    {
        get;
    }

    /// <summary>
    /// Reads and validates the identity document, or returns no identity when
    /// the target is genuinely absent.
    /// </summary>
    public async Task<RuntimeHostId?> ReadAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        FileStream stream;

        try
        {
            stream =
                new FileStream(
                    FilePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 4096,
                    FileOptions.Asynchronous
                    | FileOptions.SequentialScan);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }

        await using (stream)
        {
            byte[] document =
                new byte[
                    RuntimeHostIdentityDocumentCodec.MaximumDocumentByteCount
                    + 1];

            int documentLength =
                0;

            while (documentLength
                < document.Length)
            {
                int readLength =
                    await stream
                        .ReadAsync(
                            document.AsMemory(
                                documentLength),
                            cancellationToken)
                        .ConfigureAwait(
                            false);

                if (readLength
                    == 0)
                {
                    break;
                }

                documentLength +=
                    readLength;
            }

            if (documentLength
                > RuntimeHostIdentityDocumentCodec.MaximumDocumentByteCount)
            {
                throw new InvalidDataException(
                    "The runtime-host identity document exceeds the supported size.");
            }

            return RuntimeHostIdentityDocumentCodec.Parse(
                document.AsMemory(
                    0,
                    documentLength));
        }
    }
}