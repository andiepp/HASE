using Hase.ProtocolExplorer.Hosting;
using Hase.ProtocolExplorer.Scenarios;

namespace Hase.ProtocolExplorer;

internal static class Program
{
    public static int Main(
        string[] args)
    {
        ArgumentNullException.ThrowIfNull(
            args);

        WriteHeader();

        if (args.Length < 1)
        {
            WriteHelp();
            return 1;
        }

        ProtocolExplorerHost host =
            new();

        ScenarioRunner runner =
            new(
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
                    new CapabilityC001Scenario(
                        host),
                    new CapabilityC002Scenario(
                        host),
                    new CapabilityC003Scenario()
                ]);

        string scenarioName =
            args[0];

        IReadOnlyList<string> scenarioArguments =
            args
                .Skip(
                    1)
                .ToArray();

        try
        {
            if (!runner.TryRun(
                    scenarioName,
                    scenarioArguments))
            {
                Console.WriteLine(
                    $"Unknown scenario '{scenarioName}'.");

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
                $"{exception.GetType().Name}: "
                + $"{exception.Message}");

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
            "  Hase.ProtocolExplorer <scenario> [arguments]");

        Console.WriteLine();

        Console.WriteLine(
            "Capability scenarios:");

        Console.WriteLine(
            "  c001");

        Console.WriteLine(
            "  c002");

        Console.WriteLine(
            "  c003 <ESP32 host or IP address>");

        Console.WriteLine();

        Console.WriteLine(
            "Protocol scenarios:");

        Console.WriteLine(
            "  discover");

        Console.WriteLine(
            "  discover-response");

        Console.WriteLine(
            "  read");

        Console.WriteLine(
            "  read-response");

        Console.WriteLine(
            "  write");

        Console.WriteLine(
            "  write-response");

        Console.WriteLine(
            "  command");

        Console.WriteLine(
            "  command-response");

        Console.WriteLine(
            "  event");

        Console.WriteLine(
            "  descriptor-response");
    }
}