namespace Hase.Python.CredentialProvisioning.Tests;

public sealed class PythonRotationObsoletePrivateKeyCleanupToolTests
{
    [Fact]
    public void RemoveTool_BindsExplicitTargetsAndProtectsActiveKey()
    {
        string script=Read("Remove-HaseLaptopObsoletePythonRotationPrivateKey.ps1");
        Assert.Contains("$CutoverCustodyDirectories",script);
        Assert.Contains("$ReplacementOnlyConnectionProven",script);
        Assert.Contains("(Hash $activeKey) -ceq $oldHash",script);
        Assert.Contains("rollback\\private-key.pem",script);
        Assert.Contains("$journal.transactionId",script);
        Assert.Contains("$request.privateKeySha256",script);
    }

    [Fact]
    public void RemoveTool_QuarantinesBeforeDeletionAndRetainsJournal()
    {
        string script=Read("Remove-HaseLaptopObsoletePythonRotationPrivateKey.ps1");
        Assert.Contains("phase = \"prepared\"",script);
        Assert.Contains("$journal.phase = \"quarantined\"",script);
        Assert.Contains("$journal.phase = \"committed\"",script);
        Assert.True(script.IndexOf("Move-Item",StringComparison.Ordinal)<script.IndexOf("Remove-Item",StringComparison.Ordinal));
        Assert.Contains("Non-secret evidence retained",script);
    }

    [Fact]
    public void RecoveryResumesOnlyUncommittedTransaction()
    {
        string script=Read("Resume-HaseLaptopObsoletePythonRotationPrivateKeyCleanup.ps1");
        Assert.Contains("phase-ceq\"committed\"",script);
        Assert.Contains("oldPrivateKeySha256",script);
        Assert.Contains("Remove-Item",script);
    }

    [Fact]
    public void TestTool_RequiresCommittedAbsenceAndActiveKey()
    {
        string script=Read("Test-HaseLaptopObsoletePythonRotationPrivateKeyCleanup.ps1");
        Assert.Contains("phase-cne\"committed\"",script);
        Assert.Contains("Test-Path $active",script);
        Assert.Contains("Test-Path $_.source",script);
        Assert.Contains("Private-key cleanup valid",script);
    }

    private static string Read(string name)
    {
        DirectoryInfo? d=new(Directory.GetCurrentDirectory());
        while(d is not null){string p=Path.Combine(d.FullName,"python","hase-client","tools",name);if(File.Exists(p))return File.ReadAllText(p);d=d.Parent;}
        throw new InvalidOperationException("Repository root was not found.");
    }
}
