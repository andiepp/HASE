using System.IO;
using Hase.DesktopHost.App.Media;

namespace Hase.DesktopHost.Tests;

public sealed class RuntimeHostMediaWebView2CustodyTests
{
    [Fact]
    public void UserDataDirectory_IsStableOutsideApplicationPublication()
    {
        string localApplicationData = Path.GetFullPath(
            Path.Combine("custody", "local-application-data"));

        string actual = RuntimeHostMediaWebView2Custody
            .GetUserDataDirectory(localApplicationData);

        Assert.Equal(
            Path.Combine(
                localApplicationData,
                "HASE",
                "RuntimeHost",
                "WebView2"),
            actual);
        Assert.DoesNotContain(
            $"{Path.DirectorySeparatorChar}Application{Path.DirectorySeparatorChar}",
            actual,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BindingAndCaptureCreationProperties_UseExactSharedCustody()
    {
        string expected = Path.GetFullPath(
            Path.Combine("custody", "runtime-host", "WebView2"));

        var binding = RuntimeHostMediaWebView2Custody
            .CreateCreationProperties(expected);
        var capture = RuntimeHostMediaWebView2Custody
            .CreateCreationProperties(expected);

        Assert.Equal(expected, binding.UserDataFolder);
        Assert.Equal(expected, capture.UserDataFolder);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void CustodyPath_RejectsMissingRoot(string value)
    {
        Assert.Throws<ArgumentException>(() =>
            RuntimeHostMediaWebView2Custody.GetUserDataDirectory(value));
        Assert.Throws<ArgumentException>(() =>
            RuntimeHostMediaWebView2Custody.CreateCreationProperties(value));
    }
}
