namespace Hase.DesktopHost.App.Media;

public sealed class RuntimeHostMediaInventoryWebViewPolicy
{
    // Device identities are origin-scoped. Inventory and capture therefore
    // intentionally share the same repository-owned origin and durable
    // WebView2 profile while using separate pages and controllers.
    public const string VirtualHostName = RuntimeHostMediaWebViewPolicy.VirtualHostName;
    public static readonly Uri ApplicationUri = new(
        $"https://{VirtualHostName}/inventory.html",
        UriKind.Absolute);

    public bool IsAllowedResource(string? uriText)
    {
        if (!Uri.TryCreate(uriText, UriKind.Absolute, out var uri))
        {
            return false;
        }
        return uri.Scheme == Uri.UriSchemeHttps &&
            string.Equals(
                uri.Host,
                VirtualHostName,
                StringComparison.OrdinalIgnoreCase) &&
            uri.IsDefaultPort &&
            string.IsNullOrEmpty(uri.UserInfo);
    }
}
