using System.Globalization;
using Hase.Diagnostics.Export;

namespace Hase.Diagnostics.Offline;

/// <summary>
/// Analyzes exported HASE diagnostic documents without any connection to a
/// running application. Every read is the strict bounded export reader.
/// </summary>
public static class DiagnosticOfflineTool
{
    public const int ExitSuccess = 0;
    public const int ExitFailure = 1;
    public const int ExitUsage = 2;

    public static async Task<int> RunAsync(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (args.Count < 2)
        {
            return Usage(error);
        }

        string command = args[0];
        IReadOnlyList<string> options = args.Skip(2).ToArray();

        try
        {
            string filePath = Path.GetFullPath(args[1]);

            return command switch
            {
                "validate" when options.Count == 0 =>
                    await ValidateAsync(filePath, output),
                "summarize" when options.Count == 0 =>
                    await SummarizeAsync(filePath, output),
                "filter" =>
                    await FilterAsync(filePath, options, output, error),
                "show" =>
                    await ShowAsync(filePath, options, output, error),
                _ =>
                    Usage(error)
            };
        }
        catch (Exception exception) when (
            exception is InvalidDataException
                or IOException
                or UnauthorizedAccessException
                or ArgumentException)
        {
            await error.WriteLineAsync(
                "The document is not a valid HASE diagnostic export: "
                + exception.Message);
            return ExitFailure;
        }
    }

    private static async Task<int> ValidateAsync(
        string filePath,
        TextWriter output)
    {
        DiagnosticExportDocument document =
            await DiagnosticExportFile.ReadAsync(filePath);

        await output.WriteLineAsync("Document: valid HASE diagnostic export");
        await WriteEnvelopeAsync(document.Envelope, output);
        return ExitSuccess;
    }

    private static async Task<int> SummarizeAsync(
        string filePath,
        TextWriter output)
    {
        DiagnosticExportDocument document =
            await DiagnosticExportFile.ReadAsync(filePath);

        await WriteEnvelopeAsync(document.Envelope, output);

        if (document.Records.Count == 0)
        {
            await output.WriteLineAsync("The document contains no records.");
            return ExitSuccess;
        }

        await output.WriteLineAsync(
            "Sequence range: "
            + document.Records.Min(record => record.Sequence)
            + " to "
            + document.Records.Max(record => record.Sequence));
        await output.WriteLineAsync(
            "Timestamp range (UTC): "
            + FormatUtc(document.Records.Min(record => record.TimestampUtc))
            + " to "
            + FormatUtc(document.Records.Max(record => record.TimestampUtc)));

        await WriteGroupAsync(
            output, "Records by level", document.Records, record => record.Level);
        await WriteGroupAsync(
            output, "Records by category", document.Records, record => record.Category);
        await WriteGroupAsync(
            output, "Records by severity", document.Records, record => record.Severity);
        await WriteGroupAsync(
            output,
            "Records by outcome",
            document.Records,
            record => record.Outcome ?? "(none)");

        await WriteDistinctAsync(
            output,
            "Distinct endpoints",
            document.Records.Select(record => record.EndpointId));
        await WriteDistinctAsync(
            output,
            "Distinct instruments",
            document.Records.Select(record => record.InstrumentId));
        await WriteDistinctAsync(
            output,
            "Distinct runtime-host profiles",
            document.Records.Select(
                record => record.SessionContext?.ProfileId));

        return ExitSuccess;
    }

