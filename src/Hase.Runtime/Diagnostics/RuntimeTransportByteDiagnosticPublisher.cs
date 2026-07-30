using System.Globalization;

namespace Hase.Runtime.Diagnostics;

/// <summary>
/// Publishes bounded, locally enabled exact-byte transport diagnostics.
/// </summary>
public sealed class RuntimeTransportByteDiagnosticPublisher
{
    private readonly RuntimeDiagnosticPublisher diagnostics;
    private readonly string endpointId;
    private readonly string protocolFamily;

    public RuntimeTransportByteDiagnosticPublisher(
        RuntimeDiagnosticPublisher diagnostics,
        string endpointId,
        string protocolFamily)
    {
        this.diagnostics =
            diagnostics
            ?? throw new ArgumentNullException(
                nameof(diagnostics));

        this.endpointId =
            NormalizeRequired(
                endpointId,
                nameof(endpointId));

        this.protocolFamily =
            NormalizeRequired(
                protocolFamily,
                nameof(protocolFamily));
    }

    public void Publish(
        RuntimeDiagnosticDirection direction,
        string? correlationId,
        Func<ReadOnlyMemory<byte>> bytesFactory)
    {
        if (!Enum.IsDefined(
                direction))
        {
            throw new ArgumentOutOfRangeException(
                nameof(direction),
                direction,
                "Direction is not defined.");
        }

        ArgumentNullException.ThrowIfNull(
            bytesFactory);

        string normalizedCorrelationId =
            string.IsNullOrWhiteSpace(
                correlationId)
                ? "none"
                : correlationId.Trim();

        if (!diagnostics.IsEnabled(
                RuntimeDiagnosticLevel.Bytes))
        {
            return;
        }

        ReadOnlyMemory<byte> originalBytes =
            bytesFactory();

        int capturedByteCount =
            Math.Min(
                originalBytes.Length,
                RuntimeDiagnosticByteSnapshot
                    .MaximumCapturedByteCount);

        var snapshot =
            new RuntimeDiagnosticByteSnapshot(
                originalBytes.Length,
                originalBytes.Span[..capturedByteCount],
                isTruncated:
                    capturedByteCount < originalBytes.Length);

        diagnostics.Publish(
            new RuntimeDiagnosticEvent(
                RuntimeDiagnosticLevel.Bytes,
                RuntimeDiagnosticCategory.TransportBytes,
                direction ==
                RuntimeDiagnosticDirection.Outbound
                    ? "TransportBytesSent"
                    : "TransportBytesReceived",
                RuntimeDiagnosticSeverity.Trace,
                endpointId,
                direction: direction,
                details:
                    new Dictionary<string, string>
                    {
                        ["protocolFamily"] =
                            protocolFamily,
                        ["correlationId"] =
                            normalizedCorrelationId,
                        ["originalByteCount"] =
                            snapshot.OriginalByteCount.ToString(
                                CultureInfo.InvariantCulture),
                        ["capturedByteCount"] =
                            snapshot.CapturedByteCount.ToString(
                                CultureInfo.InvariantCulture),
                        ["isTruncated"] =
                            snapshot.IsTruncated.ToString(
                                CultureInfo.InvariantCulture)
                    },
                byteSnapshot:
                    snapshot));
    }

    private static string NormalizeRequired(
        string value,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            throw new ArgumentException(
                "Value must not be empty.",
                parameterName);
        }

        return value.Trim();
    }
}
