namespace Hase.Python.CredentialProvisioning.Tests;

public sealed class PythonCrossComputerRotationLaptopCutoverToolTests
{
    [Fact]
    public void InstallTool_RequiresLaptopCleanRepositoryAndStoppedProcesses()
    {
        string script = ReadTool(
            "Install-HaseLaptopMiniPcPythonCrossComputerRotation.ps1");

        Assert.Contains("$env:COMPUTERNAME -cne $ExpectedComputer", script);
        Assert.Matches(@"\[string\]\s*\$ExpectedComputer", script);
        Assert.Contains("rev-parse origin/main", script);
        Assert.Contains("status --porcelain", script);
        Assert.Contains("Hase.DesktopHost.App", script);
        Assert.Contains("Hase.Client.Wpf.App", script);
    }

    [Fact]
    public void InstallTool_RejectsIndirectAndRepositoryCustody()
    {
        string script = ReadTool(
            "Install-HaseLaptopMiniPcPythonCrossComputerRotation.ps1");

        Assert.Contains("Test-HaseReparsePointInExistingChain", script);
        Assert.Contains("Credential custody must remain outside", script);
        Assert.Contains("SetAccessRuleProtection($true, $false)", script);
    }

    [Fact]
    public void InstallTool_ValidatesExactArchiveManifestHashesAndIdentities()
    {
        string script = ReadTool(
            "Install-HaseLaptopMiniPcPythonCrossComputerRotation.ps1");

        foreach (string name in new[]
        {
            "client-certificate.pem",
            "private-key.pem",
            "runtime-host-profile.json",
            "transfer-manifest.json",
        })
        {
            Assert.Contains(name, script);
        }

        Assert.Contains(
            "hase-laptop-minipc-python-cross-computer-rotation-package",
            script);
        Assert.Contains("manifest.currentCredentialId", script);
        Assert.Contains("manifest.replacementCredentialId", script);
        Assert.Contains("replacement payload hash", script);
    }

    [Fact]
    public void InstallTool_RetainsRollbackAndRestoresAfterInstallationFailure()
    {
        string script = ReadTool(
            "Install-HaseLaptopMiniPcPythonCrossComputerRotation.ps1");

        Assert.Contains("phase = \"prepared\"", script);
        Assert.Contains("phase = \"replacement-installed\"", script);
        Assert.Contains("Restore-HaseInstalledFiles", script);
        Assert.Contains("rollback requires operator recovery", script);
        Assert.Equal(3, CountOccurrences(script,
            "OriginalAccessSddl = (Get-Acl"));
        Assert.Equal(5, CountOccurrences(script,
            "GetSecurityDescriptorSddlForm("));
        Assert.DoesNotContain("-AclObject $file", script);
        Assert.Contains("Installed ACL changed during replacement", script);
        Assert.Contains("Installed ACL changed during rollback", script);
        Assert.Equal(1, CountOccurrences(script,
            "Set-HasePrivateFile $journalPath"));
        Assert.Contains("$journal.phase = \"replacement-installed\"", script);
        Assert.Contains("$primaryFailureType", script);
        Assert.Contains("$rollbackFailureType", script);
        Assert.DoesNotContain("EnrollmentPath", script);
        Assert.DoesNotContain("AuthorizationPolicyPath", script);
    }

    [Fact]
    public void TestTool_RequiresExactInstallationProtectedArchiveAndRollback()
    {
        string script = ReadTool(
            "Test-HaseLaptopMiniPcPythonCrossComputerRotation.ps1");

        Assert.Contains("replacement-installed", script);
        Assert.Contains("AreAccessRulesProtected", script);
        Assert.Contains("An installed replacement was not byte-exact", script);
        Assert.Contains("Old credential rollback ready: True", script);
        Assert.Contains("MiniPC overlap changed       : False", script);
    }

    private static string ReadTool(string fileName)
    {
        DirectoryInfo? directory = new(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            string path = Path.Combine(directory.FullName, "python",
                "hase-client", "tools", fileName);
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }

    private static int CountOccurrences(string value, string fragment)
    {
        int count = 0;
        int offset = 0;
        while ((offset = value.IndexOf(fragment, offset,
            StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += fragment.Length;
        }

        return count;
    }
}
