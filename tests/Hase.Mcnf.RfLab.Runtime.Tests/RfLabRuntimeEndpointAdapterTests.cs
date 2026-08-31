using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Runtime;

namespace Hase.Mcnf.RfLab.Runtime.Tests;

public sealed class RfLabRuntimeEndpointAdapterTests
{
    private static readonly InstrumentId Instrument = new("rf-minilab-01");

    private static RuntimeEndpoint CreateReadOnlyEndpoint(string id = "rflab-endpoint") =>
        new RuntimeContext().CreateEndpoint(
            RfLabReadOnlyDefinition.EndpointDefinition.Materialize(new EndpointId(id)));

    private static RuntimeEndpoint CreateControlledEndpoint(string id = "rflab-endpoint") =>
        new RuntimeContext().CreateEndpoint(
            RfLabControlledSignalDefinition.EndpointDefinition.Materialize(new EndpointId(id)));

    private static (RfLabRuntimeEndpointAdapter Adapter, ScriptedMcnfSession Session)
        CreateControlledAdapter()
    {
        var session = new ScriptedMcnfSession();
        var adapter = new RfLabRuntimeEndpointAdapter(
            new RfLabSessionAdapter(session),
            CreateControlledEndpoint());
        return (adapter, session);
    }

    private static void EnqueueSynchronization(ScriptedMcnfSession session)
    {
        session.EnqueueSuccess(0xAE, 0x70, 0x10, 0x80);
        session.EnqueueSuccess(0x00, 0x00, 0x00, 0b10);
        session.EnqueueSuccess(0x02, 0x9A);
    }

    [Fact]
    public void Constructor_AcceptsBothExactDefinitionsAndPreservesIdentity()
    {
        var readOnly = new RfLabRuntimeEndpointAdapter(
            new RfLabSessionAdapter(new ScriptedMcnfSession()),
            CreateReadOnlyEndpoint("read-only-endpoint"));
        Assert.Equal("read-only-endpoint", readOnly.RuntimeEndpoint.Descriptor.Id.Value);

        var controlled = new RfLabRuntimeEndpointAdapter(
            new RfLabSessionAdapter(new ScriptedMcnfSession()),
            CreateControlledEndpoint("controlled-endpoint"));
        Assert.Equal("controlled-endpoint", controlled.RuntimeEndpoint.Descriptor.Id.Value);
    }

    [Fact]
    public void Constructor_RejectsForeignEndpoints()
    {
        var endpoint = new RuntimeContext().CreateEndpoint(
            new Core.Domain.Descriptors.EndpointDescriptorDefinition(
                new Core.Domain.Endpoints.EndpointMetadata { DisplayName = "Foreign" },
                [
                    new Core.Domain.Instruments.InstrumentDescriptor(
                        new InstrumentId("foreign"),
                        "Foreign",
                        new Core.Domain.Instruments.InstrumentKind("Foreign"))
                ]).Materialize(new EndpointId("foreign-endpoint")));

        Assert.Throws<InvalidDataException>(
            () => new RfLabRuntimeEndpointAdapter(
                new RfLabSessionAdapter(new ScriptedMcnfSession()),
                endpoint));
    }

    [Fact]
    public async Task SynchronizeAsync_PopulatesReadStateAndStagedDefaults()
    {
        (RfLabRuntimeEndpointAdapter adapter, ScriptedMcnfSession session) =
            CreateControlledAdapter();
        EnqueueSynchronization(session);

        await adapter.SynchronizeAsync();

        RuntimeInstrument instrument = adapter.RuntimeEndpoint.Instruments.Single();
        Assert.Equal(
            "RF-Lab",
            ValueOf(instrument, RfLabProperties.ProductIdentity));
        Assert.Equal(
            "AE.70.10.80",
            ValueOf(instrument, RfLabProperties.NodeType));
        Assert.Equal(false, ValueOf(instrument, RfLabProperties.IndicatorEnabled));
        Assert.Equal(true, ValueOf(instrument, RfLabProperties.ClockGeneratorPresent));
        Assert.Equal(
            RfLabSensorConversion.MillivoltsFromAdcValue(0x029A),
            ValueOf(instrument, RfLabProperties.SensorVoltage));

        foreach (RfLabTargetMapping mapping in RfLabTargetMapping.All)
        {
            Assert.Equal(mapping.DefaultValue, ValueOf(instrument, mapping.PropertyId));
        }
    }

