using System.Globalization;
using Hase.Protocol;
using Hase.ProtocolExplorer.Formatting;
using Hase.ProtocolExplorer.Hosting;
using Hase.ProtocolExplorer.Tracing.Model;
using Hase.Simulation.Runtime.Environment;

namespace Hase.ProtocolExplorer.Scenarios;

internal sealed class CapabilityC001Scenario
    : IScenario
{
    private const double RequestedTargetTemperature =
        25.0;

    private readonly ProtocolExplorerHost _host;

    private readonly ConsoleTraceFormatter
        _consoleFormatter =
        new();

    public CapabilityC001Scenario(
        ProtocolExplorerHost host)
    {
        _host =
            host
            ?? throw new ArgumentNullException(
                nameof(host));
    }

    public string Name =>
        "c001";

    public void Execute()
    {
        WriteCapabilityHeader();

        WriteSimulationState(
            "Simulation State Before");

        var request =
            new WritePropertyRequest(
                new CorrelationId(101),
                EnvironmentControllerDescriptorFactory
                    .InstrumentId,
                EnvironmentControllerDescriptorFactory
                    .TargetTemperaturePropertyId,
                RequestedTargetTemperature);

        WriteFlowStep(
            "Runtime Operation",
            $"Write target temperature " +
            $"{FormatTemperature(RequestedTargetTemperature)}");

        WriteTrace(
            "Protocol Request",
            request);

        WriteFlowStep(
            "Runtime Dispatch",
            "RuntimeProtocolDispatcher",
            "EnvironmentControllerInstrumentExecutor",
            "EnvironmentControllerSimulation");

        WritePropertyResponse response =
            _host.Dispatcher
                .DispatchAsync(request)
                .GetAwaiter()
                .GetResult();

        WriteSimulationState(
            "Simulation State After");

        WriteTrace(
            "Protocol Response",
            response);

        WriteResult(
            response);
    }

    private static void WriteCapabilityHeader()
    {
        Console.WriteLine(
            "Capability C-001");

        Console.WriteLine(
            "================");

        Console.WriteLine();

        Console.WriteLine(
            "Write simulated environment-controller target temperature.");

        Console.WriteLine();
    }

    private void WriteSimulationState(
        string title)
    {
        Console.WriteLine(title);

        Console.WriteLine(
            new string(
                '-',
                title.Length));

        Console.WriteLine();

        Console.WriteLine(
            $"Target Temperature : " +
            $"{FormatTemperature(_host.ControllerState.TargetTemperature)}");

        Console.WriteLine();
    }

    private static void WriteFlowStep(
        string title,
        params string[] lines)
    {
        Console.WriteLine(title);

        Console.WriteLine(
            new string(
                '-',
                title.Length));

        Console.WriteLine();

        for (int index = 0;
            index < lines.Length;
            index++)
        {
            Console.WriteLine(
                lines[index]);

            if (index < lines.Length - 1)
            {
                Console.WriteLine(
                    "    ↓");
            }
        }

        Console.WriteLine();
    }

    private void WriteTrace(
        string title,
        ProtocolMessage message)
    {
        Console.WriteLine(title);

        Console.WriteLine(
            new string(
                '=',
                title.Length));

        Console.WriteLine();

        TraceDocument trace =
            _host.TraceGenerator.Generate(
                message);

        _consoleFormatter.Write(
            trace);
    }

    private static void WriteResult(
        WritePropertyResponse response)
    {
        const string title =
            "Capability Result";

        Console.WriteLine(title);

        Console.WriteLine(
            new string(
                '-',
                title.Length));

        Console.WriteLine();

        Console.WriteLine(
            $"Result : {response.Result.Code}");

        Console.WriteLine();
    }

    private static string FormatTemperature(
        double value)
    {
        return
            value.ToString(
                "0.0",
                CultureInfo.InvariantCulture)
            + " °C";
    }
}