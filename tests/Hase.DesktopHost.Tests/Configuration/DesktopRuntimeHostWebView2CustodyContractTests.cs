using System.IO;

namespace Hase.DesktopHost.Tests.Configuration;

public sealed class DesktopRuntimeHostWebView2CustodyContractTests
{
    [Fact]
    public void Publisher_MigratesLegacyProfileOutsideApplicationWithRollback()
    {
        string script = ReadScript("Publish-HaseDesktopRuntimeHost.ps1");

        Assert.Contains(
            "$webView2DataDirectory = Join-Path $installationRoot \"WebView2\"",
            script);
        Assert.Contains("$legacyWebView2DataDirectory", script);
        Assert.Contains("Both legacy and durable", script);
        Assert.Contains("$legacyWebView2Migrated = $true", script);
        Assert.Contains(
            "-Destination $webView2DataDirectory",
            script);
        Assert.Contains(
            "-Destination $legacyWebView2DataDirectory",
            script);
        Assert.Contains("catch {", script);
        Assert.Contains("WebView2 custody", script);
    }

    [Fact]
    public void Updater_VerifiesDurableCustodyAndRejectsLegacyRemainder()
    {
        string script = ReadScript("Update-HaseDesktopRuntimeHost.ps1");

        Assert.Contains("$webView2PresentBefore", script);
        Assert.Contains("$legacyWebView2PresentBefore", script);
        Assert.Contains("WebView2 custody was not preserved", script);
        Assert.Contains(
            "Legacy WebView2 custody remained inside the replaceable application directory",
            script);
        Assert.DoesNotContain("Remove-Item -LiteralPath $webView2DataDirectory", script);
    }

    [Fact]
    public void Installer_ProtectsExistingDurableCustody()
    {
        string script = ReadScript("Install-HaseDesktopRuntimeHost.ps1");

        Assert.Contains(
            "$webView2DataDirectory = Join-Path $installationDirectory \"WebView2\"",
            script);
        Assert.Contains("$webView2DataDirectory", script);
        Assert.Contains("WebView2 custody", script);
    }

    private static string ReadScript(string name)
    {
        string repositoryRoot = GetRepositoryRoot();
        return File.ReadAllText(
            Path.Combine(repositoryRoot, "tools", "Deployment", name));
    }

    private static string GetRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                Directory.Exists(Path.Combine(directory.FullName, "tools")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
