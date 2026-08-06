using System.Text;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Connections;
using Hase.Runtime.Diagnostics;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport;
using Hase.Runtime.Transport.Attachment;
using Hase.Transport.Serial;

namespace Hase.Scpi.Kel103.Hosting.Tests;

public sealed class Kel103SupervisedAttachmentFactoryTests
{
    [Fact]
    public async Task OpenAsync_ReadyAttachmentPerformsNoBackgroundScpiTraffic()
    {
        var collector = new BoundedRuntimeDiagnosticCollector(20);
        var context = new RuntimeContext(new RuntimeDiagnosticPublisher(collector));
        var stream = SuccessfulStream("9.0000V\n");
        var transport = new SequenceFactory(stream);
        var factory = new Kel103SupervisedAttachmentFactory(context, transport);

        Kel103SupervisedAttachment attachment = await factory.OpenAsync(
            new EndpointId("kel-test-01"),
            SupportedOptions());
        await Task.Yield();

        Assert.Single(context.Endpoints);
        Assert.Equal(EndpointConnectionState.Ready, attachment.RuntimeEndpoint.ConnectionStatus.State);
        Assert.Equal(1, transport.OpenCount);
        Assert.Equal(4, stream.WriteCount);
        Assert.Equal(
            RuntimeEndpointConnectionStatistics.Empty,
            attachment.GetConnectionStatistics());
        Assert.DoesNotContain(
            collector.GetSnapshot(),
            record => record.EventName == "RecoveryScheduled");

        await attachment.DisposeAsync();
        Assert.Empty(context.Endpoints);
        Assert.Equal(1, stream.DisposeCount);
    }

