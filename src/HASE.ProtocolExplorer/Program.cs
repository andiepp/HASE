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

        if (args.Length != 1)
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
                    new ReadEndpointDescriptorResponseScenario()
                ]);

        if (!runner.TryRun(
                args[0]))
        {
            Console.WriteLine(
                $"Unknown scenario '{args[0]}'.");

            Console.WriteLine();

            WriteHelp();

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
            "  Hase.ProtocolExplorer <scenario>");

        Console.WriteLine();

        Console.WriteLine(
            "Available scenarios:");

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