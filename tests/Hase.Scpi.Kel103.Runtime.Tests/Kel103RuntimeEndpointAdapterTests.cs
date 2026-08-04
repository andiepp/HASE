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

public sealed class Kel103RuntimeEndpointAdapterTests
{
    [Fact]
    public void Constructor_AcceptsExactVersionTwoAndPreservesExternalIdentity()
    {
        var endpoint = CreateVersionTwoEndpoint("external-endpoint");
        var adapter = CreateAdapter(new FakeSession(), endpoint);
        Assert.Same(endpoint, adapter.RuntimeEndpoint);
        Assert.Equal("external-endpoint", adapter.RuntimeEndpoint.Descriptor.Id.Value);
    }

    [Fact]
    public void Constructor_AcceptsExactVersionThreeAndPreservesExternalIdentity()
    {
        var endpoint = CreateVersionThreeEndpoint("version-three-endpoint");
        var adapter = CreateAdapter(new FakeSession(), endpoint);

        Assert.Same(endpoint, adapter.RuntimeEndpoint);
        Assert.Equal("version-three-endpoint", adapter.RuntimeEndpoint.Descriptor.Id.Value);
    }

    [Fact]
    public void Constructor_RejectsIdentityOnlyEndpointWithoutQueries()
    {
        var session = new FakeSession();
        var endpoint = new RuntimeContext().CreateEndpoint(
            Kel103IdentityDefinition.EndpointDefinition.Materialize(new EndpointId("external-endpoint")));
        Assert.Throws<InvalidDataException>(() => CreateAdapter(session, endpoint));
        Assert.Empty(session.Queries);
    }

    [Fact]
    public void Constructor_RejectsVersionFourWithoutQueries()
    {
        var session = new FakeSession();
        var endpoint = new RuntimeContext().CreateEndpoint(
            Kel103ControlledSetpointDefinition.EndpointDefinition.Materialize(
                new EndpointId("external-endpoint")));

        Assert.Throws<InvalidDataException>(() => CreateAdapter(session, endpoint));
        Assert.Empty(session.Queries);
    }

    [Fact]
    public void Constructor_RejectsAlteredVersionThreeContractsWithoutQueries()
    {
        PropertyDescriptor target = Kel103OperatingStateDefinition.EndpointDefinition
            .Instruments.Single().Interface.Properties[7];
        var targetNumeric = Assert.IsType<NumericDataDescriptor>(target.Data);
        var alteredEndpoints = new[]
        {
            CreateAlteredVersionThreeEndpoint(
                5,
                property => CopyProperty(
                    property,
                    path: DescriptorPath.Parse("Operating.Other"))),
            CreateAlteredVersionThreeEndpoint(
                7,
                property => CopyProperty(
                    property,
                    accessMode: PropertyAccessMode.ReadWrite)),
            CreateAlteredVersionThreeEndpoint(
                6,
                property => CopyProperty(
                    property,
                    data: new StringDataDescriptor())),
            CreateAlteredVersionThreeEndpoint(
                7,
                property => CopyProperty(
                    property,
                    data: new NumericDataDescriptor(
                        targetNumeric.Quantity,
                        new Unit("other-unit", "Other unit", "X", targetNumeric.Quantity),
                        targetNumeric.Range,
                        targetNumeric.Resolution))),
            CreateAlteredVersionThreeEndpoint(
                7,
                property => CopyProperty(
                    property,
                    data: new NumericDataDescriptor(
                        targetNumeric.Quantity,
                        targetNumeric.NativeUnit,
                        new ValueRange(0.0, 120.0),
                        targetNumeric.Resolution))),
            CreateAlteredVersionThreeEndpoint(
                7,
                property => CopyProperty(
                    property,
                    data: new NumericDataDescriptor(
                        targetNumeric.Quantity,
                        targetNumeric.NativeUnit,
                        targetNumeric.Range,
                        new Resolution(0.1)))),
            CreateAlteredVersionThreeEndpoint(addCommand: true),
            CreateAlteredVersionThreeEndpoint(addEvent: true)
        };

        foreach (RuntimeEndpoint endpoint in alteredEndpoints)
        {
            var session = new FakeSession();
            Assert.Throws<InvalidDataException>(() => CreateAdapter(session, endpoint));
            Assert.Empty(session.Queries);
        }
    }

