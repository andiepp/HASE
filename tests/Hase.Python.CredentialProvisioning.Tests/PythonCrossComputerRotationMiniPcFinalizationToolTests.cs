namespace Hase.Python.CredentialProvisioning.Tests;

public sealed class PythonCrossComputerRotationMiniPcFinalizationToolTests
{
    [Fact]
    public void CompleteTool_RequiresExactMachineRepositoryProcessesAndProof()
    {
        string script = ReadTool(
            "Complete-HaseMiniPcLaptopPythonCrossComputerRotation.ps1");

        Assert.Contains("$env:COMPUTERNAME -cne \"LABC\"", script);
        Assert.Contains("rev-parse origin/main", script);
        Assert.Contains("status --porcelain", script);
        Assert.Contains("Hase.DesktopHost.App", script);
        Assert.Contains("Hase.Client.Wpf.App", script);
        Assert.Contains("$ReplacementConnectionProven", script);
        Assert.Contains("$ExpectedTransactionId", script);
    }

    [Fact]
    public void CompleteTool_BindsBeginEvidenceAndExactRevisions()
    {
        string script = ReadTool(
            "Complete-HaseMiniPcLaptopPythonCrossComputerRotation.ps1");

        Assert.Contains("cross-computer-rotation.transaction.json", script);
        Assert.Contains("overlap-published", script);
        Assert.Contains("OverlapSha256", script);
        Assert.Contains("FinalSha256", script);
        Assert.Contains("AuthorizationPolicySha256", script);
        Assert.Contains("expectedCurrentCredentialId", script);
        Assert.Contains("hase-laptop-python-minipc", script);
    }

    [Fact]
    public void CompleteTool_PreservesAclAndRetainsRecoveryEvidence()
    {
        string script = ReadTool(
            "Complete-HaseMiniPcLaptopPythonCrossComputerRotation.ps1");

        Assert.Contains("Write-HaseExistingBytes $enrollment $finalBytes", script);
        Assert.Contains("Get-HaseAccessSddl", script);
        Assert.DoesNotContain("Set-Acl", script);
        Assert.Contains("Test-HasePrivateCustodyFile", script);
        Assert.Contains("GetAccessRules($true, $true", script);
        Assert.Contains("enrollment.overlap-before-finalization.json", script);
        Assert.Contains("originalBackupPath", script);
        Assert.Contains("phase = \"prepared\"", script);
        Assert.Contains("$journal.phase = \"committed\"", script);
    }

    [Fact]
    public void RecoverTool_OnlyRecoversInterruptedPreparedFinalization()
    {
        string script = ReadTool(
            "Recover-HaseMiniPcLaptopPythonCrossComputerRotationFinalization.ps1");

        Assert.Contains("Committed finalization cannot be implicitly rolled back", script);
        Assert.Contains("phase -cne \"prepared\"", script);
        Assert.Contains("overlapBackupPath", script);
        Assert.Contains("$journal.phase = \"rolled-back\"", script);
        Assert.Contains("Enrollment ACL unchanged", script);
    }

    [Fact]
    public void TestTool_RequiresFinalReplacementAndProtectedEvidence()
    {
        string script = ReadTool(
            "Test-HaseMiniPcLaptopPythonCrossComputerRotationFinalization.ps1");

        Assert.Contains("phase -cne \"committed\"", script);
        Assert.Contains("replacementConnectionProven", script);
        Assert.Contains("CurrentCredentialId", script);
        Assert.Contains("ReplacementCredentialId", script);
        Assert.Contains("Test-HasePrivateCustodyFile", script);
        Assert.Contains("AreAccessRulesProtected", script);
        Assert.Contains("MiniPC finalized               : True", script);
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

}
