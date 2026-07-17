using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Protocol;
using Hase.ProtocolExplorer.Transport;
using Hase.Transport;
using Hase.Transport.Tcp;

namespace Hase.ProtocolExplorer.Scenarios;

/// <summary>
/// Validates negative physical ExecuteCommand behavior against the ESP32
/// GPIO16 active-low status LED.
/// </summary>
internal sealed class CapabilityC011Scenario
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
        "c011";

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
                "Capability C-011 requires exactly one argument: "
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

            await EstablishDisabledStateAsync(
                client);

            await ValidateUnknownCommandAsync(
                client);

            await ValidateInvalidArgumentAsync(
                client);

            await ValidateLedRemainsDisabledAsync(
                client);

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

    private static async Task EstablishDisabledStateAsync(
        ProtocolClient client)
    {
        const string step =
            "1. Establish the disabled initial state";

        WriteStepHeader(
            step);

        var correlationId =
            new CorrelationId(
                1100);

        var request =
            new WritePropertyRequest(
                correlationId,
                PhysicalEnvironmentEndpointDescriptorFactory
                    .ControllerInstrumentId,
                PhysicalEnvironmentEndpointDescriptorFactory
                    .StatusLedEnabledPropertyId,
                false);

        ProtocolExchangeResult exchange =
            await client.SendAsync(
                request);

        WritePropertyResponse response =
            exchange.ResponseMessage
                as WritePropertyResponse
            ?? throw new InvalidDataException(
                "The ESP32 did not return a "
                + "WritePropertyResponse.");

        if (response.CorrelationId
            != correlationId)
        {
            throw new InvalidDataException(
                "The WriteProperty response correlation identifier "
                + "does not match its request.");
        }

        if (!response.Result.IsSuccess)
        {
            throw new InvalidDataException(
                "The initial WriteProperty request failed with result "
                + $"'{response.Result.Code}': "
                + $"{response.Result.Message ?? "(no message)"}.");
        }

        PropertyValue propertyValue =
            response.PropertyValue
            ?? throw new InvalidDataException(
                "The successful WriteProperty response did not "
                + "contain a property value.");

        ValidateDisabledPropertyValue(
            propertyValue,
            "WriteProperty");

        Console.WriteLine(
            $"Request type    : "
            + $"{exchange.RequestEnvelope.MessageType}");

        Console.WriteLine(
            $"Response type   : "
            + $"{exchange.ResponseMessage.MessageType}");

        Console.WriteLine(
            $"Correlation ID  : {response.CorrelationId}");

        Console.WriteLine(
            $"Returned value  : {propertyValue.Value}");

        Console.WriteLine(
            "GPIO16 state    : High");

        Console.WriteLine(
            "LED state       : Off");

        Console.WriteLine();
    }

    private static async Task ValidateUnknownCommandAsync(
        ProtocolClient client)
    {
        const string step =
            "2. Reject an unknown command";

        WriteStepHeader(
            step);

        var correlationId =
            new CorrelationId(
                1101);

        var request =
            new ExecuteCommandRequest(
                correlationId,
                PhysicalEnvironmentEndpointDescriptorFactory
                    .ControllerInstrumentId,
                new DescriptorPath(
                    "Controller",
                    "UnknownCommand"),
                null);

        ProtocolExchangeResult exchange =
            await client.SendAsync(
                request);

        ExecuteCommandResponse response =
            ValidateFailureResponse(
                exchange,
                correlationId,
                ProtocolResultCode.NotFound);

        WriteFailureResult(
            exchange,
            response,
            "<null>");
    }

    private static async Task ValidateInvalidArgumentAsync(
        ProtocolClient client)
    {
        const string step =
            "3. Reject a non-null argument";

        WriteStepHeader(
            step);

        var correlationId =
            new CorrelationId(
                1102);

        var request =
            new ExecuteCommandRequest(
                correlationId,
                PhysicalEnvironmentEndpointDescriptorFactory
                    .ControllerInstrumentId,
                PhysicalEnvironmentEndpointDescriptorFactory
                    .ToggleStatusLedCommandPath,
                true);

        ProtocolExchangeResult exchange =
            await client.SendAsync(
                request);

        ExecuteCommandResponse response =
            ValidateFailureResponse(
                exchange,
                correlationId,
                ProtocolResultCode.InvalidRequest);

        WriteFailureResult(
            exchange,
            response,
            "True");
    }

    private static async Task ValidateLedRemainsDisabledAsync(
        ProtocolClient client)
    {
        const string step =
            "4. Verify the LED remains disabled";

        WriteStepHeader(
            step);

        var correlationId =
            new CorrelationId(
                1103);

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

        if (response.CorrelationId
            != correlationId)
        {
            throw new InvalidDataException(
                "The ReadProperty response correlation identifier "
                + "does not match its request.");
        }

        if (!response.Result.IsSuccess)
        {
            throw new InvalidDataException(
                "The final ReadProperty request failed with result "
                + $"'{response.Result.Code}': "
                + $"{response.Result.Message ?? "(no message)"}.");
        }

        PropertyValue propertyValue =
            response.PropertyValue
            ?? throw new InvalidDataException(
                "The successful ReadProperty response did not "
                + "contain a property value.");

        ValidateDisabledPropertyValue(
            propertyValue,
            "ReadProperty");

        Console.WriteLine(
            $"Request type    : "
            + $"{exchange.RequestEnvelope.MessageType}");

        Console.WriteLine(
            $"Response type   : "
            + $"{exchange.ResponseMessage.MessageType}");

        Console.WriteLine(
            $"Correlation ID  : {response.CorrelationId}");

        Console.WriteLine(
            $"Returned value  : {propertyValue.Value}");

        Console.WriteLine(
            $"Timestamp UTC   : {propertyValue.TimestampUtc:O}");

        Console.WriteLine(
            $"Quality         : {propertyValue.Quality}");

        Console.WriteLine(
            "GPIO16 state    : High");

        Console.WriteLine(
            "LED state       : Off");

        Console.WriteLine();
    }

    private static ExecuteCommandResponse ValidateFailureResponse(
        ProtocolExchangeResult exchange,
        CorrelationId expectedCorrelationId,
        ProtocolResultCode expectedResultCode)
    {
        ExecuteCommandResponse response =
            exchange.ResponseMessage
                as ExecuteCommandResponse
            ?? throw new InvalidDataException(
                "The ESP32 did not return an "
                + "ExecuteCommandResponse.");

        if (response.CorrelationId
            != expectedCorrelationId)
        {
            throw new InvalidDataException(
                "The ExecuteCommand response correlation identifier "
                + "does not match its request.");
        }

        if (response.Result.Code
            != expectedResultCode)
        {
            throw new InvalidDataException(
                $"Expected result '{expectedResultCode}', but "
                + $"received '{response.Result.Code}': "
                + $"{response.Result.Message ?? "(no message)"}.");
        }

        if (response.ReturnValue is not null)
        {
            throw new InvalidDataException(
                "A failed ExecuteCommand response must contain "
                + "a null return value.");
        }

        return response;
    }

    private static void ValidateDisabledPropertyValue(
        PropertyValue propertyValue,
        string operation)
    {
        if (propertyValue.Value
            is not bool enabled)
        {
            string actualType =
                propertyValue.Value?.GetType().FullName
                ?? "null";

            throw new InvalidDataException(
                $"{operation} returned a non-Boolean value. "
                + $"Actual type: '{actualType}'.");
        }

        if (enabled)
        {
            throw new InvalidDataException(
                $"{operation} reported that the LED was enabled "
                + "after a rejected command.");
        }

        if (propertyValue.Quality
            != PropertyQuality.Good)
        {
            throw new InvalidDataException(
                $"{operation} property quality must be Good, "
                + $"but was '{propertyValue.Quality}'.");
        }

        if (propertyValue.TimestampUtc.Offset
            != TimeSpan.Zero)
        {
            throw new InvalidDataException(
                $"{operation} timestamp is not expressed in UTC.");
        }

        TimeSpan timestampDifference =
            (DateTimeOffset.UtcNow
             - propertyValue.TimestampUtc)
            .Duration();

        if (timestampDifference
            > MaximumTimestampDifference)
        {
            throw new InvalidDataException(
                $"{operation} timestamp differs from current UTC "
                + "by "
                + $"{timestampDifference.TotalSeconds:0.000} seconds.");
        }
    }

    private static void WriteHeader(
        string host)
    {
        const string title =
            "Capability C-011";

        Console.WriteLine(
            title);

        Console.WriteLine(
            new string(
                '=',
                title.Length));

        Console.WriteLine();

        Console.WriteLine(
            "Validate rejected physical ExecuteCommand operations "
            + "against the DOIT ESP32 DEVKITC V4 GPIO16 active-low "
            + "status LED.");

        Console.WriteLine();

        Console.WriteLine(
            $"Host          : {host}");

        Console.WriteLine(
            $"Port          : {TcpPort}");

        Console.WriteLine(
            $"Instrument ID : "
            + $"{PhysicalEnvironmentEndpointDescriptorFactory.ControllerInstrumentId}");

        Console.WriteLine(
            $"Command path  : "
            + $"{PhysicalEnvironmentEndpointDescriptorFactory.ToggleStatusLedCommandPath}");

        Console.WriteLine(
            $"Property ID   : "
            + $"{PhysicalEnvironmentEndpointDescriptorFactory.StatusLedEnabledPropertyId}");

        Console.WriteLine(
            "Initial state : False / GPIO16 High / LED Off");

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

    private static void WriteFailureResult(
        ProtocolExchangeResult exchange,
        ExecuteCommandResponse response,
        string argument)
    {
        Console.WriteLine(
            $"Request type   : "
            + $"{exchange.RequestEnvelope.MessageType}");

        Console.WriteLine(
            $"Response type  : "
            + $"{exchange.ResponseMessage.MessageType}");

        Console.WriteLine(
            $"Correlation ID : {response.CorrelationId}");

        Console.WriteLine(
            $"Argument       : {argument}");

        Console.WriteLine(
            $"Result code    : {response.Result.Code}");

        Console.WriteLine(
            $"Result message : "
            + $"{response.Result.Message ?? "(no message)"}");

        Console.WriteLine(
            "Return value   : <null>");

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
            "Result                 : Success");

        Console.WriteLine(
            "Unknown command        : NotFound");

        Console.WriteLine(
            "Non-null argument      : InvalidRequest");

        Console.WriteLine(
            "Rejected-command effect: None");

        Console.WriteLine(
            "Final property value   : False");

        Console.WriteLine(
            "Final GPIO16 state     : High");

        Console.WriteLine(
            "Final LED state        : Off");

        Console.WriteLine();
    }
}