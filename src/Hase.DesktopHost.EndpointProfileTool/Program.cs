using System.Diagnostics;
using Hase.DesktopHost.Configuration;
using Hase.Scpi.Kel103;

if (Process.GetProcessesByName("Hase.DesktopHost.App").Length > 0)
    return Fail("HASE Desktop Runtime Host is running. Close it before editing endpoint composition.");
if (args.Length < 3) return Usage();

string operation = args[0];
string profilePath = Path.GetFullPath(args[1]);
string endpointId = args[2];
string backupPath = profilePath + "." + DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfffffff") + ".backup";
var editor = new DesktopRuntimeHostEndpointCompositionProfileEditor();
string endpointKind;
try
{
    switch (operation)
    {
        case "add-native" when args.Length == 5:
            await editor.AddNativeAsync(profilePath, backupPath,
                new DesktopRuntimeHostNativeNetworkEndpointProfile(
                    endpointId, args[3], int.Parse(args[4])));
            endpointKind = "NativeNetwork";
            break;
        case "remove-native" when args.Length == 4 && args[3] == endpointId:
            await editor.RemoveNativeAsync(profilePath, backupPath, endpointId);
            endpointKind = "NativeNetwork";
            break;
        case "add-compact" when args.Length == 7:
            await editor.AddCompactAsync(profilePath, backupPath,
                new DesktopRuntimeHostCompactSerialEndpointProfile(
                    endpointId,
                    CompactSerialUsbIdentifierParser.ParseExactHex16(args[3], "USB vendor ID"),
                    CompactSerialUsbIdentifierParser.ParseExactHex16(args[4], "USB product ID"),
                    int.Parse(args[5]),
                    TimeSpan.FromMilliseconds(int.Parse(args[6]))));
            endpointKind = "CompactSerial";
            break;
        case "remove-compact" when args.Length == 4 && args[3] == endpointId:
            await editor.RemoveCompactAsync(profilePath, backupPath, endpointId);
            endpointKind = "CompactSerial";
            break;
        case "add-kel103" when args.Length == 4:
            await editor.AddKel103Async(profilePath, backupPath,
                new DesktopRuntimeHostKel103SerialEndpointProfile(
                    endpointId,
                    Kel103ReadOnlyMeasurementDefinition.Reference.Id.Value,
                    Kel103ReadOnlyMeasurementDefinition.Reference.Version,
                    args[3],
                    DesktopRuntimeHostKel103SerialEndpointProfile.SupportedBaudRate));
            endpointKind = "Kel103Serial";
            break;
        case "remove-kel103" when args.Length == 4 && args[3] == endpointId:
            await editor.RemoveKel103Async(profilePath, backupPath, endpointId);
            endpointKind = "Kel103Serial";
            break;
        case "migrate-kel103-v4" when args.Length == 4 && args[3] == endpointId:
            await editor.MigrateKel103DefinitionAsync(
                profilePath,
                backupPath,
                endpointId,
                Kel103ReadOnlyMeasurementDefinition.Reference,
                Kel103ControlledSetpointDefinition.Reference);
            endpointKind = "Kel103Serial";
            break;
        case "migrate-kel103-v5" when args.Length == 4 && args[3] == endpointId:
            await editor.MigrateKel103DefinitionAsync(
                profilePath,
                backupPath,
                endpointId,
                Kel103ControlledSetpointDefinition.Reference,
                Kel103ControlledInputDefinition.Reference);
            endpointKind = "Kel103Serial";
            break;
        default: return Usage();
    }
}
catch (Exception exception)
{
    return Fail(
        IsKel103Migration(operation)
            ? MigrationFailure(
                exception,
                operation)
            : exception.Message);
}
Console.WriteLine($"Endpoint profile operation succeeded: {operation}");
Console.WriteLine($"Expected endpoint ID: {endpointId}");
Console.WriteLine($"Endpoint kind: {endpointKind}");
if (IsKel103Migration(operation))
{
    Console.WriteLine(
        operation == "migrate-kel103-v4"
            ? "Previous definition version: 2"
            : "Previous definition version: 4");
    Console.WriteLine(
        operation == "migrate-kel103-v4"
            ? "Current definition version: 4"
            : "Current definition version: 5");
    Console.WriteLine("Previous composition backup: Retained");
}
else
{
    Console.WriteLine($"Previous composition backup: {backupPath}");
}
return 0;

static int Usage()
{
    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  add-native <composition> <endpoint-id> <host> <port>");
    Console.Error.WriteLine("  remove-native <composition> <endpoint-id> <same-id-confirmation>");
    Console.Error.WriteLine("  add-compact <composition> <endpoint-id> <0xVID> <0xPID> <baud> <timeout-ms>");
    Console.Error.WriteLine("  remove-compact <composition> <endpoint-id> <same-id-confirmation>");
    Console.Error.WriteLine("  add-kel103 <composition> <endpoint-id> <serial-target>");
    Console.Error.WriteLine("  remove-kel103 <composition> <endpoint-id> <same-id-confirmation>");
    Console.Error.WriteLine("  migrate-kel103-v4 <composition> <endpoint-id> <same-endpoint-id-confirmation>");
    Console.Error.WriteLine("  migrate-kel103-v5 <composition> <endpoint-id> <same-endpoint-id-confirmation>");
    return 2;
}
static int Fail(string message) { Console.Error.WriteLine(message); return 1; }
static bool IsKel103Migration(string operation) =>
    operation is "migrate-kel103-v4" or "migrate-kel103-v5";
static string MigrationFailure(
    Exception exception,
    string operation) => exception switch
{
    KeyNotFoundException => "The selected KEL-103 endpoint is not registered.",
    InvalidOperationException =>
        operation == "migrate-kel103-v4"
            ? "The selected KEL-103 endpoint does not use the exact version 2 definition."
            : "The selected KEL-103 endpoint does not use the exact version 4 definition.",
    InvalidDataException => "The endpoint composition is not valid for migration.",
    OperationCanceledException => "The KEL-103 definition migration was cancelled.",
    _ => "The KEL-103 definition migration failed. Inspect the active profile and retained backups before retrying."
};
