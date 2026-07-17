using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Protocol;
using Hase.ProtocolExplorer.Transport;
using Hase.Transport;
using Hase.Transport.Tcp;

namespace Hase.ProtocolExplorer.Scenarios;

/// <summary>
/// Validates negative physical WriteProperty behavior against the ESP32 GPIO16
/// status LED.
/// </summary>
internal sealed class CapabilityC009Scenario
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
        "c009";

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
                "Capability C-009 requires exactly one argument: "
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

            await ValidateUnknownPropertyAsync(
                client);

            await ValidateInvalidValueTypeAsync(
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

    private static async Task ValidateUnknownPropertyAsync(
        ProtocolClient client)
    {
        const string step =
            "1. Reject an unknown property";

        WriteStepHeader(
            step);

        var correlationId =
            new CorrelationId(
                901);

        var request =
            new WritePropertyRequest(
                correlationId,
                PhysicalEnvironmentEndpointDescriptorFactory
                    .ControllerInstrumentId,
                new PropertyId(
                    "physical.controller.unknown"),
                true);

        ProtocolExchangeResult exchange =
            await client.SendAsync(
                request);

        WritePropertyResponse response =
            ValidateFailureResponse(
                exchange,
                correlationId,
                ProtocolResultCode.NotFound);

        WriteFailureResult(
            exchange,
            response);
    }

    private static async Task ValidateInvalidValueTypeAsync(
        ProtocolClient client)
    {
        const string step =
            "2. Reject a non-Boolean value";

        WriteStepHeader(
            step);

        var correlationId =
            new CorrelationId(
                902);

        var request =
            new WritePropertyRequest(
                correlationId,
                PhysicalEnvironmentEndpointDescriptorFactory
                    .ControllerInstrumentId,
                PhysicalEnvironmentEndpointDescriptorFactory
                    .StatusLedEnabledPropertyId,
                "true");

        ProtocolExchangeResult exchange =
            await client.SendAsync(
                request);

        WritePropertyResponse response =
            ValidateFailureResponse(
                exchange,
                correlationId,
                ProtocolResultCode.InvalidRequest);

        WriteFailureResult(
            exchange,
            response);
    }

    private static async Task ValidateLedRemainsDisabledAsync(
        ProtocolClient client)
    {
        const string step =
            "3. Verify the LED remains disabled";

        WriteStepHeader(
            step);

        var correlationId =
            new CorrelationId(
                903);

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
                "The successful ReadProperty response did not contain "
                + "a property value.");

        if (propertyValue.Value
            is not bool enabled)
        {
            throw new InvalidDataException(
                "The LED property did not contain a Boolean value.");
        }

        if (enabled)
        {
            throw new InvalidDataException(
                "A rejected write changed the LED property to true.");
        }

        ValidatePropertyValueMetadata(
            propertyValue);

        Console.WriteLine(
            $"Returned value  : {enabled}");

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

    private static WritePropertyResponse ValidateFailureResponse(
        ProtocolExchangeResult exchange,
        CorrelationId expectedCorrelationId,
        ProtocolResultCode expectedResultCode)
    {
        WritePropertyResponse response =
            exchange.ResponseMessage
                as WritePropertyResponse
            ?? throw new InvalidDataException(
                "The ESP32 did not return a "
                + "WritePropertyResponse.");

        if (response.CorrelationId
            != expectedCorrelationId)
        {
            throw new InvalidDataException(
                "The WriteProperty response correlation identifier "
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

        if (response.PropertyValue is not null)
        {
            throw new InvalidDataException(
                "A failed WriteProperty response must not contain "
                + "a property value.");
        }

        return response;
    }

    private static void ValidatePropertyValueMetadata(
        PropertyValue propertyValue)
    {
        if (propertyValue.Quality
            != PropertyQuality.Good)
        {
            throw new InvalidDataException(
                "The LED property quality must be Good, but was "
                + $"'{propertyValue.Quality}'.");
        }

        if (propertyValue.TimestampUtc.Offset
            != TimeSpan.Zero)
        {
            throw new InvalidDataException(
                "The LED property timestamp is not expressed in UTC.");
        }

        TimeSpan timestampDifference =
            (DateTimeOffset.UtcNow
             - propertyValue.TimestampUtc)
            .Duration();

        if (timestampDifference
            > MaximumTimestampDifference)
        {
            throw new InvalidDataException(
                "The LED property timestamp differs from current UTC "
                + "by "
                + $"{timestampDifference.TotalSeconds:0.000} seconds.");
        }
    }

    private static void WriteHeader(
        string host)
    {
        const string title =
            "Capability C-009";

        Console.WriteLine(
            title);

        Console.WriteLine(
            new string(
                '=',
                title.Length));

        Console.WriteLine();

        Console.WriteLine(
            "Validate rejected physical WriteProperty operations "
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
        WritePropertyResponse response)
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
            $"Result code    : {response.Result.Code}");

        Console.WriteLine(
            $"Result message : "
            + $"{response.Result.Message ?? "(no message)"}");

        Console.WriteLine(
            $"Property value : <none>");

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
            "Result                : Success");

        Console.WriteLine(
            "Unknown property       : NotFound");

        Console.WriteLine(
            "Invalid value type     : InvalidRequest");

        Console.WriteLine(
            "Rejected-write effect  : None");

        Console.WriteLine(
            "Final property value   : False");

        Console.WriteLine(
            "Final GPIO16 state     : High");

        Console.WriteLine(
            "Final LED state        : Off");

        Console.WriteLine();
    }
}