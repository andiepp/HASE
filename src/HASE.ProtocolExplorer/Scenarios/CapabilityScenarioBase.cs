using System.Globalization;
using Hase.Protocol;
using Hase.ProtocolExplorer.Formatting;
using Hase.ProtocolExplorer.Hosting;
using Hase.ProtocolExplorer.Tracing.Model;
using Hase.ProtocolExplorer.Transport;

namespace Hase.ProtocolExplorer.Scenarios;

internal abstract class CapabilityScenarioBase<TResponse>
    : IScenario
    where TResponse : ProtocolMessage
{
    private readonly ConsoleTraceFormatter
        _consoleFormatter =
        new();

    private readonly HexDumpFormatter
        _hexDumpFormatter =
        new();

    protected CapabilityScenarioBase(
        ProtocolExplorerHost host)
    {
        Host =
            host
            ?? throw new ArgumentNullException(
                nameof(host));
    }

    protected ProtocolExplorerHost Host
    {
        get;
    }

    public abstract string Name
    {
        get;
    }

    protected abstract string CapabilityTitle
    {
        get;
    }

    protected abstract string Description
    {
        get;
    }

    protected abstract IReadOnlyList<string>
        RuntimeOperationLines
    {
        get;
    }

    protected virtual void PrepareSimulation()
    {
    }

    protected abstract ProtocolMessage CreateRequest();

    protected abstract void WriteCapabilityResult(
        TResponse response);

    public void Execute()
    {
        PrepareSimulation();

        WriteCapabilityHeader();

        WriteSimulationState(
            "Simulation State Before");

        ProtocolMessage request =
            CreateRequest();

        WriteFlowStep(
            "Runtime Operation",
            RuntimeOperationLines);

        WriteTrace(
            "Protocol Request",
            request);

        WriteFlowStep(
            "Transport and Runtime Flow",
            [
                "ProtocolClient",
                "ProtocolEnvelopeByteCodec",
                "LoopbackProtocolTransport",
                "BinaryProtocolPayloadCodec",
                "RuntimeProtocolDispatcher",
                "EnvironmentControllerInstrumentExecutor",
                "EnvironmentControllerSimulation"
            ]);

        ProtocolExchangeResult exchange =
            Host.Client
                .SendAsync(request)
                .GetAwaiter()
                .GetResult();

        WriteFrame(
            "Request Frame Bytes",
            exchange.RequestFrame);

        WriteFrame(
            "Response Frame Bytes",
            exchange.ResponseFrame);

        WriteSimulationState(
            "Simulation State After");

        if (exchange.ResponseMessage is not TResponse response)
        {
            throw new InvalidDataException(
                $"Expected a {typeof(TResponse).Name}, but received " +
                $"'{exchange.ResponseMessage.GetType().Name}'.");
        }

        WriteTrace(
            "Protocol Response",
            response);

        WriteCapabilityResult(
            response);
    }

    protected static void WriteResultSection(
        params string[] lines)
    {
        const string title =
            "Capability Result";

        Console.WriteLine(title);

        Console.WriteLine(
            new string(
                '-',
                title.Length));

        Console.WriteLine();

        foreach (string line in lines)
        {
            Console.WriteLine(line);
        }

        Console.WriteLine();
    }

    protected static string FormatTemperature(
        double value)
    {
        return
            value.ToString(
                "0.0",
                CultureInfo.InvariantCulture)
            + " °C";
    }

    protected static string FormatValue(
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

    private void WriteCapabilityHeader()
    {
        Console.WriteLine(
            CapabilityTitle);

        Console.WriteLine(
            new string(
                '=',
                CapabilityTitle.Length));

        Console.WriteLine();

        Console.WriteLine(
            Description);

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
            $"{FormatTemperature(
                Host.ControllerState.TargetTemperature)}");

        Console.WriteLine();
    }

    private static void WriteFlowStep(
        string title,
        IReadOnlyList<string> lines)
    {
        Console.WriteLine(title);

        Console.WriteLine(
            new string(
                '-',
                title.Length));

        Console.WriteLine();

        for (int index = 0;
            index < lines.Count;
            index++)
        {
            Console.WriteLine(
                lines[index]);

            if (index < lines.Count - 1)
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
            Host.TraceGenerator.Generate(
                message);

        _consoleFormatter.Write(
            trace);
    }

    private void WriteFrame(
        string title,
        ReadOnlyMemory<byte> frame)
    {
        Console.WriteLine(title);

        Console.WriteLine(
            new string(
                '-',
                title.Length));

        Console.WriteLine();

        Console.WriteLine(
            $"Frame Length : {frame.Length} bytes");

        Console.WriteLine();

        IReadOnlyList<string> lines =
            _hexDumpFormatter.Format(
                frame.Span);

        foreach (string line in lines)
        {
            Console.WriteLine(line);
        }

        Console.WriteLine();
    }
}