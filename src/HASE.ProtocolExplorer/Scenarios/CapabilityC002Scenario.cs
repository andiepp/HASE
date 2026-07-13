using System.Globalization;
using Hase.Protocol;
using Hase.ProtocolExplorer.Formatting;
using Hase.ProtocolExplorer.Hosting;
using Hase.ProtocolExplorer.Tracing.Model;
using Hase.Simulation.Environment;
using Hase.Simulation.Runtime.Environment;

namespace Hase.ProtocolExplorer.Scenarios;

internal sealed class CapabilityC002Scenario
    : IScenario
{
    private const double InitialTargetTemperature =
        30.0;

    private readonly ProtocolExplorerHost _host;

    private readonly ConsoleTraceFormatter
        _consoleFormatter =
        new();

    public CapabilityC002Scenario(
        ProtocolExplorerHost host)
    {
        _host =
            host
            ?? throw new ArgumentNullException(
                nameof(host));
    }

    public string Name =>
        "c002";

    public void Execute()
    {
        PrepareSimulation();

        WriteCapabilityHeader();

        WriteSimulationState(
            "Simulation State Before");

        var request =
            new ExecuteCommandRequest(
                new CorrelationId(102),
                EnvironmentControllerDescriptorFactory
                    .InstrumentId,
                EnvironmentControllerDescriptorFactory
                    .ResetTargetTemperatureCommandPath,
                Argument: null);

        WriteFlowStep(
            "Runtime Operation",
            "Reset target temperature to its default value.");

        WriteTrace(
            "Protocol Request",
            request);

        WriteFlowStep(
            "Runtime Dispatch",
            "RuntimeProtocolDispatcher",
            "EnvironmentControllerInstrumentExecutor",
            "EnvironmentControllerSimulation");

        ExecuteCommandResponse response =
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

    private void PrepareSimulation()
    {
        _host.ControllerSimulation
            .SetTargetTemperature(
                InitialTargetTemperature);
    }

    private static void WriteCapabilityHeader()
    {
        Console.WriteLine(
            "Capability C-002");

        Console.WriteLine(
            "================");

        Console.WriteLine();

        Console.WriteLine(
            "Reset the simulated environment-controller " +
            "target temperature.");

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
        ExecuteCommandResponse response)
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
            $"Result       : {response.Result.Code}");

        Console.WriteLine(
            $"Return Value : {FormatValue(response.ReturnValue)}");

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

    private static string FormatValue(
        object? value)
    {
        return value switch
        {
            null =>
                "<null>",

            string text =>
                $"\"{text}\"",

            IFormattable formattable =>
                formattable.ToString(
                    null,
                    CultureInfo.InvariantCulture)
                ?? "<null>",

            _ =>
                value.ToString() ?? "<null>"
        };
    }
}