using Hase.Core.Domain.Commands;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Events;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Runtime;
using Hase.Scpi;

namespace Hase.Scpi.Kel103.Runtime.Tests;

public sealed class Kel103RuntimeEndpointInputControlTests
{
    [Fact]
    public void Constructor_AcceptsExactVersionFiveAndPreservesExternalIdentity()
    {
        var endpoint = CreateVersionFiveEndpoint("version-five-endpoint");
        var adapter = CreateAdapter(new FakeSession(), endpoint);

        Assert.Same(endpoint, adapter.RuntimeEndpoint);
        Assert.Equal("version-five-endpoint", adapter.RuntimeEndpoint.Descriptor.Id.Value);
    }

    [Fact]
    public void Constructor_RejectsAlteredVersionFiveContractsWithoutScpiUse()
    {
        InstrumentDescriptor expected =
            Kel103ControlledInputDefinition.EndpointDefinition.Instruments.Single();
        CommandDescriptor[] commands = expected.Interface.Commands.ToArray();
        var confirmation = new CommandArgumentDescriptor(
            "Confirmation",
            new BooleanDataDescriptor());
        var stringConfirmation = new CommandArgumentDescriptor(
            "Confirmation",
            new StringDataDescriptor());
        RuntimeEndpoint[] endpoints =
        [
            CreateVersionFiveEndpoint(commands: commands.Reverse().ToArray()),
            CreateVersionFiveEndpoint(commands: commands
                .Select((command, index) => index == 5
                    ? new CommandDescriptor(command.Path, command.DisplayName, confirmation)
                    : command).ToArray()),
            CreateVersionFiveEndpoint(commands: commands
                .Select((command, index) => index == 7
                    ? new CommandDescriptor(command.Path, command.DisplayName)
                    : command).ToArray()),
            CreateVersionFiveEndpoint(commands: commands
                .Select((command, index) => index == 7
                    ? new CommandDescriptor(command.Path, command.DisplayName, stringConfirmation)
                    : command).ToArray()),
            CreateVersionFiveEndpoint(properties: expected.Interface.Properties
                .Select((property, index) => index == 6
                    ? CopyProperty(property, path: DescriptorPath.Parse("Input.Other"))
                    : property).ToArray()),
            CreateVersionFiveEndpoint(events:
                [new EventDescriptor(DescriptorPath.Parse("State.Unexpected"), "Unexpected")])
        ];

        foreach (RuntimeEndpoint endpoint in endpoints)
        {
            var session = new FakeSession();
            Assert.Throws<InvalidDataException>(() => CreateAdapter(session, endpoint));
            Assert.Empty(session.Queries);
            Assert.Empty(session.Commands);
        }
    }

    [Fact]
    public async Task VersionFiveSynchronize_UsesUnchangedOperatingStatePath()
    {
        RuntimeEndpoint endpoint = CreateVersionFiveEndpoint();
        var session = CreateOperatingStateSession();
        await using var adapter = CreateAdapter(session, endpoint);

        RuntimeEndpoint result = await adapter.SynchronizeAsync();

        Assert.Same(endpoint, result);
        Assert.Equal(
            new object[]
            {
                "KEL-103", "V3.30", 9.8864m, 0.1000m, 0.9893m,
                "CC", false, 10.000m, 0.1000m, 100.00m, 1.000m
            },
            endpoint.Instruments.Single().Properties
                .Select(property => property.CurrentValue!.Value));
        Assert.Empty(session.Commands);
    }

