using Hase.DesktopHost.Configuration;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: Hase.DesktopHost.OnboardingAudit <installation-directory>");
    return 2;
}

try
{
    DesktopRuntimeHostInstallationAuditResult result =
        await DesktopRuntimeHostInstallationAudit.AuditAsync(args[0]);

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