    private static async Task<int> FilterAsync(
        string filePath,
        IReadOnlyList<string> options,
        TextWriter output,
        TextWriter error)
    {
        string? outputPath = null;
        var predicates =
            new List<Func<ExportedDiagnosticRecord, bool>>();

        for (int index = 0; index < options.Count; index += 2)
        {
            if (index + 1 >= options.Count)
            {
                return Usage(error);
            }

            string option = options[index];
            string value = options[index + 1];

            switch (option)
            {
                case "--output":
                    outputPath = Path.GetFullPath(value);
                    break;
                case "--level":
                    predicates.Add(record =>
                        string.Equals(record.Level, value, StringComparison.Ordinal));
                    break;
                case "--category":
                    predicates.Add(record =>
                        string.Equals(record.Category, value, StringComparison.Ordinal));
                    break;
                case "--event":
                    predicates.Add(record =>
                        string.Equals(record.EventName, value, StringComparison.Ordinal));
                    break;
                case "--endpoint":
                    predicates.Add(record =>
                        string.Equals(record.EndpointId, value, StringComparison.Ordinal));
                    break;
                case "--outcome":
                    predicates.Add(record =>
                        string.Equals(record.Outcome, value, StringComparison.Ordinal));
                    break;
                default:
                    return Usage(error);
            }
        }

        if (outputPath is null)
        {
            return Usage(error);
        }

        DiagnosticExportDocument document =
            await DiagnosticExportFile.ReadAsync(filePath);

        ExportedDiagnosticRecord[] filtered = document.Records
            .Where(record => predicates.All(predicate => predicate(record)))
            .ToArray();

        DiagnosticExportDocument filteredDocument = new(
            new DiagnosticExportEnvelope(
                document.Envelope.Application,
                document.Envelope.CaptureLevel,
                document.Envelope.RuntimeHostId,
                document.Envelope.ExportedAtUtc,
                filtered.Length),
            filtered);

        try
        {
            await DiagnosticExportFile.WriteNewAsync(
                outputPath,
                filteredDocument);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            await error.WriteLineAsync(
                "The filter output could not be written: "
                + exception.Message);
            return ExitFailure;
        }

        await output.WriteLineAsync(
            "Filtered "
            + filtered.Length.ToString(CultureInfo.InvariantCulture)
            + " of "
            + document.Records.Count.ToString(CultureInfo.InvariantCulture)
            + " records to "
            + Path.GetFileName(outputPath)
            + ".");
        return ExitSuccess;
    }

    private static async Task<int> ShowAsync(
        string filePath,
        IReadOnlyList<string> options,
        TextWriter output,
        TextWriter error)
    {
        long? selectedSequence = null;

        if (options.Count == 2 && options[0] == "--sequence")
        {
            if (!long.TryParse(
                    options[1],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out long parsed))
            {
                return Usage(error);
            }
            selectedSequence = parsed;
        }
        else if (options.Count != 0)
        {
            return Usage(error);
        }

        DiagnosticExportDocument document =
            await DiagnosticExportFile.ReadAsync(filePath);

        IReadOnlyList<ExportedDiagnosticRecord> selected =
            selectedSequence is null
                ? document.Records
                : document.Records
                    .Where(record => record.Sequence == selectedSequence.Value)
                    .ToArray();

        if (selectedSequence is not null && selected.Count == 0)
        {
            await error.WriteLineAsync(
                "No record has sequence "
                + selectedSequence.Value.ToString(CultureInfo.InvariantCulture)
                + ".");
            return ExitFailure;
        }

        await WriteEnvelopeAsync(document.Envelope, output);

        foreach (ExportedDiagnosticRecord record in selected)
        {
            await WriteRecordAsync(record, output);
        }

        return ExitSuccess;
    }

    private static async Task WriteEnvelopeAsync(
        DiagnosticExportEnvelope envelope,
        TextWriter output)
    {
        await output.WriteLineAsync("Application: " + envelope.Application);
        await output.WriteLineAsync("Capture level: " + envelope.CaptureLevel);
        if (envelope.RuntimeHostId is not null)
        {
            await output.WriteLineAsync(
                "Runtime host: " + envelope.RuntimeHostId);
        }
        await output.WriteLineAsync(
            "Exported at (UTC): " + FormatUtc(envelope.ExportedAtUtc));
        await output.WriteLineAsync(
            "Records: "
            + envelope.RecordCount.ToString(CultureInfo.InvariantCulture));
    }

