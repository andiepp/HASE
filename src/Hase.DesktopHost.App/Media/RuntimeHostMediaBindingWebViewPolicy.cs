namespace Hase.DesktopHost.App.Media;

public sealed class RuntimeHostMediaBindingWebViewPolicy
{
    public static readonly Uri ApplicationUri =
        new(
            $"https://{RuntimeHostMediaWebViewPolicy.VirtualHostName}/binding.html",
            UriKind.Absolute);

    private static readonly HashSet<string> AllowedPaths =
        new(StringComparer.Ordinal)
        {
            "/binding.html",
            "/binding.css",
            "/binding.js"
        };

    private bool discoveryArmed;

    public void ArmDiscovery() => discoveryArmed = true;
    public void EndDiscovery() => discoveryArmed = false;

    public bool IsAllowedResource(string? uriText)
    {
        if (!Uri.TryCreate(uriText, UriKind.Absolute, out Uri? uri))
        {
            return false;
        }
        return uri.Scheme == Uri.UriSchemeHttps &&
            string.Equals(
                uri.Host,
                RuntimeHostMediaWebViewPolicy.VirtualHostName,
                StringComparison.OrdinalIgnoreCase) &&
            uri.IsDefaultPort &&
            string.IsNullOrEmpty(uri.UserInfo) &&
            string.IsNullOrEmpty(uri.Query) &&
            string.IsNullOrEmpty(uri.Fragment) &&
            AllowedPaths.Contains(uri.AbsolutePath);
    }

    public bool IsPermissionAllowed(
        string? origin,
        RuntimeHostMediaPermissionKind kind)
    {
        if (!discoveryArmed ||
            !Uri.TryCreate(origin, UriKind.Absolute, out Uri? uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(
                uri.Host,
                RuntimeHostMediaWebViewPolicy.VirtualHostName,
                StringComparison.OrdinalIgnoreCase) ||
            !uri.IsDefaultPort ||
            !string.IsNullOrEmpty(uri.UserInfo))
        {
            return false;
        }

        return kind is RuntimeHostMediaPermissionKind.Camera or
            RuntimeHostMediaPermissionKind.Microphone;
    }
}
