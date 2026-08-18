namespace Hase.Client.Wpf.AppHost.Media;

/// <summary>
/// Receiver-only browser policy. The viewing Client never grants camera,
/// microphone, geolocation, notification, clipboard, or other permissions.
/// </summary>
public sealed class ClientMediaWebViewPolicy
{
    public const string VirtualHostName = "hase-media-client.local";
    public const string AssetVersion = "55f4c17";
    public static readonly Uri ApplicationUri =
        new($"https://{VirtualHostName}/index.html?v={AssetVersion}",
            UriKind.Absolute);

    public bool IsAllowedResource(string? uriText)
    {
        if (!Uri.TryCreate(uriText, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Scheme == Uri.UriSchemeHttps &&
            string.Equals(uri.Host, VirtualHostName,
                StringComparison.OrdinalIgnoreCase) &&
            uri.IsDefaultPort &&
            string.IsNullOrEmpty(uri.UserInfo);
    }

    public bool IsPermissionAllowed() => false;
}