    private static async Task WriteRecordAsync(
        ExportedDiagnosticRecord record,
        TextWriter output)
    {
        await output.WriteLineAsync(
            "--- Record "
            + record.Sequence.ToString(CultureInfo.InvariantCulture)
            + " ---");
        await output.WriteLineAsync(
            "Timestamp (UTC): " + FormatUtc(record.TimestampUtc));
        await output.WriteLineAsync("Level: " + record.Level);
        await output.WriteLineAsync("Category: " + record.Category);
        await output.WriteLineAsync("Event: " + record.EventName);
        await output.WriteLineAsync("Severity: " + record.Severity);
        await WriteOptionalAsync(output, "Direction", record.Direction);
        await WriteOptionalAsync(
            output, "Operation", record.OperationId?.ToString());
        await WriteOptionalAsync(output, "Endpoint", record.EndpointId);
        await WriteOptionalAsync(
            output, "Generation", record.AttachmentGeneration?.ToString());
        await WriteOptionalAsync(output, "Instrument", record.InstrumentId);
        await WriteOptionalAsync(
            output, "Descriptor path", record.DescriptorPath);
        await WriteOptionalAsync(
            output, "Duration", record.Duration?.ToString());
        await WriteOptionalAsync(output, "Outcome", record.Outcome);

        foreach (KeyValuePair<string, string> item in record.Details
            .OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            await output.WriteLineAsync(
                "Detail " + item.Key + ": " + item.Value);
        }

        if (record.SessionContext is not null)
        {
            await output.WriteLineAsync(
                "Profile: "
                + record.SessionContext.ProfileId
                + " ("
                + record.SessionContext.ProfileDisplayName
                + ")");
            await output.WriteLineAsync(
                "Expected host: "
                + record.SessionContext.ExpectedRuntimeHostId);
            await WriteOptionalAsync(
                output,
                "Authoritative host",
                record.SessionContext.AuthoritativeRuntimeHostId);
        }

        if (record.ByteSnapshot is not null)
        {
            await output.WriteLineAsync(
                "Byte snapshot: "
                + record.ByteSnapshot.CapturedBytes.Count
                    .ToString(CultureInfo.InvariantCulture)
                + "/"
                + record.ByteSnapshot.OriginalByteCount
                    .ToString(CultureInfo.InvariantCulture)
                + " bytes"
                + (record.ByteSnapshot.IsTruncated
                    ? " (truncated)"
                    : string.Empty));
            await output.WriteLineAsync(
                "Bytes (hex): "
                + Convert.ToHexString(
                    record.ByteSnapshot.CapturedBytes.ToArray()));
        }
    }

    private static async Task WriteOptionalAsync(
        TextWriter output,
        string label,
        string? value)
    {
        if (value is not null)
        {
            await output.WriteLineAsync(label + ": " + value);
        }
    }

    private static async Task WriteGroupAsync(
        TextWriter output,
        string title,
        IReadOnlyList<ExportedDiagnosticRecord> records,
        Func<ExportedDiagnosticRecord, string> keySelector)
    {
        await output.WriteLineAsync(title + ":");

        foreach (IGrouping<string, ExportedDiagnosticRecord> group in records
            .GroupBy(keySelector)
            .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            await output.WriteLineAsync(
                "  "
                + group.Key
                + ": "
                + group.Count().ToString(CultureInfo.InvariantCulture));
        }
    }

    private static async Task WriteDistinctAsync(
        TextWriter output,
        string title,
        IEnumerable<string?> values)
    {
        string[] distinct = values
            .Where(value => value is not null)
            .Select(value => value!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        if (distinct.Length == 0)
        {
            return;
        }

        await output.WriteLineAsync(
            title + ": " + string.Join(", ", distinct));
    }

    private static string FormatUtc(DateTimeOffset value)
    {
        return value.UtcDateTime.ToString(
            "yyyy-MM-dd HH:mm:ss.fff",
            CultureInfo.InvariantCulture);
    }

    private static int Usage(TextWriter error)
    {
        error.WriteLine("Usage:");
        error.WriteLine("  Hase.Diagnostics.Offline validate <file>");
        error.WriteLine("  Hase.Diagnostics.Offline summarize <file>");
        error.WriteLine(
            "  Hase.Diagnostics.Offline filter <file> --output <newfile>"
            + " [--level L] [--category C] [--event E]"
            + " [--endpoint EP] [--outcome O]");
        error.WriteLine(
            "  Hase.Diagnostics.Offline show <file> [--sequence N]");
        error.WriteLine(
            "Filter values match the exported names exactly."
            + " The filter output is a new valid export document"
            + " and is never overwritten.");
        return ExitUsage;
    }
}
