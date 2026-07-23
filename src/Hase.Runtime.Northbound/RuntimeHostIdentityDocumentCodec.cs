using System.Text;
using System.Text.Json;

namespace Hase.Runtime.Northbound;

/// <summary>
/// Serializes and parses the versioned runtime-host identity document.
/// </summary>
internal static class RuntimeHostIdentityDocumentCodec
{
    internal const int MaximumDocumentByteCount =
        4096;

    private const int CurrentFormatVersion =
        1;

    private const string FormatVersionPropertyName =
        "formatVersion";

    private const string RuntimeHostIdPropertyName =
        "runtimeHostId";

    private static readonly UTF8Encoding StrictUtf8Encoding =
        new(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true);

    /// <summary>
    /// Serializes one runtime-host identity as a version-1 UTF-8 document.
    /// </summary>
    public static byte[] Serialize(
        RuntimeHostId runtimeHostId)
    {
        ArgumentNullException.ThrowIfNull(
            runtimeHostId);

        using var stream =
            new MemoryStream();

        using (var writer =
            new Utf8JsonWriter(
                stream,
                new JsonWriterOptions
                {
                    Indented = true,
                }))
        {
            writer.WriteStartObject();

            writer.WriteNumber(
                FormatVersionPropertyName,
                CurrentFormatVersion);

            writer.WriteString(
                RuntimeHostIdPropertyName,
                runtimeHostId.Value);

            writer.WriteEndObject();
        }

        stream.WriteByte(
            (byte)'\n');

        return stream.ToArray();
    }

    /// <summary>
    /// Parses and strictly validates one version-1 UTF-8 identity document.
    /// </summary>
    public static RuntimeHostId Parse(
        ReadOnlyMemory<byte> document)
    {
        if (document.IsEmpty)
        {
            throw new InvalidDataException(
                "The runtime-host identity document is empty.");
        }

        if (document.Length
            > MaximumDocumentByteCount)
        {
            throw new InvalidDataException(
                "The runtime-host identity document exceeds the supported size.");
        }

        try
        {
            StrictUtf8Encoding.GetCharCount(
                document.Span);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException(
                "The runtime-host identity document is not valid UTF-8.",
                exception);
        }

        try
        {
            using JsonDocument jsonDocument =
                JsonDocument.Parse(
                    document,
                    new JsonDocumentOptions
                    {
                        AllowTrailingCommas = false,
                        CommentHandling =
                            JsonCommentHandling.Disallow,
                        MaxDepth = 4,
                    });

            JsonElement root =
                jsonDocument.RootElement;

            if (root.ValueKind
                != JsonValueKind.Object)
            {
                throw new InvalidDataException(
                    "The runtime-host identity document root must be an object.");
            }

            bool formatVersionSeen =
                false;

            bool runtimeHostIdSeen =
                false;

            string? runtimeHostIdValue =
                null;

            foreach (JsonProperty property
                in root.EnumerateObject())
            {
                switch (property.Name)
                {
                    case FormatVersionPropertyName:
                        if (formatVersionSeen)
                        {
                            throw new InvalidDataException(
                                "The runtime-host identity document contains a duplicate format version.");
                        }

                        formatVersionSeen =
                            true;

                        if (property.Value.ValueKind
                                != JsonValueKind.Number
                            || !property.Value.TryGetInt32(
                                out int formatVersion)
                            || formatVersion
                                != CurrentFormatVersion)
                        {
                            throw new InvalidDataException(
                                "The runtime-host identity document format version is not supported.");
                        }

                        break;

                    case RuntimeHostIdPropertyName:
                        if (runtimeHostIdSeen)
                        {
                            throw new InvalidDataException(
                                "The runtime-host identity document contains a duplicate runtime-host identity.");
                        }

                        runtimeHostIdSeen =
                            true;

                        if (property.Value.ValueKind
                            != JsonValueKind.String)
                        {
                            throw new InvalidDataException(
                                "The runtime-host identity document identity must be a string.");
                        }

                        runtimeHostIdValue =
                            property.Value.GetString();

                        break;

                    default:
                        throw new InvalidDataException(
                            $"The runtime-host identity document contains unknown property '{property.Name}'.");
                }
            }

            if (!formatVersionSeen)
            {
                throw new InvalidDataException(
                    "The runtime-host identity document format version is missing.");
            }

            if (!runtimeHostIdSeen)
            {
                throw new InvalidDataException(
                    "The runtime-host identity document identity is missing.");
            }

            try
            {
                return new RuntimeHostId(
                    runtimeHostIdValue!);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidDataException(
                    "The runtime-host identity document identity is invalid.",
                    exception);
            }
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "The runtime-host identity document is not valid UTF-8 JSON.",
                exception);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException(
                "The runtime-host identity document is not valid UTF-8 JSON.",
                exception);
        }
    }
}