    [Fact]
    public async Task ReadAsync_ServesStagedTargetsWithoutTouchingTheSession()
    {
        (RfLabRuntimeEndpointAdapter adapter, ScriptedMcnfSession session) =
            CreateControlledAdapter();

        RuntimeProperty property = await adapter.ReadAsync(
            Instrument,
            RfLabTargetMapping.Frequency.PropertyId);

        Assert.Equal(
            RfLabTargetMapping.Frequency.DefaultValue,
            property.CurrentValue!.Value);
        Assert.Empty(session.Requests);
    }

    [Fact]
    public async Task ReadAsync_ReadsTheSensorThroughTheSession()
    {
        (RfLabRuntimeEndpointAdapter adapter, ScriptedMcnfSession session) =
            CreateControlledAdapter();
        session.EnqueueSuccess(0x03, 0xE8);

        RuntimeProperty property = await adapter.ReadAsync(
            Instrument,
            RfLabProperties.SensorLevel);

        Assert.Equal(
            RfLabSensorConversion.LevelFromMillivolts(2500.0),
            property.CurrentValue!.Value);
        Assert.Equal(0x20, Assert.Single(session.Requests).Function);
    }

    [Fact]
    public async Task ReadAsync_RejectsUnknownProperties()
    {
        (RfLabRuntimeEndpointAdapter adapter, _) = CreateControlledAdapter();

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => adapter.ReadAsync(Instrument, new PropertyId("unknown")));
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => adapter.ReadAsync(
                new InstrumentId("foreign"),
                RfLabProperties.SensorLevel));
    }

    [Fact]
    public async Task WriteAsync_StagesTargetsWithoutTouchingTheSession()
    {
        (RfLabRuntimeEndpointAdapter adapter, ScriptedMcnfSession session) =
            CreateControlledAdapter();

        RuntimeProperty property = await adapter.WriteAsync(
            Instrument,
            RfLabTargetMapping.Frequency.PropertyId,
            145_000_000);

        Assert.Equal(145_000_000.0, property.CurrentValue!.Value);
        Assert.Empty(session.Requests);
    }

    [Fact]
    public async Task WriteAsync_RejectsValuesOutsideTheCharacterizedRange()
    {
        (RfLabRuntimeEndpointAdapter adapter, _) = CreateControlledAdapter();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => adapter.WriteAsync(
                Instrument,
                RfLabTargetMapping.Frequency.PropertyId,
                99_999));
        await Assert.ThrowsAsync<ArgumentException>(
            () => adapter.WriteAsync(
                Instrument,
                RfLabTargetMapping.Frequency.PropertyId,
                double.NaN));
        await Assert.ThrowsAsync<ArgumentException>(
            () => adapter.WriteAsync(
                Instrument,
                RfLabTargetMapping.Frequency.PropertyId,
                "text"));
    }

    [Fact]
    public async Task WriteAsync_RejectsWritesOnTheReadOnlyDefinition()
    {
        var adapter = new RfLabRuntimeEndpointAdapter(
            new RfLabSessionAdapter(new ScriptedMcnfSession()),
            CreateReadOnlyEndpoint());

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => adapter.WriteAsync(
                Instrument,
                RfLabTargetMapping.Frequency.PropertyId,
                10_000_000));
    }

    [Fact]
    public async Task ExecuteAsync_AppliesTheStagedCarrierTargets()
    {
        (RfLabRuntimeEndpointAdapter adapter, ScriptedMcnfSession session) =
            CreateControlledAdapter();
        await adapter.WriteAsync(
            Instrument, RfLabTargetMapping.Frequency.PropertyId, 10_000_000);
        await adapter.WriteAsync(
            Instrument, RfLabTargetMapping.Attenuation.PropertyId, 10);
        session.EnqueueSuccess();

        await adapter.ExecuteAsync(
            Instrument,
            RfLabCommandMapping.ApplyCarrier.CommandPath,
            argument: null);

        Assert.Equal(
            RfLabProtocol.CreateCarrierRequest(10_000_000, 10).Bytes.ToArray(),
            Assert.Single(session.Requests).Bytes.ToArray());
    }

    [Fact]
    public async Task ExecuteAsync_AppliesTheStagedSweepTargetsWithTheMappedMode()
    {
        (RfLabRuntimeEndpointAdapter adapter, ScriptedMcnfSession session) =
            CreateControlledAdapter();
        session.EnqueueSuccess();

        await adapter.ExecuteAsync(
            Instrument,
            RfLabCommandMapping.StartSweepSingleRamp.CommandPath,
            argument: null);

        Assert.Equal(
            RfLabProtocol.CreateSweepRequest(
                10_000_000,
                30_000_000,
                2_000,
                20,
                RfLabSweepMode.SingleRamp).Bytes.ToArray(),
            Assert.Single(session.Requests).Bytes.ToArray());
    }

    [Fact]
    public async Task ExecuteAsync_AppliesTheStagedClockTargetOfTheMappedChannel()
    {
        (RfLabRuntimeEndpointAdapter adapter, ScriptedMcnfSession session) =
            CreateControlledAdapter();
        await adapter.WriteAsync(
            Instrument, RfLabTargetMapping.Clock1Frequency.PropertyId, 12_345_678);
        session.EnqueueSuccess();

        await adapter.ExecuteAsync(
            Instrument,
            RfLabCommandMapping.ApplyClock1.CommandPath,
            argument: null);

        Assert.Equal(
            RfLabProtocol.CreateClockRequest(1, 12_345_678).Bytes.ToArray(),
            Assert.Single(session.Requests).Bytes.ToArray());
    }

    [Fact]
    public async Task ExecuteAsync_UpdatesTheIndicatorPropertyFromTheReadback()
    {
        (RfLabRuntimeEndpointAdapter adapter, ScriptedMcnfSession session) =
            CreateControlledAdapter();
        session.EnqueueSuccess(0x01);

        await adapter.ExecuteAsync(
            Instrument,
            RfLabCommandMapping.IndicatorOn.CommandPath,
            argument: null);

        RuntimeInstrument instrument = adapter.RuntimeEndpoint.Instruments.Single();
        Assert.Equal(true, ValueOf(instrument, RfLabProperties.IndicatorEnabled));
    }

    [Fact]
    public async Task ExecuteAsync_RejectsArguments()
    {
        (RfLabRuntimeEndpointAdapter adapter, ScriptedMcnfSession session) =
            CreateControlledAdapter();

        await Assert.ThrowsAsync<ArgumentException>(
            () => adapter.ExecuteAsync(
                Instrument,
                RfLabCommandMapping.ApplyCarrier.CommandPath,
                argument: true));
        Assert.Empty(session.Requests);
    }

    [Fact]
    public async Task ExecuteAsync_RejectsCommandsOnTheReadOnlyDefinition()
    {
        var adapter = new RfLabRuntimeEndpointAdapter(
            new RfLabSessionAdapter(new ScriptedMcnfSession()),
            CreateReadOnlyEndpoint());

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => adapter.ExecuteAsync(
                Instrument,
                RfLabCommandMapping.ApplyCarrier.CommandPath,
                argument: null));
    }

    private static object ValueOf(RuntimeInstrument instrument, PropertyId propertyId) =>
        instrument.Properties
            .Single(property => property.Descriptor.Id == propertyId)
            .CurrentValue!
            .Value!;
}
