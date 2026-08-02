namespace Hase.DesktopHost.Configuration;

public sealed record SecondPcRuntimeHostPreflightAssessment(
    bool WindowsReady,
    bool DotNetSdkReady,
    bool RepositoryReady,
    bool InstallationStateClean,
    bool ShortcutStateClean,
    bool PrivateNetworkConfigurationReady,
    bool ClientEnrollmentReady,
    bool ServerCertificateReady,
    bool ListenerAddressOwned,
    bool ArduinoCandidatePresent)
{
    public bool IsReady => Readiness.All(item => item.Ready);

    public IReadOnlyList<(string Name, bool Ready)> Readiness =>
    [
        ("Windows platform", WindowsReady),
        (".NET 10 SDK", DotNetSdkReady),
        ("Repository Release source", RepositoryReady),
        ("Guided installation state", InstallationStateClean),
        ("Desktop shortcut state", ShortcutStateClean),
        ("Private-network configuration", PrivateNetworkConfigurationReady),
        ("Client enrollment", ClientEnrollmentReady),
        ("Server certificate private key", ServerCertificateReady),
        ("Listener address ownership", ListenerAddressOwned),
        ("Arduino USB candidate", ArduinoCandidatePresent)
    ];
}
