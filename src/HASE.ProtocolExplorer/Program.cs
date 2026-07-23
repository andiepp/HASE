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
                    new UsbSerialCandidatesScenario(),
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
                    new CapabilityC022Scenario(),
                    new CapabilityC023Scenario(),
                    new CapabilityC024Scenario(),
                    new CapabilityC025Scenario()
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

        for (
            int capability = 1;
            capability <= 14;
            capability++)
        {
            Console.WriteLine(
                $"  c{capability:000}");
        }

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

        Console.WriteLine(
            "  c023 [baud rate] [verification timeout seconds]");

        Console.WriteLine(
            "  c024 [baud rate] [verification timeout seconds]");

        Console.WriteLine(
            "  c025 [baud rate] [verification timeout seconds]");

        Console.WriteLine();

        Console.WriteLine(
            "Protocol and discovery scenarios:");

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

        Console.WriteLine(
            "  usb-serial-candidates");
    }
}