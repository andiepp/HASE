using System.Management;

namespace Hase.Transport.Discovery;

/// <summary>
/// Queries serial-port Plug-and-Play entities through Windows Management
/// Instrumentation.
/// </summary>
internal sealed class SystemManagementWindowsPnpEntityQuery
    : IWindowsPnpEntityQuery
{
    internal const string QueryText =
        "SELECT Name, Manufacturer, PNPDeviceID, Description "
        + "FROM Win32_PnPEntity "
        + "WHERE Name LIKE '%(COM%)'";

    public IReadOnlyList<WindowsPnpEntitySnapshot> Query()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Windows USB serial discovery is supported only on Windows.");
        }

        using var searcher =
            new ManagementObjectSearcher(
                QueryText);

        using ManagementObjectCollection results =
            searcher.Get();

        var snapshots =
            new List<WindowsPnpEntitySnapshot>();

        foreach (
            ManagementObject managementObject
            in results)
        {
            using (managementObject)
            {
                snapshots.Add(
                    new WindowsPnpEntitySnapshot(
                        ReadString(
                            managementObject,
                            "Name"),
                        ReadString(
                            managementObject,
                            "Manufacturer"),
                        ReadString(
                            managementObject,
                            "PNPDeviceID"),
                        ReadString(
                            managementObject,
                            "Description")));
            }
        }

        return snapshots;
    }

    private static string? ReadString(
        ManagementBaseObject managementObject,
        string propertyName)
    {
        object? value =
            managementObject[
                propertyName];

        return value as string;
    }
}