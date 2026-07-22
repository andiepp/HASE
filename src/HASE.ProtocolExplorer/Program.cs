using Hase.ProtocolExplorer.Hosting;
using Hase.ProtocolExplorer.Scenarios;

namespace Hase.ProtocolExplorer;

internal static class Program
{
    public static int Main(
        string[] args)
    {
        WriteHeader();

        if (args.Length == 0)
        {
            WriteHelp();
            return 1;
        }

        var host =
            new ProtocolExplorerHost();

        var runner =
            new ScenarioRunner(
                host.TraceGenerator,
                [
                    new DiscoverScenario(),
                    new DiscoverResponseScenario(),
                    new ReadPropertyScenario(),
                    new ReadPropertyResponseScenario(),
                    new WritePropertyScenario(),
                    new WritePropertyResponseScenario(),
                    new ExecuteCommandScenario(),
                    new ExecuteCommandResponseScenario(),
                    new EventNotificationScenario(),
                    new ReadEndpointDescriptorResponseScenario(),
                    new NetworkDiscoveryScenario(),
                    new CapabilityC001Scenario(host),
                    new CapabilityC002Scenario(host),
                    new CapabilityC003Scenario(),
                    new CapabilityC004Scenario(),
                    new CapabilityC005Scenario(),
                    new CapabilityC006Scenario(),
                    new CapabilityC007Scenario(),
                    new CapabilityC008Scenario(),
                    new CapabilityC009Scenario(),
                    new CapabilityC010Scenario(),
                    new CapabilityC011Scenario(),
                    new CapabilityC012Scenario(),
                    new CapabilityC013Scenario(),
                    new CapabilityC014Scenario(),
                    new CapabilityC016Scenario(),
                    new CapabilityC017Scenario(),
                    new CapabilityC018Scenario(),
                    new CapabilityC019Scenario(),
                    new CapabilityC020Scenario(),
                    new CapabilityC021Scenario(),
                    new CapabilityC022Scenario()
                ]);

        string scenarioName =
            args[0];

        string[] scenarioArguments =
            args
                .Skip(1)
                .ToArray();

        try
        {
            bool scenarioFound =
                runner.TryRun(
                    scenarioName,
                    scenarioArguments);

            if (!scenarioFound)
            {
                Console.WriteLine(
                    $"Unknown scenario: {scenarioName}");
                Console.WriteLine();

                WriteHelp();
                return 1;
            }
        }
        catch (Exception exception)
        {
            Console.WriteLine();
            Console.WriteLine(
                "Scenario failed.");
            Console.WriteLine();
            Console.WriteLine(
                $"Exception type : {exception.GetType().FullName}");
            Console.WriteLine(
                $"Message        : {exception.Message}");

            return 1;
        }

        return 0;
    }

    private static void WriteHeader()
    {
        Console.WriteLine(
            "HASE Protocol Explorer");
        Console.WriteLine(
            "======================");
        Console.WriteLine();
    }

    private static void WriteHelp()
    {
        Console.WriteLine(
            "Usage:");
        Console.WriteLine();
        Console.WriteLine(
            "  HASE.ProtocolExplorer <scenario> [arguments]");
        Console.WriteLine();

        Console.WriteLine(
            "Capability scenarios:");
        Console.WriteLine();
        Console.WriteLine(
            "  c001");
        Console.WriteLine(
            "  c002");
        Console.WriteLine(
            "  c003");
        Console.WriteLine(
            "  c004");
        Console.WriteLine(
            "  c005");
        Console.WriteLine(
            "  c006");
        Console.WriteLine(
            "  c007");
        Console.WriteLine(
            "  c008");
        Console.WriteLine(
            "  c009");
        Console.WriteLine(
            "  c010");
        Console.WriteLine(
            "  c011");
        Console.WriteLine(
            "  c012");
        Console.WriteLine(
            "  c013");
        Console.WriteLine(
            "  c014");
        Console.WriteLine(
            "  c016");
        Console.WriteLine(
            "  c017");
        Console.WriteLine(
            "  c018 <COM port> [baud rate]");
        Console.WriteLine(
            "  c019 <COM port> [baud rate]");
        Console.WriteLine(
            "  c020 <COM port> [baud rate]");
        Console.WriteLine(
            "  c021 <COM port> [baud rate]");
        Console.WriteLine(
            "  c022 <COM port> [baud rate]");
        Console.WriteLine();

        Console.WriteLine(
            "Protocol scenarios:");
        Console.WriteLine();
        Console.WriteLine(
            "  discover");
        Console.WriteLine(
            "  discover-response");
        Console.WriteLine(
            "  read-property");
        Console.WriteLine(
            "  read-property-response");
        Console.WriteLine(
            "  write-property");
        Console.WriteLine(
            "  write-property-response");
        Console.WriteLine(
            "  execute-command");
        Console.WriteLine(
            "  execute-command-response");
        Console.WriteLine(
            "  event-notification");
        Console.WriteLine(
            "  read-endpoint-descriptor-response");
        Console.WriteLine(
            "  network-discovery");
    }
}