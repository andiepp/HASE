using System.Diagnostics;
using Hase.Client;
using Hase.Client.Configuration;
using Hase.Client.Grpc.Configuration;
using Hase.Runtime.Northbound;

if (Process.GetProcessesByName("Hase.Client.Wpf.App").Length > 0)
{
    return Fail("HASE Client is running. Close it before editing the registry.");
}

if (args.Length < 3)
{
    return Usage();
}

string operation = args[0];
string registryPath = Path.GetFullPath(args[1]);
string profileIdText = args[2];
var profileId = new RuntimeHostProfileId(profileIdText);
string backupPath = registryPath
    + "."
    + DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfffffff")
    + ".backup";
var editor = new PrivateNetworkRuntimeHostProfileRegistryEditor();
RuntimeHostId? importedRuntimeHostId = null;

try
{
    switch (operation)
    {
        case "add" when args.Length == 7:
            await editor.AddAsync(
                registryPath,
                backupPath,
                new PrivateNetworkRuntimeHostProfile(
                    new RuntimeHostProfile(
                        profileId,
                        args[3],
                        new RemoteRuntimeHostId(args[4]),
                        ParseEnabled(args[6])),
                    Path.GetFullPath(args[5])));
            break;
        case "add-from-handoff" when args.Length == 6:
            importedRuntimeHostId = await editor.AddFromHandoffAsync(
                registryPath,
                backupPath,
                args[4],
                profileId,
                args[3],
                Path.GetFullPath(args[5]));
            break;
        case "enable" when args.Length == 3:
            await editor.SetEnabledAsync(registryPath, backupPath, profileId, true);
            break;
        case "disable" when args.Length == 3:
            await editor.SetEnabledAsync(registryPath, backupPath, profileId, false);
            break;
        case "remove" when args.Length == 4 && args[3] == profileIdText:
            await editor.RemoveAsync(registryPath, backupPath, profileId);
            break;
        default:
            return Usage();
    }
}
catch (Exception exception)
{
    return Fail(exception.Message);
}

Console.WriteLine($"Runtime Host registry operation succeeded: {operation}");
Console.WriteLine($"Profile ID: {profileId.Value}");
if (importedRuntimeHostId is not null)
{
    Console.WriteLine($"Expected Runtime Host ID: {importedRuntimeHostId.Value}");
}
Console.WriteLine($"Previous registry backup: {backupPath}");
return 0;

static bool ParseEnabled(string value) => value switch
{
    "true" => true,
    "false" => false,
    _ => throw new ArgumentException("Enabled must be exactly 'true' or 'false'.")
};

static int Usage()
{
    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  add <registry> <profile-id> <display-name> <expected-host-id> <private-config> <true|false>");
    Console.Error.WriteLine("  add-from-handoff <registry> <profile-id> <display-name> <handoff> <private-config>");
    Console.Error.WriteLine("  enable|disable <registry> <profile-id>");
    Console.Error.WriteLine("  remove <registry> <profile-id> <same-profile-id-confirmation>");
    return 2;
}

static int Fail(string message)
{
    Console.Error.WriteLine(message);
    return 1;
}
