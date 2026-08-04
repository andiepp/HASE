using System.Text;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Transport.Attachment;
using Hase.Transport.Serial;

namespace Hase.Scpi.Kel103.Hosting.Tests;

public sealed class Kel103DefinitionAwareFactoryTests
{
    [Theory]
    [InlineData(2, 5, 0)]
    [InlineData(3, 11, 0)]
    [InlineData(4, 11, 5)]
    public async Task OperationalFactory_MaterializesExactSuppliedDefinition(
        int version,
        int expectedProperties,
        int expectedCommands)
    {
        EndpointDescriptorDefinition definition = Definition(version);
        var stream = new ScriptedSerialByteStream(Responses(version).ToArray());
        var transport = new RecordingSerialFactory(stream);
        var factory = new Kel103OperationalConnectionFactory(
            new Hase.Runtime.Runtime.RuntimeContext(),
            transport,
            new FixedTimeProvider());

        await using Kel103OperationalConnection connection = await factory.OpenAsync(
            new EndpointId("kel-test-01"),
            definition,
            SupportedOptions());

        Assert.Equal(1, transport.OpenCount);
        Assert.Equal(expectedProperties, connection.RuntimeEndpoint.Instruments.Single().Properties.Count);
        Assert.Equal(expectedCommands, connection.RuntimeEndpoint.Instruments.Single().Commands.Count);
    }

    [Fact]
    public async Task PublishedFactory_VersionFourWriteUsesDefinitionAwarePath()
    {
        var stream = new ScriptedSerialByteStream(
            Responses(version: 4)
                .Concat(["OFF\n", "0.25A\n", "CC\n"])
                .ToArray());
        var context = new Hase.Runtime.Runtime.RuntimeContext();
        var factory = new Kel103PublishedAttachmentFactory(
            context,
            new RecordingSerialFactory(stream),
            new FixedTimeProvider());
        await using Kel103PublishedAttachment attachment = await factory.OpenAsync(
            new EndpointId("kel-test-01"),
            Kel103ControlledSetpointDefinition.EndpointDefinition,
            SupportedOptions());

        EndpointAttachmentPropertyOperationResult result = await attachment.PropertyOperations.WriteAsync(
            new InstrumentId("electronic-load-01"),
            Kel103SetpointMapping.Current.PropertyId,
            0.25m);

        Assert.True(result.IsSuccess);
        Assert.Equal(0.25m, result.ConfirmedValue!.Value);
        Assert.Equal(1, stream.Writes.Count(value => value == ":CURRent 0.25A\r"));
        Assert.Equal(11, attachment.RuntimeEndpoint.Instruments.Single().Properties.Count);
        Assert.Equal(5, attachment.RuntimeEndpoint.Instruments.Single().Commands.Count);
    }

    [Fact]
    public async Task OperationalFactory_RejectsUnrelatedDefinitionBeforeSerialOpen()
    {
        var transport = new RecordingSerialFactory(
            new ScriptedSerialByteStream());
        var factory = new Kel103OperationalConnectionFactory(
            new Hase.Runtime.Runtime.RuntimeContext(),
            transport);

        await Assert.ThrowsAsync<InvalidDataException>(() => factory.OpenAsync(
            new EndpointId("kel-test-01"),
            Kel103IdentityDefinition.EndpointDefinition,
            SupportedOptions()));

        Assert.Equal(0, transport.OpenCount);
    }

    private static EndpointDescriptorDefinition Definition(int version) => version switch
    {
        2 => Kel103ReadOnlyMeasurementDefinition.EndpointDefinition,
        3 => Kel103OperatingStateDefinition.EndpointDefinition,
        4 => Kel103ControlledSetpointDefinition.EndpointDefinition,
        _ => throw new ArgumentOutOfRangeException(nameof(version))
    };

    private static IEnumerable<string> Responses(int version)
    {
        yield return "RND 320-KEL103 V3.30 SN:REDACTED\n";
        yield return "0.0000V\n";
        yield return "0.0000A\n";
        yield return "0.0000W\n";
        if (version == 2)
        {
            yield break;
        }

        yield return "CC\n";
        yield return "OFF\n";
        yield return "0.1000V\n";
        yield return "0.1000A\n";
        yield return "0.1000OHM\n";
        yield return "0.1000W\n";
    }

    private static SerialTransportOptions SupportedOptions() =>
        new(
            "TEST-PORT",
            115200,
            8,
            SerialParity.None,
            SerialStopBits.One,
            SerialHandshake.None);

    private sealed class RecordingSerialFactory(ISerialByteStream stream)
        : ISerialByteStreamFactory
    {
        public int OpenCount { get; private set; }

        public ValueTask<ISerialByteStream> OpenAsync(
            SerialTransportOptions options,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpenCount++;
            return ValueTask.FromResult(stream);
        }
    }

    private sealed class ScriptedSerialByteStream(params string[] responses)
        : ISerialByteStream
    {
        private readonly Queue<byte[]> pending = new(
            responses.Select(Encoding.ASCII.GetBytes));

        public List<string> Writes { get; } = [];

        public ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            byte[] response = pending.Dequeue();
            response.AsSpan().CopyTo(buffer.Span);
            return ValueTask.FromResult(response.Length);
        }

        public ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Writes.Add(Encoding.ASCII.GetString(buffer.Span));
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new(2026, 8, 4, 19, 0, 0, TimeSpan.Zero);
    }
}
