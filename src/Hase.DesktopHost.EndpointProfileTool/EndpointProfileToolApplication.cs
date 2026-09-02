using System.Diagnostics;
using Hase.DesktopHost.Configuration;

namespace Hase.DesktopHost.EndpointProfileTool;

/// <summary>
/// Edits one Desktop Runtime Host endpoint composition.
/// </summary>
/// <remarks>
/// This tool edits the endpoint kinds that carry no device knowledge and
/// migrates the composition format. A composition root that ships
/// instruments contributes their operations and the process name its own
/// Runtime Host runs under.
/// </remarks>
public static class EndpointProfileToolApplication
{
    /// <summary>
    /// The Runtime Host this tool refuses to edit alongside.
    /// </summary>
    private const string PublishedHostProcessName = "Hase.DesktopHost.App";

    public static async Task<int> RunAsync(
        string[] args,
        IReadOnlyList<IEndpointProfileOperation>? additionalOperations = null,
        IReadOnlyList<string>? additionalHostProcessNames = null)
    {
        ArgumentNullException.ThrowIfNull(args);

        IReadOnlyList<IEndpointProfileOperation> operations =
            additionalOperations ?? [];

        if (args.Length >= 1 && args[0] == "preflight-open-format")
        {
            // Reads and reports; creates nothing, writes nothing, takes no
            // backup, so it runs whether or not a host is up.
            if (args.Length != 2)
            {
                return Usage(operations);
            }

            DesktopRuntimeHostEndpointCompositionFormatAssessment assessment;
            try
            {
                assessment = await DesktopRuntimeHostEndpointCompositionFormatPreflight
                    .InspectAsync(Path.GetFullPath(args[1]));
            }
            catch (Exception exception)
            {
                return Fail(exception.Message);
            }

            Console.WriteLine("Endpoint composition preflight succeeded: preflight-open-format");
            Console.WriteLine($"Format version: {assessment.FormatVersion}");
            Console.WriteLine($"Endpoint count: {assessment.EndpointCount}");
            Console.WriteLine($"Migration required: {assessment.MigrationRequired}");
            Console.WriteLine($"Expressible in closed format: {assessment.ExpressibleInLegacyFormat}");
            foreach (DesktopRuntimeHostEndpointCompositionFormatEndpoint endpoint
                in assessment.Endpoints)
            {
                Console.WriteLine(
                    $"Endpoint: {endpoint.ExpectedEndpointId} "
                    + $"provider={endpoint.ProviderId} settings={endpoint.SettingCount}");
            }

            return 0;
        }

        if (FindRunningHost(additionalHostProcessNames) is string runningHost)
        {
            return Fail(
                $"HASE Desktop Runtime Host is running ({runningHost}). "
                + "Close it before editing endpoint composition.");
        }

        if (args.Length >= 1 && args[0] == "migrate-open-format")
        {
            if (args.Length != 3 || args[2] != args[1])
            {
                return Usage(operations);
            }

            string formatProfilePath = Path.GetFullPath(args[1]);
            string formatBackupPath = formatProfilePath + "." + Stamp() + ".backup";
            try
            {
                await new DesktopRuntimeHostEndpointCompositionProfileEditor()
                    .MigrateToOpenFormatAsync(formatProfilePath, formatBackupPath);
            }
            catch (Exception exception)
            {
                return Fail(FormatMigrationFailure(exception));
            }

            Console.WriteLine("Endpoint profile operation succeeded: migrate-open-format");
            Console.WriteLine(
                "Format version: "
                + DesktopRuntimeHostEndpointCompositionProfile.OpenFormatVersion);
            Console.WriteLine($"Previous composition backup: {formatBackupPath}");
            return 0;
        }

        if (args.Length < 3)
        {
            return Usage(operations);
        }

        string operation = args[0];
        string profilePath = Path.GetFullPath(args[1]);
        string endpointId = args[2];
        string backupPath = profilePath + "." + Stamp() + ".backup";
        var editor = new DesktopRuntimeHostEndpointCompositionProfileEditor();
        IEndpointProfileOperation? contributed =
            operations.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, operation, StringComparison.Ordinal));
        EndpointProfileOperationResult result;

        try
        {
            switch (operation)
            {
                case "add-native" when args.Length == 5:
                    await editor.AddNativeAsync(profilePath, backupPath,
                        new DesktopRuntimeHostNativeNetworkEndpointProfile(
                            endpointId, args[3], int.Parse(args[4])));
                    result = new EndpointProfileOperationResult("NativeNetwork");
                    break;
                case "remove-native" when args.Length == 4 && args[3] == endpointId:
                    await editor.RemoveNativeAsync(profilePath, backupPath, endpointId);
                    result = new EndpointProfileOperationResult("NativeNetwork");
                    break;
                case "add-compact" when args.Length == 7:
                    await editor.AddCompactAsync(profilePath, backupPath,
                        new DesktopRuntimeHostCompactSerialEndpointProfile(
                            endpointId,
                            CompactSerialUsbIdentifierParser.ParseExactHex16(args[3], "USB vendor ID"),
                            CompactSerialUsbIdentifierParser.ParseExactHex16(args[4], "USB product ID"),
                            int.Parse(args[5]),
                            TimeSpan.FromMilliseconds(int.Parse(args[6]))));
                    result = new EndpointProfileOperationResult("CompactSerial");
                    break;
                case "remove-compact" when args.Length == 4 && args[3] == endpointId:
                    await editor.RemoveCompactAsync(profilePath, backupPath, endpointId);
                    result = new EndpointProfileOperationResult("CompactSerial");
                    break;
                default:
                    if (contributed is null)
                    {
                        return Usage(operations);
                    }

                    EndpointProfileOperationResult? contributedResult =
                        await contributed.ExecuteAsync(
                            new EndpointProfileOperationContext(
                                args, editor, profilePath, backupPath, endpointId));
                    if (contributedResult is null)
                    {
                        return Usage(operations);
                    }

                    result = contributedResult;
                    break;
            }
        }
        catch (Exception exception)
        {
            return Fail(
                contributed?.DescribeFailure(exception)
                ?? exception.Message);
        }

        Console.WriteLine($"Endpoint profile operation succeeded: {operation}");
        Console.WriteLine($"Expected endpoint ID: {endpointId}");
        Console.WriteLine($"Endpoint kind: {result.EndpointKind}");
        foreach (string line in result.AdditionalReportLines ?? [])
        {
            Console.WriteLine(line);
        }

        Console.WriteLine(
            result.BackupRetained
                ? "Previous composition backup: Retained"
                : $"Previous composition backup: {backupPath}");
        return 0;
    }

    private static string Stamp() =>
        DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfffffff");

    /// <summary>
    /// Names the running Runtime Host, if any is running.
    /// </summary>
    /// <remarks>
    /// A composition root that ships its own Runtime Host names that
    /// process too, because a composition must not be edited underneath a
    /// host that is reading it, whichever application that host is.
    /// </remarks>
    private static string? FindRunningHost(
        IReadOnlyList<string>? additionalHostProcessNames)
    {
        foreach (string name in
            new[] { PublishedHostProcessName }
                .Concat(additionalHostProcessNames ?? []))
        {
            if (Process.GetProcessesByName(name).Length > 0)
            {
                return name;
            }
        }

        return null;
    }

    private static int Usage(
        IReadOnlyList<IEndpointProfileOperation> operations)
    {
        Console.Error.WriteLine("Usage:");
        Console.Error.WriteLine("  add-native <composition> <endpoint-id> <host> <port>");
        Console.Error.WriteLine("  remove-native <composition> <endpoint-id> <same-id-confirmation>");
        Console.Error.WriteLine("  add-compact <composition> <endpoint-id> <0xVID> <0xPID> <baud> <timeout-ms>");
        Console.Error.WriteLine("  remove-compact <composition> <endpoint-id> <same-id-confirmation>");
        Console.Error.WriteLine("  preflight-open-format <composition>");
        Console.Error.WriteLine("  migrate-open-format <composition> <same-composition-path-confirmation>");

        foreach (IEndpointProfileOperation operation in operations)
        {
            foreach (string line in operation.UsageLines)
            {
                Console.Error.WriteLine(line);
            }
        }

        return 2;
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 1;
    }

    private static string FormatMigrationFailure(Exception exception) => exception switch
    {
        InvalidOperationException =>
            "The endpoint composition is already in the provider-keyed format.",
        InvalidDataException => "The endpoint composition is not valid for migration.",
        IOException => "A retained backup already exists for this composition.",
        OperationCanceledException => "The composition format migration was cancelled.",
        _ => "The composition format migration failed. Inspect the active profile and retained backups before retrying."
    };
}
