using Hase.Core.Domain.Identity;
using Hase.Protocol;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport;

namespace Hase.ProtocolExplorer.Scenarios;

/// <summary>
/// Runs recurring duplex-safe protocol health probes against the physical
/// environment endpoint.
/// </summary>
internal static class PhysicalRuntimeEndpointProbeLoop
{
    public static async Task RunAsync(
        RuntimeEndpointConnectionCoordinator coordinator,
        RuntimeEndpoint runtimeEndpoint,
        TimeSpan probeInterval,
        TimeSpan probeTimeout,
        uint initialCorrelationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            coordinator);

        ArgumentNullException.ThrowIfNull(
            runtimeEndpoint);

        if (probeInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(probeInterval),
                probeInterval,
                "The probe interval must be positive.");
        }

        if (probeTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(probeTimeout),
                probeTimeout,
                "The probe timeout must be positive.");
        }

        uint correlationIdValue =
            initialCorrelationId;

        while (true)
        {
            await Task.Delay(
                probeInterval,
                cancellationToken);

            if (runtimeEndpoint.ConnectionStatus.State
                != EndpointConnectionState.Ready)
            {
                continue;
            }

            correlationIdValue++;

            if (correlationIdValue
                == CorrelationId.None.Value)
            {
                correlationIdValue++;
            }

            var request =
                new ReadPropertyRequest(
                    new CorrelationId(
                        correlationIdValue),
                    PhysicalEnvironmentEndpointDescriptorFactory
                        .InstrumentId,
                    PhysicalEnvironmentEndpointDescriptorFactory
                        .TemperaturePropertyId);

            try
            {
                ProtocolMessage responseMessage =
                    await coordinator.ProbeAsync(
                        request,
                        probeTimeout,
                        cancellationToken);

                ReadPropertyResponse response =
                    responseMessage
                        as ReadPropertyResponse
                    ?? throw new InvalidDataException(
                        "The physical health probe did not receive a "
                        + "ReadPropertyResponse.");

                if (!response.Result.IsSuccess)
                {
                    throw new InvalidDataException(
                        "The physical health probe was rejected with "
                        + $"result '{response.Result.Code}': "
                        + $"{response.Result.Message ?? "(no message)"}.");
                }

                if (response.PropertyValue is null)
                {
                    throw new InvalidDataException(
                        "The successful physical health probe did not "
                        + "contain a property value.");
                }
            }
            catch (TimeoutException)
            {
                Console.WriteLine(
                    $"[{DateTimeOffset.UtcNow:O}] "
                    + "Protocol health probe timed out.");

                continue;
            }
            catch (InvalidOperationException)
                when (runtimeEndpoint.ConnectionStatus.State
                      != EndpointConnectionState.Ready)
            {
                continue;
            }
            catch (Exception)
                when (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(
                    cancellationToken);
            }
        }
    }
}