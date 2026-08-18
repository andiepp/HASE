using System.IO;
using Microsoft.Web.WebView2.Wpf;

namespace Hase.DesktopHost.App.Media;

/// <summary>
/// Keeps the Runtime Host WebView2 profile outside replaceable application
/// publication custody. Camera device identifiers are scoped to this profile,
/// so binding and capture must always share the same durable directory.
/// </summary>
public static class RuntimeHostMediaWebView2Custody
{
    public const string DirectoryName = "WebView2";

    public static string GetDefaultUserDataDirectory() =>
        GetUserDataDirectory(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData));

    public static string GetUserDataDirectory(string localApplicationData)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localApplicationData);
        string root = Path.GetFullPath(localApplicationData);
        return Path.Combine(root, "HASE", "RuntimeHost", DirectoryName);
    }

    public static CoreWebView2CreationProperties CreateCreationProperties(
        string userDataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userDataDirectory);
        return new CoreWebView2CreationProperties
        {
            UserDataFolder = Path.GetFullPath(userDataDirectory)
        };
    }
}
