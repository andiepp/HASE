using System.Diagnostics;
using Hase.DesktopHost.Configuration;

if (Process.GetProcessesByName("Hase.DesktopHost.App").Length > 0)
    return Fail("HASE Desktop Runtime Host is running. Close it before editing endpoint composition.");
if (args.Length < 3) return Usage();

string operation = args[0];
string profilePath = Path.GetFullPath(args[1]);
string endpointId = args[2];
string backupPath = profilePath + "." + DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfffffff") + ".backup";
var editor = new DesktopRuntimeHostEndpointCompositionProfileEditor();
try
{
    switch (operation)
    {
        case "add-native" when args.Length == 5:
            await editor.AddNativeAsync(profilePath, backupPath,
                new DesktopRuntimeHostNativeNetworkEndpointProfile(
                    endpointId, args[3], int.Parse(args[4])));
            break;
        case "remove-native" when args.Length == 4 && args[3] == endpointId:
            await editor.RemoveNativeAsync(profilePath, backupPath, endpointId);
            break;
        default: return Usage();
    }
}
catch (Exception exception) { return Fail(exception.Message); }
Console.WriteLine($"Endpoint profile operation succeeded: {operation}");
Console.WriteLine($"Expected endpoint ID: {endpointId}");
Console.WriteLine("Endpoint kind: NativeNetwork");
Console.WriteLine($"Previous composition backup: {backupPath}");
return 0;

static int Usage() { Console.Error.WriteLine("Usage: add-native <composition> <endpoint-id> <host> <port> | remove-native <composition> <endpoint-id> <same-id-confirmation>"); return 2; }
static int Fail(string message) { Console.Error.WriteLine(message); return 1; }
