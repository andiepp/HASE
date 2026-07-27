namespace Hase.DesktopHost;

public sealed record DesktopRuntimeHostShellInformation(
    string Composition,
    string HostIdentity,
    string ApiVersion,
    string LoopbackBinding,
    string PrivateNetworkBinding);
