using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Protocol;
using Hase.ProtocolExplorer.Transport;
using Hase.Transport;
using Hase.Transport.Tcp;

namespace Hase.ProtocolExplorer.Scenarios;

/// <summary>
/// Validates physical Boolean WriteProperty behavior against the ESP32 GPIO16
/// status LED.
/// </summary>
internal sealed class CapabilityC008Scenario
    : IParameterizedScenario
{
    private const int TcpPort =
        5000;

    private const int MaximumPayloadLength =
        4096;

    private static readonly TimeSpan ConnectionTimeout =
        TimeSpan.FromSeconds(
            3);

    private static readonly TimeSpan MaximumTimestampDifference =
        TimeSpan.FromMinutes(
            2);

    public string Name =>
        "c008";

    public void Execute(
        IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(
            arguments);

        ExecuteAsync(
                arguments)
            .GetAwaiter()
            .GetResult();
    }

    private static async Task ExecuteAsync(
        IReadOnlyList<string> arguments)
    {
        if (arguments.Count != 1)
        {
            throw new ArgumentException(
                "Capability C-008 requires exactly one argument: "
                + "the ESP32 host name or IP address.",
                nameof(arguments));
        }

        string host =
            arguments[0];

        WriteHeader(
            host);

        var options =
            new TcpTransportOptions(
                host,
                TcpPort,
                ConnectionTimeout);

        ITransportFactory factory =
            new TcpTransportFactory(
                options,
                MaximumPayloadLength);

        Console.WriteLine(
            "Establishing TCP connection...");

        ITransportConnection connection =
            await factory.ConnectAsync();

        try
        {
            Console.WriteLine(
                "TCP connection established.");

            Console.WriteLine();

            var client =
                new ProtocolClient(
                    connection);

            await WriteBooleanAsync(
                client,
                new CorrelationId(
                    208),
                expectedValue:
                    true,
                step:
                    "1. Enable the status LED");

            await ReadBooleanAsync(
                client,
                new CorrelationId(
                    209),
                expectedValue:
                    true,
                step:
                    "2. Verify the enabled state");

            await WriteBooleanAsync(
                client,
                new CorrelationId(
                    210),
                expectedValue:
                    false,
                step:
                    "3. Disable the status LED");

            await ReadBooleanAsync(
                client,
                new CorrelationId(
                    211),
                expectedValue:
                    false,
                step:
                    "4. Verify the disabled state");

            WriteSuccess();
        }
        finally
        {
            if (connection
                is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else if (connection
                     is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    private static async Task WriteBooleanAsync(
        ProtocolClient client,
        CorrelationId correlationId,
        bool expectedValue,
        string step)
    {
        WriteStepHeader(
            step);

        var request =
            new WritePropertyRequest(
                correlationId,
                PhysicalEnvironmentEndpointDescriptorFactory
                    .ControllerInstrumentId,
                PhysicalEnvironmentEndpointDescriptorFactory
                    .StatusLedEnabledPropertyId,
                expectedValue);

        ProtocolExchangeResult exchange =
            await client.SendAsync(
                request);

        WritePropertyResponse response =
            exchange.ResponseMessage
                as WritePropertyResponse
            ?? throw new InvalidDataException(
                "The ESP32 did not return a "
                + "WritePropertyResponse.");

        PropertyValue propertyValue =
            ValidatePropertyValue(
                response.CorrelationId,
                correlationId,
                response.Result,
                response.PropertyValue,
                expectedValue,
                "WriteProperty");

        WriteExchangeResult(
            exchange,
            propertyValue,
            expectedValue);
    }

    private static async Task ReadBooleanAsync(
        ProtocolClient client,
        CorrelationId correlationId,
        bool expectedValue,
        string step)
    {
        WriteStepHeader(
            step);

        var request =
            new ReadPropertyRequest(
                correlationId,
                PhysicalEnvironmentEndpointDescriptorFactory
                    .ControllerInstrumentId,
                PhysicalEnvironmentEndpointDescriptorFactory
                    .StatusLedEnabledPropertyId);

        ProtocolExchangeResult exchange =
            await client.SendAsync(
                request);

        ReadPropertyResponse response =
            exchange.ResponseMessage
                as ReadPropertyResponse
            ?? throw new InvalidDataException(
                "The ESP32 did not return a "
                + "ReadPropertyResponse.");

        PropertyValue propertyValue =
            ValidatePropertyValue(
                response.CorrelationId,
                correlationId,
                response.Result,
                response.PropertyValue,
                expectedValue,
                "ReadProperty");

        WriteExchangeResult(
            exchange,
            propertyValue,
            expectedValue);
    }

    private static PropertyValue ValidatePropertyValue(
        CorrelationId actualCorrelationId,
        CorrelationId expectedCorrelationId,
        ProtocolResult result,
        PropertyValue? propertyValue,
        bool expectedValue,
        string operation)
    {
        if (actualCorrelationId
            != expectedCorrelationId)
        {
            throw new InvalidDataException(
                $"{operation} response correlation identifier "
                + "does not match its request.");
        }

        if (!result.IsSuccess)
        {
            throw new InvalidDataException(
                $"{operation} failed with result "
                + $"'{result.Code}': "
                + $"{result.Message ?? "(no message)"}.");
        }

        PropertyValue value =
            propertyValue
            ?? throw new InvalidDataException(
                $"The successful {operation} response did not "
                + "contain a property value.");

        if (value.Value
            is not bool booleanValue)
        {
            string actualType =
                value.Value?.GetType().FullName
                ?? "null";

            throw new InvalidDataException(
                $"{operation} returned a non-Boolean value. "
                + $"Actual type: '{actualType}'.");
        }

        if (booleanValue
            != expectedValue)
        {
            throw new InvalidDataException(
                $"{operation} returned '{booleanValue}', "
                + $"but '{expectedValue}' was expected.");
        }

        if (value.Quality
            != PropertyQuality.Good)
        {
            throw new InvalidDataException(
                $"{operation} property quality must be Good, "
                + $"but was '{value.Quality}'.");
        }

        if (value.TimestampUtc.Offset
            != TimeSpan.Zero)
        {
            throw new InvalidDataException(
                $"{operation} timestamp is not expressed in UTC.");
        }

        TimeSpan timestampDifference =
            (DateTimeOffset.UtcNow
             - value.TimestampUtc)
            .Duration();

        if (timestampDifference
            > MaximumTimestampDifference)
        {
            throw new InvalidDataException(
                $"{operation} timestamp differs from current UTC "
                + "by "
                + $"{timestampDifference.TotalSeconds:0.000} seconds.");
        }

        return value;
    }

    private static void WriteHeader(
        string host)
    {
        const string title =
            "Capability C-008";

        Console.WriteLine(
            title);

        Console.WriteLine(
            new string(
                '=',
                title.Length));

        Console.WriteLine();

        Console.WriteLine(
            "Write and read the physical ESP32 GPIO16 active-low "
            + "status LED through HASE Protocol Version 1 over "
            + "framed TCP.");

        Console.WriteLine();

        Console.WriteLine(
            $"Host          : {host}");

        Console.WriteLine(
            $"Port          : {TcpPort}");

        Console.WriteLine(
            $"Instrument ID : "
            + $"{PhysicalEnvironmentEndpointDescriptorFactory.ControllerInstrumentId}");

        Console.WriteLine(
            $"Property ID   : "
            + $"{PhysicalEnvironmentEndpointDescriptorFactory.StatusLedEnabledPropertyId}");

        Console.WriteLine(
            "GPIO          : 16");

        Console.WriteLine(
            "Active level  : Low");

        Console.WriteLine();

        Console.WriteLine(
            "Expected electrical behavior:");

        Console.WriteLine(
            "  True  -> GPIO16 Low  -> LED On");

        Console.WriteLine(
            "  False -> GPIO16 High -> LED Off");

        Console.WriteLine();
    }

    private static void WriteStepHeader(
        string step)
    {
        Console.WriteLine(
            step);

        Console.WriteLine(
            new string(
                '-',
                step.Length));

        Console.WriteLine();
    }

    private static void WriteExchangeResult(
        ProtocolExchangeResult exchange,
        PropertyValue propertyValue,
        bool expectedValue)
    {
        Console.WriteLine(
            $"Request type    : "
            + $"{exchange.RequestEnvelope.MessageType}");

        Console.WriteLine(
            $"Response type   : "
            + $"{exchange.ResponseMessage.MessageType}");

        Console.WriteLine(
            $"Correlation ID  : "
            + $"{exchange.ResponseMessage.CorrelationId}");

        Console.WriteLine(
            $"Expected value  : {expectedValue}");

        Console.WriteLine(
            $"Returned value  : {propertyValue.Value}");

        Console.WriteLine(
            $"Timestamp UTC   : {propertyValue.TimestampUtc:O}");

        Console.WriteLine(
            $"Quality         : {propertyValue.Quality}");

        Console.WriteLine(
            $"Request bytes   : {exchange.RequestFrame.Length}");

        Console.WriteLine(
            $"Response bytes  : {exchange.ResponseFrame.Length}");

        Console.WriteLine();
    }

    private static void WriteSuccess()
    {
        const string title =
            "Capability Result";

        Console.WriteLine(
            title);

        Console.WriteLine(
            new string(
                '-',
                title.Length));

        Console.WriteLine();

        Console.WriteLine(
            "Result             : Success");

        Console.WriteLine(
            "Write true         : Passed");

        Console.WriteLine(
            "Read true          : Passed");

        Console.WriteLine(
            "Write false        : Passed");

        Console.WriteLine(
            "Read false         : Passed");

        Console.WriteLine(
            "Final GPIO16 state : High");

        Console.WriteLine(
            "Final LED state    : Off");

        Console.WriteLine();
    }
}