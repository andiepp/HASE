using Hase.DesktopHost.Configuration;

namespace Hase.DesktopHost.Tests.Configuration;

public sealed class SecondPcRuntimeHostPreflightAssessmentTests
{
    [Fact]
    public void AllReady_ShouldBeReady()
    {
        SecondPcRuntimeHostPreflightAssessment assessment = Create();
        Assert.True(assessment.IsReady);
        Assert.All(assessment.Readiness, item => Assert.True(item.Ready));
    }

    [Fact]
    public void ExistingInstallation_ShouldBlock()
    {
        SecondPcRuntimeHostPreflightAssessment assessment = Create(installation: false);
        Assert.False(assessment.IsReady);
        Assert.False(assessment.Readiness.Single(item =>
            item.Name == "Guided installation state").Ready);
    }

    [Fact]
    public void InvalidPrivateNetworkConfiguration_ShouldBlock()
    {
        Assert.False(Create(configuration: false).IsReady);
    }

    [Fact]
    public void CertificateWithoutPrivateKey_ShouldBlock()
    {
        Assert.False(Create(certificate: false).IsReady);
    }

    [Fact]
    public void ListenerAddressNotOwned_ShouldBlock()
    {
        Assert.False(Create(address: false).IsReady);
    }

    [Fact]
    public void MissingArduinoCandidate_ShouldBlock()
    {
        Assert.False(Create(arduino: false).IsReady);
    }

    private static SecondPcRuntimeHostPreflightAssessment Create(
        bool installation = true,
        bool configuration = true,
        bool certificate = true,
        bool address = true,
        bool arduino = true) =>
        new(
            WindowsReady: true,
            DotNetSdkReady: true,
            RepositoryReady: true,
            InstallationStateClean: installation,
            ShortcutStateClean: true,
            PrivateNetworkConfigurationReady: configuration,
            ClientEnrollmentReady: true,
            ServerCertificateReady: certificate,
            ListenerAddressOwned: address,
            ArduinoCandidatePresent: arduino);
}