    [Theory]
    [InlineData(0, "CC")]
    [InlineData(1, "CV")]
    [InlineData(2, "CR")]
    [InlineData(3, "CW")]
    [InlineData(4, "SHORt")]
    public async Task VersionFiveModeCommands_RetainVersionFourDispatch(
        int index,
        string readback)
    {
        RuntimeEndpoint endpoint = CreateVersionFiveEndpoint();
        RuntimeInstrument instrument = endpoint.Instruments.Single();
        var session = new FakeSession("OFF", "OFF", readback);
        await using var adapter = CreateAdapter(session, endpoint);
        Kel103ModeSelectionMapping mapping = Kel103ModeSelectionMapping.All[index];

        RuntimeCommand result = await adapter.ExecuteAsync(
            instrument.Descriptor.Id,
            mapping.CommandPath,
            argument: null);

        Assert.Same(instrument.Commands[index], result);
        Assert.Equal(
            Kel103OperatingModeMapping.ToNormalizedValue(mapping.Mode),
            instrument.Properties[5].CurrentValue!.Value);
        Assert.False(Assert.IsType<bool>(instrument.Properties[6].CurrentValue!.Value));
        Assert.Equal(new[] { mapping.Command }, session.Commands);
    }

    [Theory]
    [InlineData(0, "OFF", "CC", "ON", true)]
    [InlineData(1, "OFF", null, null, false)]
    public async Task OrdinaryInputControl_UpdatesOnlyAuthoritativeInputCache(
        int mappingIndex,
        string readback,
        string? mode,
        string? finalReadback,
        bool expectedInput)
    {
        RuntimeEndpoint endpoint = CreateVersionFiveEndpoint();
        RuntimeInstrument instrument = endpoint.Instruments.Single();
        RuntimeProperty modeProperty = instrument.Properties[5];
        RuntimeProperty inputProperty = instrument.Properties[6];
        RuntimeProperty unrelated = instrument.Properties[8];
        var previousMode = new PropertyValue("OLD", FixedTimestamp.AddMinutes(-1));
        var previousInput = new PropertyValue(!expectedInput, FixedTimestamp.AddMinutes(-1));
        var previousUnrelated = new PropertyValue(0.2m, FixedTimestamp.AddMinutes(-1));
        modeProperty.UpdateValue(previousMode);
        inputProperty.UpdateValue(previousInput);
        unrelated.UpdateValue(previousUnrelated);
        var responses = new[] { readback, mode, finalReadback }
            .Where(value => value is not null)
            .Cast<string>()
            .ToArray();
        var session = new FakeSession(responses);
        await using var adapter = CreateAdapter(session, endpoint);
        Kel103InputControlMapping mapping = Kel103InputControlMapping.All[mappingIndex];

        RuntimeCommand result = await adapter.ExecuteAsync(
            instrument.Descriptor.Id,
            mapping.CommandPath,
            argument: null);

        Assert.Same(instrument.Commands[5 + mappingIndex], result);
        Assert.Equal(expectedInput, inputProperty.CurrentValue!.Value);
        Assert.Equal(FixedTimestamp, inputProperty.CurrentValue.TimestampUtc);
        Assert.Equal(PropertyQuality.Good, inputProperty.CurrentValue.Quality);
        Assert.Same(previousMode, modeProperty.CurrentValue);
        Assert.Same(previousUnrelated, unrelated.CurrentValue);
        Assert.Equal(new[] { mapping.Command }, session.Commands);
    }

