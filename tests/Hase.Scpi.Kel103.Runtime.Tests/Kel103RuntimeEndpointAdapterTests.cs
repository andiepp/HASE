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
    public void Constructor_AcceptsExactVersionFourAndPreservesExternalIdentity()
    {
        var endpoint = CreateVersionFourEndpoint("version-four-endpoint");
        var adapter = CreateAdapter(new FakeSession(), endpoint);

        Assert.Same(endpoint, adapter.RuntimeEndpoint);
        Assert.Equal("version-four-endpoint", adapter.RuntimeEndpoint.Descriptor.Id.Value);
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
    public void Constructor_RejectsAlteredVersionFourContractsWithoutQueries()
    {
        InstrumentDescriptor expected =
            Kel103ControlledSetpointDefinition.EndpointDefinition.Instruments.Single();
        PropertyDescriptor[] alteredProperties = expected.Interface.Properties.ToArray();
        var targetNumeric = Assert.IsType<NumericDataDescriptor>(alteredProperties[7].Data);
        alteredProperties[7] = CopyProperty(
            alteredProperties[7],
            data: new NumericDataDescriptor(
                targetNumeric.Quantity,
                targetNumeric.NativeUnit,
                new ValueRange(0.0, 120.0),
                targetNumeric.Resolution));
        var alteredCommand = new CommandDescriptor(
            DescriptorPath.Parse("Mode.Unexpected"),
            "Unexpected");

        RuntimeEndpoint[] endpoints =
        [
            CreateVersionFourEndpoint(properties: alteredProperties),
            CreateVersionFourEndpoint(
                commands: expected.Interface.Commands
                    .Select((command, index) => index == 0 ? alteredCommand : command)
                    .ToArray())
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

    [Fact]
    public async Task VersionFourSynchronize_UsesOperatingStatePath()
    {
        var endpoint = CreateVersionFourEndpoint();
        var session = CreateOperatingStateSession();
        await using var adapter = CreateAdapter(session, endpoint);

        await adapter.SynchronizeAsync();

        Assert.Equal(11, endpoint.Instruments.Single().Properties.Count);
        Assert.All(
            endpoint.Instruments.Single().Properties,
            property => Assert.NotNull(property.CurrentValue));
        Assert.Equal(10, session.Queries.Count);
        Assert.Empty(session.Commands);
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

    [Theory]
    [InlineData("target-voltage", 7, 5, 1.25, "1.25V", "CV", ":VOLTage 1.25V", "CV")]
    [InlineData("target-current", 8, 5, 0.1, "0.1A", "CC", ":CURRent 0.1A", "CC")]
    [InlineData("target-resistance", 9, 5, 100.0, "100OHM", "CR", ":RESistance 100OHM", "CR")]
    [InlineData("target-power", 10, 5, 0.125, "0.125W", "CW", ":POWer 0.125W", "CW")]
    public async Task VersionFourWrite_UpdatesOnlyAuthoritativeTargetAndMode(
        string propertyId,
        int targetIndex,
        int modeIndex,
        double requestedValue,
        string targetReadback,
        string modeReadback,
        string expectedCommand,
        string expectedMode)
    {
        var endpoint = CreateVersionFourEndpoint();
        RuntimeInstrument instrument = endpoint.Instruments.Single();
        RuntimeProperty target = instrument.Properties[targetIndex];
        RuntimeProperty mode = instrument.Properties[modeIndex];
        RuntimeProperty unrelated = instrument.Properties[6];
        var previousTarget = new PropertyValue(-1m, FixedTimestamp);
        var previousMode = new PropertyValue("OLD", FixedTimestamp);
        var previousUnrelated = new PropertyValue(false, FixedTimestamp);
        target.UpdateValue(previousTarget);
        mode.UpdateValue(previousMode);
        unrelated.UpdateValue(previousUnrelated);
        var session = new FakeSession("OFF", targetReadback, modeReadback);
        await using var adapter = CreateAdapter(session, endpoint);

        RuntimeProperty result = await adapter.WriteAsync(
            new InstrumentId("electronic-load-01"),
            new PropertyId(propertyId),
            requestedValue);

        Assert.Same(target, result);
        Assert.Equal(Convert.ToDecimal(requestedValue), target.CurrentValue!.Value);
        Assert.Equal(expectedMode, mode.CurrentValue!.Value);
        Assert.Equal(FixedTimestamp, target.CurrentValue.TimestampUtc);
        Assert.Equal(FixedTimestamp, mode.CurrentValue.TimestampUtc);
        Assert.Same(previousUnrelated, unrelated.CurrentValue);
        Assert.Equal(new[] { ":INPut?", targetReadback.EndsWith("OHM", StringComparison.Ordinal) ? ":RESistance?" :
            propertyId switch
            {
                "target-voltage" => ":VOLTage?",
                "target-current" => ":CURRent?",
                "target-power" => ":POWer?",
                _ => throw new InvalidOperationException()
            }, ":FUNCtion?" }, session.Queries);
        Assert.Equal(new[] { expectedCommand }, session.Commands);
    }

    public static IEnumerable<object[]> SupportedNumericValues()
    {
        yield return [(byte)1, "1A", ":CURRent 1A"];
        yield return [(sbyte)1, "1A", ":CURRent 1A"];
        yield return [(short)1, "1A", ":CURRent 1A"];
        yield return [(ushort)1, "1A", ":CURRent 1A"];
        yield return [1, "1A", ":CURRent 1A"];
        yield return [1U, "1A", ":CURRent 1A"];
        yield return [1L, "1A", ":CURRent 1A"];
        yield return [1UL, "1A", ":CURRent 1A"];
        yield return [1.25F, "1.25A", ":CURRent 1.25A"];
        yield return [1.25D, "1.25A", ":CURRent 1.25A"];
        yield return [1.25M, "1.25A", ":CURRent 1.25A"];
    }

    [Theory]
    [MemberData(nameof(SupportedNumericValues))]
    public async Task VersionFourWrite_AcceptsNormalizedHostNumericTypes(
        object requestedValue,
        string readback,
        string expectedCommand)
    {
        var session = new FakeSession("OFF", readback, "CC");
        await using var adapter = CreateAdapter(session, CreateVersionFourEndpoint());

        RuntimeProperty result = await adapter.WriteAsync(
            new InstrumentId("electronic-load-01"),
            Kel103SetpointMapping.Current.PropertyId,
            requestedValue);

        Assert.Equal(expectedCommand, Assert.Single(session.Commands));
        Assert.Equal(
            decimal.Parse(readback[..^1], System.Globalization.CultureInfo.InvariantCulture),
            result.CurrentValue!.Value);
    }

    [Fact]
    public async Task VersionFourWrite_RejectsInvalidLocalRequestsWithoutScpiUse()
    {
        object?[] invalidValues =
        [
            null,
            true,
            "1",
            float.NaN,
            float.PositiveInfinity,
            double.NaN,
            double.NegativeInfinity,
            double.MaxValue,
            31m
        ];

        foreach (object? value in invalidValues)
        {
            var session = new FakeSession();
            await using var adapter = CreateAdapter(session, CreateVersionFourEndpoint());
            await Assert.ThrowsAnyAsync<ArgumentException>(() => adapter.WriteAsync(
                new InstrumentId("electronic-load-01"),
                Kel103SetpointMapping.Current.PropertyId,
                value));
            Assert.Empty(session.Queries);
            Assert.Empty(session.Commands);
            Assert.False(adapter.IsFaulted);
        }

        var versionThreeSession = new FakeSession();
        await using var versionThreeAdapter = CreateAdapter(
            versionThreeSession,
            CreateVersionThreeEndpoint());
        await Assert.ThrowsAsync<KeyNotFoundException>(() => versionThreeAdapter.WriteAsync(
            new InstrumentId("electronic-load-01"),
            Kel103SetpointMapping.Current.PropertyId,
            1.0));
        Assert.Empty(versionThreeSession.Queries);

        var readOnlySession = new FakeSession();
        await using var versionFourAdapter = CreateAdapter(
            readOnlySession,
            CreateVersionFourEndpoint());
        await Assert.ThrowsAsync<KeyNotFoundException>(() => versionFourAdapter.WriteAsync(
            new InstrumentId("electronic-load-01"),
            Kel103InputStateMapping.PropertyId,
            1.0));
        Assert.Empty(readOnlySession.Queries);
    }

    [Fact]
    public async Task VersionFourWrite_InputOnPreservesCachesAndSessionAvailability()
    {
        var endpoint = CreateVersionFourEndpoint();
        RuntimeProperty target = endpoint.Instruments.Single().Properties[8];
        RuntimeProperty mode = endpoint.Instruments.Single().Properties[5];
        var previousTarget = new PropertyValue(0.2m, FixedTimestamp);
        var previousMode = new PropertyValue("CV", FixedTimestamp);
        target.UpdateValue(previousTarget);
        mode.UpdateValue(previousMode);
        var session = new FakeSession("ON");
        await using var adapter = CreateAdapter(session, endpoint);

        await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.WriteAsync(
            new InstrumentId("electronic-load-01"),
            target.Descriptor.Id,
            0.1));

        Assert.Same(previousTarget, target.CurrentValue);
        Assert.Same(previousMode, mode.CurrentValue);
        Assert.Empty(session.Commands);
        Assert.False(adapter.IsFaulted);
    }

    [Fact]
    public async Task VersionFourWrite_UncertaintyPreservesCachesAndFaults()
    {
        var endpoint = CreateVersionFourEndpoint();
        RuntimeProperty target = endpoint.Instruments.Single().Properties[8];
        RuntimeProperty mode = endpoint.Instruments.Single().Properties[5];
        var previousTarget = new PropertyValue(0.2m, FixedTimestamp);
        var previousMode = new PropertyValue("CV", FixedTimestamp);
        target.UpdateValue(previousTarget);
        mode.UpdateValue(previousMode);
        var transmission = new ScpiCommandTransmissionException(
            "uncertain",
            true,
            new IOException("write failed"));
        var session = new FakeSession("OFF") { SendException = transmission };
        await using var adapter = CreateAdapter(session, endpoint);

        await Assert.ThrowsAsync<ScpiCommandTransmissionException>(() => adapter.WriteAsync(
            new InstrumentId("electronic-load-01"),
            target.Descriptor.Id,
            0.1));

        Assert.Same(previousTarget, target.CurrentValue);
        Assert.Same(previousMode, mode.CurrentValue);
        Assert.True(adapter.IsFaulted);
        Assert.Single(session.Commands);
    }

    [Fact]
    public async Task VersionFourWrite_ReadbackUncertaintyPreservesCachesAndFaults()
    {
        var endpoint = CreateVersionFourEndpoint();
        RuntimeProperty target = endpoint.Instruments.Single().Properties[8];
        RuntimeProperty mode = endpoint.Instruments.Single().Properties[5];
        var previousTarget = new PropertyValue(0.2m, FixedTimestamp);
        var previousMode = new PropertyValue("CV", FixedTimestamp);
        target.UpdateValue(previousTarget);
        mode.UpdateValue(previousMode);
        var session = new FakeSession("OFF", "0.2A", "CC");
        await using var adapter = CreateAdapter(session, endpoint);

        await Assert.ThrowsAsync<Kel103MutationOutcomeUncertainException>(() => adapter.WriteAsync(
            new InstrumentId("electronic-load-01"),
            target.Descriptor.Id,
            0.1));

        Assert.Same(previousTarget, target.CurrentValue);
        Assert.Same(previousMode, mode.CurrentValue);
        Assert.True(adapter.IsFaulted);
        Assert.Single(session.Commands);
    }

    [Fact]
    public async Task VersionFourWrite_PreCancellationPreservesCachesWithoutScpiUse()
    {
        var endpoint = CreateVersionFourEndpoint();
        RuntimeProperty target = endpoint.Instruments.Single().Properties[8];
        RuntimeProperty mode = endpoint.Instruments.Single().Properties[5];
        var previousTarget = new PropertyValue(0.2m, FixedTimestamp);
        var previousMode = new PropertyValue("CV", FixedTimestamp);
        target.UpdateValue(previousTarget);
        mode.UpdateValue(previousMode);
        var session = new FakeSession();
        await using var adapter = CreateAdapter(session, endpoint);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => adapter.WriteAsync(
            new InstrumentId("electronic-load-01"),
            target.Descriptor.Id,
            0.1,
            cancellation.Token));

        Assert.Same(previousTarget, target.CurrentValue);
        Assert.Same(previousMode, mode.CurrentValue);
        Assert.Empty(session.Queries);
        Assert.Empty(session.Commands);
        Assert.False(adapter.IsFaulted);
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

    private static RuntimeEndpoint CreateVersionFourEndpoint(
        string id = "external-endpoint",
        IReadOnlyList<PropertyDescriptor>? properties = null,
        IReadOnlyList<CommandDescriptor>? commands = null)
    {
        InstrumentDescriptor expected =
            Kel103ControlledSetpointDefinition.EndpointDefinition.Instruments.Single();
        if (properties is null && commands is null)
        {
            return new RuntimeContext().CreateEndpoint(
                Kel103ControlledSetpointDefinition.EndpointDefinition.Materialize(
                    new EndpointId(id)));
        }

        var instrument = new InstrumentDescriptor(expected.Id, expected.Name, expected.Kind)
        {
            Metadata = expected.Metadata,
            Interface = new InstrumentInterface(
                properties ?? expected.Interface.Properties,
                commands ?? expected.Interface.Commands,
                expected.Interface.Events)
        };
        var descriptor = new EndpointDescriptor(new EndpointId(id), [instrument])
        {
            Metadata = Kel103ControlledSetpointDefinition.EndpointDefinition.Metadata
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
        public List<string> Commands { get; } = [];
        public int DisposeCount { get; private set; }
        public Exception? SendException { get; init; }
        public ScpiTextSessionState State => DisposeCount == 0 ? ScpiTextSessionState.Open : ScpiTextSessionState.Disposed;
        public Task<string> QueryAsync(string query, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Queries.Add(query);
            return Task.FromResult(pending.Dequeue());
        }
        public Task SendCommandAsync(string command, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Commands.Add(command);
            return SendException is null
                ? Task.CompletedTask
                : Task.FromException(SendException);
        }
        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }
}