    [Fact]
    public async Task Synchronize_UpdatesAllFivePropertiesWithOneGoodUtcTimestamp()
    {
        var endpoint = CreateVersionTwoEndpoint();
        var session = new FakeSession(
            "RND 320-KEL103 V3.30 SN:REDACTED", "9.8864V", "0.1000A", "0.9893W");
        await using var adapter = CreateAdapter(session, endpoint);

        RuntimeEndpoint result = await adapter.SynchronizeAsync();

        Assert.Same(endpoint, result);
        object?[] values = endpoint.Instruments.Single().Properties
            .Select(property => property.CurrentValue?.Value).ToArray();
        Assert.Equal(new object[] { "KEL-103", "V3.30", 9.8864m, 0.1000m, 0.9893m }, values);
        Assert.All(endpoint.Instruments.Single().Properties, property =>
        {
            Assert.Equal(PropertyQuality.Good, property.CurrentValue!.Quality);
            Assert.Equal(FixedTimestamp, property.CurrentValue.TimestampUtc);
        });
    }

    [Fact]
    public async Task FailedSynchronization_LeavesEveryCacheEmpty()
    {
        var endpoint = CreateVersionTwoEndpoint();
        var session = new FakeSession(
            "RND 320-KEL103 V3.30 SN:REDACTED", "9.8V", "WRONG", "1W");
        await using var adapter = CreateAdapter(session, endpoint);

        await Assert.ThrowsAsync<InvalidDataException>(() => adapter.SynchronizeAsync());

        Assert.All(endpoint.Instruments.Single().Properties,
            property => Assert.Null(property.CurrentValue));
    }

    [Fact]
    public async Task VersionThreeSynchronize_UpdatesAllElevenPropertiesAtomically()
    {
        var endpoint = CreateVersionThreeEndpoint();
        var session = new FakeSession(
            "RND 320-KEL103 V3.30 SN:REDACTED",
            "9.8864V",
            "0.1000A",
            "0.9893W",
            "SHORt",
            "OFF",
            "10.000V",
            "0.1000A",
            "100.00OHM",
            "1.000W");
        await using var adapter = CreateAdapter(session, endpoint);

        RuntimeEndpoint result = await adapter.SynchronizeAsync();

        Assert.Same(endpoint, result);
        Assert.Equal(
            new object[]
            {
                "KEL-103",
                "V3.30",
                9.8864m,
                0.1000m,
                0.9893m,
                "SHORT",
                false,
                10.000m,
                0.1000m,
                100.00m,
                1.000m
            },
            endpoint.Instruments.Single().Properties
                .Select(property => property.CurrentValue?.Value));
        Assert.Equal(
            new[]
            {
                "*IDN?",
                ":MEASure:VOLTage?",
                ":MEASure:CURRent?",
                ":MEASure:POWer?",
                ":FUNCtion?",
                ":INPut?",
                ":VOLTage?",
                ":CURRent?",
                ":RESistance?",
                ":POWer?"
            },
            session.Queries);
        Assert.All(endpoint.Instruments.Single().Properties, property =>
        {
            Assert.Equal(PropertyQuality.Good, property.CurrentValue!.Quality);
            Assert.Equal(FixedTimestamp, property.CurrentValue.TimestampUtc);
        });
    }

    [Fact]
    public async Task FailedVersionThreeSynchronization_LeavesEveryCacheEmpty()
    {
        var endpoint = CreateVersionThreeEndpoint();
        var session = new FakeSession(
            "RND 320-KEL103 V3.30 SN:REDACTED",
            "9.8864V",
            "0.1000A",
            "0.9893W",
            "CC",
            "OFF",
            "10.000V",
            "WRONG");
        await using var adapter = CreateAdapter(session, endpoint);

        await Assert.ThrowsAsync<InvalidDataException>(() => adapter.SynchronizeAsync());

        Assert.All(
            endpoint.Instruments.Single().Properties,
            property => Assert.Null(property.CurrentValue));
    }