    [Fact]
    public async Task ConfirmedShortActivation_UpdatesOnlyAuthoritativeInputCache()
    {
        RuntimeEndpoint endpoint = CreateVersionFiveEndpoint();
        RuntimeInstrument instrument = endpoint.Instruments.Single();
        RuntimeProperty mode = instrument.Properties[5];
        RuntimeProperty input = instrument.Properties[6];
        RuntimeProperty unrelated = instrument.Properties[9];
        var previousMode = new PropertyValue("SHORT", FixedTimestamp.AddMinutes(-1));
        var previousInput = new PropertyValue(false, FixedTimestamp.AddMinutes(-1));
        var previousUnrelated = new PropertyValue(100m, FixedTimestamp.AddMinutes(-1));
        mode.UpdateValue(previousMode);
        input.UpdateValue(previousInput);
        unrelated.UpdateValue(previousUnrelated);
        var session = new FakeSession("OFF", "SHORt", "ON");
        await using var adapter = CreateAdapter(session, endpoint);

        RuntimeCommand result = await adapter.ExecuteAsync(
            instrument.Descriptor.Id,
            Kel103InputControlMapping.ShortCircuitActivate.CommandPath,
            argument: true);

        Assert.Same(instrument.Commands[7], result);
        Assert.True(Assert.IsType<bool>(input.CurrentValue!.Value));
        Assert.Equal(FixedTimestamp, input.CurrentValue.TimestampUtc);
        Assert.Same(previousMode, mode.CurrentValue);
        Assert.Same(previousUnrelated, unrelated.CurrentValue);
        Assert.Equal(new[] { ":INPut?", ":FUNCtion?", ":INPut?" }, session.Queries);
        Assert.Equal(new[] { ":INPut ON" }, session.Commands);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task OrdinaryInputControl_RejectsArgumentsWithoutScpi(int mappingIndex)
    {
        RuntimeEndpoint endpoint = CreateVersionFiveEndpoint();
        RuntimeInstrument instrument = endpoint.Instruments.Single();
        var session = new FakeSession();
        await using var adapter = CreateAdapter(session, endpoint);

        await Assert.ThrowsAsync<ArgumentException>(() => adapter.ExecuteAsync(
            instrument.Descriptor.Id,
            Kel103InputControlMapping.All[mappingIndex].CommandPath,
            argument: true));

        Assert.Empty(session.Queries);
        Assert.Empty(session.Commands);
        Assert.False(adapter.IsFaulted);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    [InlineData("true")]
    [InlineData(1)]
    public async Task ShortActivation_RejectsInvalidConfirmationWithoutScpi(object? argument)
    {
        RuntimeEndpoint endpoint = CreateVersionFiveEndpoint();
        RuntimeInstrument instrument = endpoint.Instruments.Single();
        var session = new FakeSession();
        await using var adapter = CreateAdapter(session, endpoint);

        await Assert.ThrowsAsync<ArgumentException>(() => adapter.ExecuteAsync(
            instrument.Descriptor.Id,
            Kel103InputControlMapping.ShortCircuitActivate.CommandPath,
            argument));

        Assert.Empty(session.Queries);
        Assert.Empty(session.Commands);
        Assert.False(adapter.IsFaulted);
    }

    [Theory]
    [InlineData("OFF", "SHORt", 0)]
    [InlineData("ON", null, 2)]
    [InlineData("OFF", "CC", 2)]
    public async Task InputActivation_PreconditionRejectionPreservesEveryCache(
        string input,
        string? mode,
        int mappingIndex)
    {
        RuntimeEndpoint endpoint = CreateVersionFiveEndpoint();
        RuntimeInstrument instrument = endpoint.Instruments.Single();
        PropertyValue?[] previous = SeedCaches(instrument);
        string[] responses = mode is null ? [input] : [input, mode];
        var session = new FakeSession(responses);
        await using var adapter = CreateAdapter(session, endpoint);
        Kel103InputControlMapping mapping = Kel103InputControlMapping.All[mappingIndex];
        object? argument = mapping == Kel103InputControlMapping.ShortCircuitActivate
            ? true
            : null;

        await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.ExecuteAsync(
            instrument.Descriptor.Id,
            mapping.CommandPath,
            argument));

        AssertCachesUnchanged(instrument, previous);
        Assert.Empty(session.Commands);
        Assert.False(adapter.IsFaulted);
    }

    [Theory]
    [InlineData(2, 0)]
    [InlineData(2, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 0)]
    [InlineData(3, 1)]
    [InlineData(3, 2)]
    [InlineData(4, 0)]
    [InlineData(4, 1)]
    [InlineData(4, 2)]
    public async Task VersionsTwoThroughFour_RejectInputControlCommandsWithoutScpi(
        int version,
        int mappingIndex)
    {
        RuntimeEndpoint endpoint = version switch
        {
            2 => new RuntimeContext().CreateEndpoint(
                Kel103ReadOnlyMeasurementDefinition.EndpointDefinition.Materialize(
                    new EndpointId("version-two-endpoint"))),
            3 => new RuntimeContext().CreateEndpoint(
                Kel103OperatingStateDefinition.EndpointDefinition.Materialize(
                    new EndpointId("version-three-endpoint"))),
            _ => CreateVersionFourEndpoint()
        };
        RuntimeInstrument instrument = endpoint.Instruments.Single();
        var session = new FakeSession();
        await using var adapter = CreateAdapter(session, endpoint);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => adapter.ExecuteAsync(
            instrument.Descriptor.Id,
            Kel103InputControlMapping.All[mappingIndex].CommandPath,
            mappingIndex == 2 ? true : null));

        Assert.Empty(session.Queries);
        Assert.Empty(session.Commands);
    }

