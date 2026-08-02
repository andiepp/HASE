using Hase.DesktopHost.Configuration;

if (args.Length == 2 && args[0] == "create-identity")
{
    try
    {
        var store = new Hase.Runtime.Northbound.FileRuntimeHostIdentityStore(
            Path.GetFullPath(args[1]));
        var candidate = new Hase.Runtime.Northbound.RuntimeHostId(
            $"runtime-host-{Guid.NewGuid():D}");
        var result = await store.CreateIfMissingAsync(candidate);
        if (result.Outcome != Hase.Runtime.Northbound.RuntimeHostIdentityStoreCreateOutcome.Created)
        {
            Console.Error.WriteLine("Runtime Host identity creation refused because an identity already exists.");
            return 1;
        }

        Console.WriteLine("Runtime Host identity created.");
        Console.WriteLine("Authoritative identity value: Withheld");
        return 0;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine(exception.Message);
        return 1;
    }
}

if (args.Length is not (1 or 3))
{
    Console.Error.WriteLine("Usage: Hase.DesktopHost.OnboardingAudit <installation-directory>");
    Console.Error.WriteLine("   or: Hase.DesktopHost.OnboardingAudit export <installation-directory> <handoff-path>");
    Console.Error.WriteLine("   or: Hase.DesktopHost.OnboardingAudit create-identity <identity-path>");
    return 2;
}

try
{
    DesktopRuntimeHostInstallationAuditResult result =
        await DesktopRuntimeHostInstallationAudit.AuditAsync(
            args.Length == 1 ? args[0] : args[1]);

    if (args.Length == 3)
    {
        if (args[0] != "export")
            return 2;
        await Hase.Runtime.Northbound.RuntimeHostOnboardingHandoffFile.CreateAsync(
            args[2], result.RuntimeHostId);
        Console.WriteLine("Runtime Host onboarding handoff created.");
        Console.WriteLine($"Authoritative Runtime Host ID: {result.RuntimeHostId.Value}");
        Console.WriteLine($"Handoff path: {Path.GetFullPath(args[2])}");
        return 0;
    }

    Console.WriteLine("Runtime Host onboarding audit succeeded.");
    Console.WriteLine($"Authoritative Runtime Host ID: {result.RuntimeHostId.Value}");
    Console.WriteLine("Application executable       : Ready");
    Console.WriteLine("Application profile          : Ready");
    Console.WriteLine("Installation identity        : Ready");
    Console.WriteLine("Private-network configuration: Ready");
    Console.WriteLine("Client enrollment            : Ready");
    Console.WriteLine("Endpoint composition         : Ready");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}