    [Theory]
    [InlineData("product-identity", "RND 320-KEL103 V3.30 SN:REDACTED", "KEL-103")]
    [InlineData("firmware-version", "RND 320-KEL103 V3.30 SN:REDACTED", "V3.30")]
    [InlineData("measured-voltage", "1.25V", "1.25")]
    [InlineData("measured-current", "0.10A", "0.10")]
    [InlineData("measured-power", "0.125W", "0.125")]
    public async Task Read_RefreshesOnlyRequestedProperty(
        string propertyId,
        string response,
        string expected)
    {
        var endpoint = CreateVersionTwoEndpoint();
        var session = new FakeSession(response);
        await using var adapter = CreateAdapter(session, endpoint);

        RuntimeProperty property = await adapter.ReadAsync(
            new InstrumentId("electronic-load-01"), new PropertyId(propertyId));

        Assert.Equal(propertyId, property.Descriptor.Id.Value);
        Assert.Equal(expected, Convert.ToString(property.CurrentValue!.Value, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Single(session.Queries);
        Assert.All(endpoint.Instruments.Single().Properties.Where(candidate => candidate != property),
            candidate => Assert.Null(candidate.CurrentValue));
    }

    [Theory]
    [InlineData("operating-mode", "SHORt", "SHORT", ":FUNCtion?")]
    [InlineData("input-enabled", "ON", "True", ":INPut?")]
    [InlineData("target-voltage", "1.25V", "1.25", ":VOLTage?")]
    [InlineData("target-current", "0.10A", "0.10", ":CURRent?")]
    [InlineData("target-resistance", "100.0OHM", "100.0", ":RESistance?")]
    [InlineData("target-power", "0.125W", "0.125", ":POWer?")]
    public async Task VersionThreeRead_RefreshesOnlyRequestedProperty(
        string propertyId,
        string response,
        string expected,
        string expectedQuery)
    {
        var endpoint = CreateVersionThreeEndpoint();
        var session = new FakeSession(response);
        await using var adapter = CreateAdapter(session, endpoint);

        RuntimeProperty property = await adapter.ReadAsync(
            new InstrumentId("electronic-load-01"),
            new PropertyId(propertyId));

        Assert.Equal(
            expected,
            Convert.ToString(
                property.CurrentValue!.Value,
                System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(expectedQuery, Assert.Single(session.Queries));
        Assert.All(
            endpoint.Instruments.Single().Properties.Where(candidate => candidate != property),
            candidate => Assert.Null(candidate.CurrentValue));
    }

    [Theory]
    [InlineData("other-instrument", "measured-voltage")]
    [InlineData("electronic-load-01", "other-property")]
    public async Task Read_RejectsUnknownTargetWithoutQuery(string instrumentId, string propertyId)
    {
        var session = new FakeSession();
        await using var adapter = CreateAdapter(session, CreateVersionTwoEndpoint());
        await Assert.ThrowsAsync<KeyNotFoundException>(() => adapter.ReadAsync(
            new InstrumentId(instrumentId), new PropertyId(propertyId)));
        Assert.Empty(session.Queries);
    }

    [Fact]
    public async Task FailedRead_PreservesPreviousCachedValue()
    {
        var endpoint = CreateVersionTwoEndpoint();
        RuntimeProperty property = endpoint.Instruments.Single().Properties[2];
        property.UpdateValue(new PropertyValue(2m, FixedTimestamp));
        var session = new FakeSession("WRONG");
        await using var adapter = CreateAdapter(session, endpoint);
        await Assert.ThrowsAsync<InvalidDataException>(() => adapter.ReadAsync(
            new InstrumentId("electronic-load-01"), property.Descriptor.Id));
        Assert.Equal(2m, property.CurrentValue!.Value);
    }

    [Fact]
    public async Task FailedVersionThreeRead_PreservesPreviousCachedValue()
    {
        var endpoint = CreateVersionThreeEndpoint();
        RuntimeProperty property = endpoint.Instruments.Single().Properties[5];
        property.UpdateValue(new PropertyValue("CC", FixedTimestamp));
        var session = new FakeSession("WRONG");
        await using var adapter = CreateAdapter(session, endpoint);

        await Assert.ThrowsAsync<InvalidDataException>(() => adapter.ReadAsync(
            new InstrumentId("electronic-load-01"),
            property.Descriptor.Id));

        Assert.Equal("CC", property.CurrentValue!.Value);
    }

    [Fact]
    public async Task Dispose_DisposesOwnedSessionAdapter()
    {
        var session = new FakeSession();
        var adapter = CreateAdapter(session, CreateVersionTwoEndpoint());
        await adapter.DisposeAsync();
        Assert.Equal(1, session.DisposeCount);
    }

    [Fact]
    public void Assembly_DoesNotReferenceDeferredLayers()
    {
        string[] references = typeof(Kel103RuntimeEndpointAdapter).Assembly
            .GetReferencedAssemblies().Select(value => value.Name ?? string.Empty).ToArray();
        Assert.DoesNotContain(references, name => name == "Hase.Runtime.Transport");
        Assert.DoesNotContain(references, name => name == "Hase.Transport");
        Assert.DoesNotContain(references, name => name.Contains("Grpc", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.Contains("Wpf", StringComparison.Ordinal));
    }

    private static readonly DateTimeOffset FixedTimestamp =
        new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);

    private static RuntimeEndpoint CreateVersionTwoEndpoint(string id = "external-endpoint") =>
        new RuntimeContext().CreateEndpoint(
            Kel103ReadOnlyMeasurementDefinition.EndpointDefinition.Materialize(new EndpointId(id)));

    private static RuntimeEndpoint CreateVersionThreeEndpoint(string id = "external-endpoint") =>
        new RuntimeContext().CreateEndpoint(
            Kel103OperatingStateDefinition.EndpointDefinition.Materialize(new EndpointId(id)));

    private static RuntimeEndpoint CreateAlteredVersionThreeEndpoint(
        int? propertyIndex = null,
        Func<PropertyDescriptor, PropertyDescriptor>? alterProperty = null,
        bool addCommand = false,
        bool addEvent = false)
    {
        InstrumentDescriptor expected =
            Kel103OperatingStateDefinition.EndpointDefinition.Instruments.Single();
        PropertyDescriptor[] properties = expected.Interface.Properties.ToArray();
        if (propertyIndex is int index)
        {
            properties[index] = (alterProperty ?? throw new ArgumentNullException(nameof(alterProperty)))(
                properties[index]);
        }

        var instrument = new InstrumentDescriptor(expected.Id, expected.Name, expected.Kind)
        {
            Metadata = expected.Metadata,
            Interface = new InstrumentInterface(
                properties,
                addCommand
                    ? [new CommandDescriptor(DescriptorPath.Parse("Mode.Unexpected"), "Unexpected")]
                    : null,
                addEvent
                    ? [new EventDescriptor(DescriptorPath.Parse("State.Unexpected"), "Unexpected")]
                    : null)
        };
        var descriptor = new EndpointDescriptor(new EndpointId("external-endpoint"), [instrument])
        {
            Metadata = Kel103OperatingStateDefinition.EndpointDefinition.Metadata
        };
        return new RuntimeContext().CreateEndpoint(descriptor);
    }

    private static PropertyDescriptor CopyProperty(
        PropertyDescriptor property,
        DescriptorPath? path = null,
        PropertyAccessMode? accessMode = null,
        DataDescriptor? data = null) =>
        new(
            property.Id,
            path ?? property.Path,
            property.DisplayName,
            data ?? property.Data)
        {
            Description = property.Description,
            AccessMode = accessMode ?? property.AccessMode
        };

    private static Kel103RuntimeEndpointAdapter CreateAdapter(FakeSession session, RuntimeEndpoint endpoint) =>
        new(new Kel103ReadOnlySessionAdapter(session, new FixedTimeProvider()), endpoint, new FixedTimeProvider());

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => FixedTimestamp;
    }

    private sealed class FakeSession(params string[] responses) : IScpiTextSession
    {
        private readonly Queue<string> pending = new(responses);
        public List<string> Queries { get; } = [];
        public int DisposeCount { get; private set; }
        public ScpiTextSessionState State => DisposeCount == 0 ? ScpiTextSessionState.Open : ScpiTextSessionState.Disposed;
        public Task<string> QueryAsync(string query, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Queries.Add(query);
            return Task.FromResult(pending.Dequeue());
        }
        public Task SendCommandAsync(string command, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }
}