    [Fact]
    public async Task InputControl_ReadbackUncertaintyPreservesCachesAndFaults()
    {
        RuntimeEndpoint endpoint = CreateVersionFiveEndpoint();
        RuntimeInstrument instrument = endpoint.Instruments.Single();
        PropertyValue?[] previous = SeedCaches(instrument);
        var session = new FakeSession("OFF", "CC", "OFF");
        await using var adapter = CreateAdapter(session, endpoint);

        await Assert.ThrowsAsync<Kel103MutationOutcomeUncertainException>(() =>
            adapter.ExecuteAsync(
                instrument.Descriptor.Id,
                Kel103InputControlMapping.Activate.CommandPath,
                argument: null));

        AssertCachesUnchanged(instrument, previous);
        Assert.True(adapter.IsFaulted);
        Assert.Single(session.Commands);
    }

    [Fact]
    public async Task InputControl_TransmissionUncertaintyPreservesCachesAndFaultsWithoutRetry()
    {
        RuntimeEndpoint endpoint = CreateVersionFiveEndpoint();
        RuntimeInstrument instrument = endpoint.Instruments.Single();
        PropertyValue?[] previous = SeedCaches(instrument);
        var transmission = new ScpiCommandTransmissionException(
            "uncertain",
            true,
            new IOException("write failed"));
        var session = new FakeSession("OFF", "CC") { SendException = transmission };
        await using var adapter = CreateAdapter(session, endpoint);

        ScpiCommandTransmissionException actual =
            await Assert.ThrowsAsync<ScpiCommandTransmissionException>(() =>
                adapter.ExecuteAsync(
                    instrument.Descriptor.Id,
                    Kel103InputControlMapping.Activate.CommandPath,
                    argument: null));

        Assert.Same(transmission, actual);
        AssertCachesUnchanged(instrument, previous);
        Assert.True(adapter.IsFaulted);
        Assert.Single(session.Commands);
        await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.ExecuteAsync(
            instrument.Descriptor.Id,
            Kel103InputControlMapping.Activate.CommandPath,
            argument: null));
        Assert.Single(session.Commands);
    }

    [Fact]
    public async Task InputControl_PreCancellationPreservesCachesWithoutScpi()
    {
        RuntimeEndpoint endpoint = CreateVersionFiveEndpoint();
        RuntimeInstrument instrument = endpoint.Instruments.Single();
        PropertyValue?[] previous = SeedCaches(instrument);
        var session = new FakeSession();
        await using var adapter = CreateAdapter(session, endpoint);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => adapter.ExecuteAsync(
            instrument.Descriptor.Id,
            Kel103InputControlMapping.Deactivate.CommandPath,
            argument: null,
            cancellationToken: cancellation.Token));

        AssertCachesUnchanged(instrument, previous);
        Assert.Empty(session.Queries);
        Assert.Empty(session.Commands);
        Assert.False(adapter.IsFaulted);
    }

    private static readonly DateTimeOffset FixedTimestamp =
        new(2026, 8, 6, 14, 0, 0, TimeSpan.Zero);

    private static RuntimeEndpoint CreateVersionFourEndpoint() =>
        new RuntimeContext().CreateEndpoint(
            Kel103ControlledSetpointDefinition.EndpointDefinition.Materialize(
                new EndpointId("version-four-endpoint")));

    private static RuntimeEndpoint CreateVersionFiveEndpoint(
        string id = "version-five-endpoint",
        IReadOnlyList<PropertyDescriptor>? properties = null,
        IReadOnlyList<CommandDescriptor>? commands = null,
        IReadOnlyList<EventDescriptor>? events = null)
    {
        InstrumentDescriptor expected =
            Kel103ControlledInputDefinition.EndpointDefinition.Instruments.Single();
        if (properties is null && commands is null && events is null)
        {
            return new RuntimeContext().CreateEndpoint(
                Kel103ControlledInputDefinition.EndpointDefinition.Materialize(
                    new EndpointId(id)));
        }

        var instrument = new InstrumentDescriptor(expected.Id, expected.Name, expected.Kind)
        {
            Metadata = expected.Metadata,
            Interface = new InstrumentInterface(
                properties ?? expected.Interface.Properties,
                commands ?? expected.Interface.Commands,
                events ?? expected.Interface.Events)
        };
        var descriptor = new EndpointDescriptor(new EndpointId(id), [instrument])
        {
            Metadata = Kel103ControlledInputDefinition.EndpointDefinition.Metadata
        };
        return new RuntimeContext().CreateEndpoint(descriptor);
    }

    private static FakeSession CreateOperatingStateSession() => new(
        "RND 320-KEL103 V3.30 SN:REDACTED",
        "9.8864V",
        "0.1000A",
        "0.9893W",
        "CC",
        "OFF",
        "10.000V",
        "0.1000A",
        "100.00OHM",
        "1.000W");

    private static PropertyDescriptor CopyProperty(
        PropertyDescriptor property,
        DescriptorPath? path = null) =>
        new(property.Id, path ?? property.Path, property.DisplayName, property.Data)
        {
            Description = property.Description,
            AccessMode = property.AccessMode
        };

    private static PropertyValue?[] SeedCaches(RuntimeInstrument instrument)
    {
        for (int index = 0; index < instrument.Properties.Count; index++)
        {
            object value = instrument.Properties[index].Descriptor.Data switch
            {
                BooleanDataDescriptor => false,
                NumericDataDescriptor => Convert.ToDecimal(index),
                _ => $"old-{index}"
            };
            instrument.Properties[index].UpdateValue(
                new PropertyValue(value, FixedTimestamp.AddMinutes(-1)));
        }

        return instrument.Properties.Select(property => property.CurrentValue).ToArray();
    }

    private static void AssertCachesUnchanged(
        RuntimeInstrument instrument,
        IReadOnlyList<PropertyValue?> previous)
    {
        for (int index = 0; index < instrument.Properties.Count; index++)
        {
            Assert.Same(previous[index], instrument.Properties[index].CurrentValue);
        }
    }

    private static Kel103RuntimeEndpointAdapter CreateAdapter(
        FakeSession session,
        RuntimeEndpoint endpoint) =>
        new(
            new Kel103ReadOnlySessionAdapter(session, new FixedTimeProvider()),
            endpoint,
            new FixedTimeProvider());

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => FixedTimestamp;
    }

    private sealed class FakeSession(params string[] responses) : IScpiTextSession
    {
        private readonly Queue<string> pending = new(responses);
        public List<string> Queries { get; } = [];
        public List<string> Commands { get; } = [];
        public Exception? SendException { get; init; }
        public ScpiTextSessionState State => ScpiTextSessionState.Open;

        public Task<string> QueryAsync(
            string query,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Queries.Add(query);
            return Task.FromResult(pending.Dequeue());
        }

        public Task SendCommandAsync(
            string command,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Commands.Add(command);
            return SendException is null
                ? Task.CompletedTask
                : Task.FromException(SendException);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