    [Fact]
    public async Task FaultedAttachment_IsRecoveredWithoutReplacingPublishedEndpoint()
    {
        var collector = new BoundedRuntimeDiagnosticCollector(30);
        var context = new RuntimeContext(new RuntimeDiagnosticPublisher(collector));
        var initial = SuccessfulStream("9.0000V\n");
        var replacement = SuccessfulStream("10.0000V\n");
        var transport = new SequenceFactory(initial, replacement);
        var factory = new Kel103SupervisedAttachmentFactory(context, transport);
        await using Kel103SupervisedAttachment attachment = await factory.OpenAsync(
            new EndpointId("kel-test-01"),
            SupportedOptions());
        RuntimeEndpoint endpoint = attachment.RuntimeEndpoint;
        object propertyPort = attachment.PropertyOperations;
        object commandPort = attachment.CommandOperations;

        endpoint.UpdateConnectionStatus(
            new EndpointConnectionStatus(EndpointConnectionState.Faulted));
        await WaitUntilAsync(() =>
            transport.OpenCount == 2
            && endpoint.ConnectionStatus.State == EndpointConnectionState.Ready);

        Assert.Same(endpoint, attachment.RuntimeEndpoint);
        Assert.Same(propertyPort, attachment.PropertyOperations);
        Assert.Same(commandPort, attachment.CommandOperations);
        Assert.Single(context.Endpoints);
        Assert.Same(endpoint, context.Endpoints.Single());
        Assert.Equal(1, initial.DisposeCount);
        Assert.Equal(0, replacement.DisposeCount);
        Assert.Equal(
            10.0000m,
            Assert.IsType<decimal>(endpoint.Instruments.Single().Properties.Single(
                property => property.Descriptor.Id == new PropertyId("measured-voltage"))
                .CurrentValue!.Value));
        RuntimeDiagnosticRecord scheduled = Assert.Single(
            collector.GetSnapshot(
                RuntimeDiagnosticLevel.Operational,
                RuntimeDiagnosticCategory.RuntimeRecovery)
            .Where(record => record.EventName == "RecoveryScheduled"));
        Assert.Equal("kel-test-01", scheduled.EndpointId);
        Assert.Null(scheduled.AttachmentGeneration);
        Assert.Equal("1", scheduled.Details["AttemptNumber"]);
        Assert.Equal("0", scheduled.Details["RetryIndex"]);
        Assert.Equal("0", scheduled.Details["DelayMilliseconds"]);
        RuntimeDiagnosticRecord[] synchronizationRecords = collector.GetSnapshot(
                RuntimeDiagnosticLevel.Operational,
                RuntimeDiagnosticCategory.RuntimeSynchronization)
            .Where(record => record.EventName.StartsWith(
                "InstrumentSynchronization",
                StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(4, synchronizationRecords.Length);
        Assert.Equal(2, synchronizationRecords.Select(record => record.OperationId).Distinct().Count());
    }

    [Theory]
    [InlineData("setpoint")]
    [InlineData("mode")]
    public async Task VersionFourUncertainMutation_RecoversByReadOnlySynchronizationWithoutReplay(
        string mutation)
    {
        var collector = new BoundedRuntimeDiagnosticCollector(40);
        var context = new RuntimeContext(new RuntimeDiagnosticPublisher(collector));
        var initial = new ScriptedSerialByteStream(
            VersionFourResponses()
                .Concat(MutationResponses(mutation))
                .ToArray());
        var replacement = new ScriptedSerialByteStream(
            VersionFourResponses(
                voltage: "1.0000V\n",
                current: "2.0000A\n",
                power: "3.0000W\n",
                mode: "CW\n",
                targetVoltage: "0.2000V\n",
                targetCurrent: "0.3000A\n",
                targetResistance: "0.4000OHM\n",
                targetPower: "0.5000W\n").ToArray());
        var transport = new SequenceFactory(initial, replacement);
        var factory = new Kel103SupervisedAttachmentFactory(context, transport);
        await using Kel103SupervisedAttachment attachment = await factory.OpenAsync(
            new EndpointId("kel-test-01"),
            Kel103ControlledSetpointDefinition.EndpointDefinition,
            SupportedOptions());
        RuntimeEndpoint endpoint = attachment.RuntimeEndpoint;
        object propertyPort = attachment.PropertyOperations;
        object commandPort = attachment.CommandOperations;

        if (mutation == "setpoint")
        {
            EndpointAttachmentPropertyOperationResult result =
                await attachment.PropertyOperations.WriteAsync(
                    new InstrumentId("electronic-load-01"),
                    Kel103SetpointMapping.Current.PropertyId,
                    0.25m);
            Assert.Equal(EndpointAttachmentPropertyOperationStatus.Unavailable, result.Status);
        }
        else
        {
            EndpointAttachmentCommandOperationResult result =
                await attachment.CommandOperations.ExecuteAsync(
                    new InstrumentId("electronic-load-01"),
                    Kel103ModeSelectionMapping.ConstantVoltage.CommandPath,
                    argument: null);
            Assert.Equal(EndpointAttachmentCommandOperationStatus.Unavailable, result.Status);
        }

        await WaitUntilAsync(() =>
            transport.OpenCount == 2
            && endpoint.ConnectionStatus.State == EndpointConnectionState.Ready);

        Assert.Same(endpoint, attachment.RuntimeEndpoint);
        Assert.Same(propertyPort, attachment.PropertyOperations);
        Assert.Same(commandPort, attachment.CommandOperations);
        Assert.Single(context.Endpoints);
        Assert.Same(endpoint, context.Endpoints.Single());
        Assert.Equal(11, endpoint.Instruments.Single().Properties.Count);
        Assert.Equal(5, endpoint.Instruments.Single().Commands.Count);
        Assert.Equal(
            ExpectedVersionFourQueries(),
            replacement.Writes);
        Assert.DoesNotContain(
            replacement.Writes,
            value => !value.EndsWith("?\r", StringComparison.Ordinal));
        Assert.Equal(
            mutation == "setpoint" ? 1 : 0,
            initial.Writes.Count(value => value == ":CURRent 0.25A\r"));
        Assert.Equal(
            mutation == "mode" ? 1 : 0,
            initial.Writes.Count(value => value == ":FUNCtion CV\r"));
        Assert.Equal(
            0.3000m,
            Assert.IsType<decimal>(endpoint.Instruments.Single().Properties.Single(
                property => property.Descriptor.Id == Kel103SetpointMapping.Current.PropertyId)
                .CurrentValue!.Value));
        Assert.Equal(
            "CW",
            endpoint.Instruments.Single().Properties.Single(
                property => property.Descriptor.Id == Kel103OperatingModeMapping.PropertyId)
                .CurrentValue!.Value);

        RuntimeDiagnosticRecord[] recoveryRecords = collector.GetSnapshot(
                RuntimeDiagnosticLevel.Operational,
                RuntimeDiagnosticCategory.RuntimeRecovery)
            .ToArray();
        Assert.Contains(recoveryRecords, record => record.EventName == "RecoveryScheduled");
        Assert.DoesNotContain(
            recoveryRecords.SelectMany(record => record.Details.Values),
            value => value.Contains("0.25", StringComparison.Ordinal));
        RuntimeDiagnosticRecord[] synchronizationRecords = collector.GetSnapshot(
                RuntimeDiagnosticLevel.Operational,
                RuntimeDiagnosticCategory.RuntimeSynchronization)
            .Where(record => record.EventName.StartsWith(
                "InstrumentSynchronization",
                StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(4, synchronizationRecords.Length);
        Assert.All(
            synchronizationRecords,
            record => Assert.Equal("4", record.Details["DefinitionVersion"]));
    }

    [Fact]
    public async Task OpenAsync_InitialFailureNeverPublishesAndClosesCandidate()
    {
        var context = new RuntimeContext();
        var stream = new ScriptedSerialByteStream("invalid\n");
        var transport = new SequenceFactory(stream);
        var factory = new Kel103SupervisedAttachmentFactory(context, transport);

        await Assert.ThrowsAsync<InvalidDataException>(() => factory.OpenAsync(
            new EndpointId("kel-test-01"),
            SupportedOptions()));

        Assert.Empty(context.Endpoints);
        Assert.Equal(1, transport.OpenCount);
        Assert.Equal(1, stream.DisposeCount);
    }

    private static SerialTransportOptions SupportedOptions() =>
        new("TEST-PORT", 115200);

    private static ScriptedSerialByteStream SuccessfulStream(string voltage) => new(
        "RND 320-KEL103 V3.30 SN:REDACTED\n",
        voltage,
        "0.1000A\n",
        "0.9000W\n");

    private static IEnumerable<string> VersionFourResponses(
        string voltage = "0.0000V\n",
        string current = "0.0000A\n",
        string power = "0.0000W\n",
        string mode = "CC\n",
        string targetVoltage = "0.1000V\n",
        string targetCurrent = "0.1000A\n",
        string targetResistance = "0.1000OHM\n",
        string targetPower = "0.1000W\n")
    {
        yield return "RND 320-KEL103 V3.30 SN:REDACTED\n";
        yield return voltage;
        yield return current;
        yield return power;
        yield return mode;
        yield return "OFF\n";
        yield return targetVoltage;
        yield return targetCurrent;
        yield return targetResistance;
        yield return targetPower;
    }

    private static IEnumerable<string> MutationResponses(string mutation) =>
        mutation switch
        {
            "setpoint" => ["OFF\n", "0.2000A\n", "CC\n"],
            "mode" => ["OFF\n", "OFF\n", "CC\n"],
            _ => throw new ArgumentOutOfRangeException(nameof(mutation))
        };

    private static string[] ExpectedVersionFourQueries() =>
    [
        "*IDN?\r",
        ":MEASure:VOLTage?\r",
        ":MEASure:CURRent?\r",
        ":MEASure:POWer?\r",
        ":FUNCtion?\r",
        ":INPut?\r",
        ":VOLTage?\r",
        ":CURRent?\r",
        ":RESistance?\r",
        ":POWer?\r"
    ];

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!predicate())
        {
            await Task.Delay(1, timeout.Token);
        }
    }

    private sealed class SequenceFactory(params ISerialByteStream[] streams)
        : ISerialByteStreamFactory
    {
        private int next;

        public int OpenCount => next;

        public ValueTask<ISerialByteStream> OpenAsync(
            SerialTransportOptions options,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (next >= streams.Length)
            {
                throw new InvalidOperationException("No scripted stream remains.");
            }

            return ValueTask.FromResult(streams[next++]);
        }
    }

    private sealed class ScriptedSerialByteStream(params string[] responses)
        : ISerialByteStream
    {
        private readonly Queue<byte[]> remaining = new(
            responses.Select(Encoding.ASCII.GetBytes));

        public int WriteCount { get; private set; }
        public int DisposeCount { get; private set; }
        public List<string> Writes { get; } = [];

        public ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!remaining.TryDequeue(out byte[]? response))
            {
                return ValueTask.FromResult(0);
            }

            response.AsSpan().CopyTo(buffer.Span);
            return ValueTask.FromResult(response.Length);
        }

        public ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteCount++;
            Writes.Add(Encoding.ASCII.GetString(buffer.Span));
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }
}
