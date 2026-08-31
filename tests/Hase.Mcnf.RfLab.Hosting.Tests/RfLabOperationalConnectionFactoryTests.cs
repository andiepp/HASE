using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Runtime;
using Hase.Transport.Serial;

namespace Hase.Mcnf.RfLab.Hosting.Tests;

public sealed class RfLabOperationalConnectionFactoryTests
{
    private static RfLabOperationalConnectionFactory CreateFactory(
        RuntimeContext context,
        params ISerialByteStream[] streams) =>
        new(
            context,
            new RecordingSerialFactory(streams),
            settleDelay: TimeSpan.Zero);

    [Fact]
    public async Task OpenAsync_SynchronizesTheReadOnlyEndpointCompletely()
    {
        var context = new RuntimeContext();
        var stream = RfLabHostingTestSupport.SuccessfulOpenStream();
        var factory = CreateFactory(context, stream);

        await using RfLabOperationalConnection connection = await factory.OpenAsync(
            new EndpointId("rflab-test-01"),
            RfLabHostingTestSupport.SupportedOptions());

        Assert.All(
            connection.RuntimeEndpoint.Instruments.Single().Properties,
            property => Assert.NotNull(property.CurrentValue));
        Assert.Equal(4, stream.Writes.Count);
        Assert.Equal(new byte[] { 0xA1 }, stream.Writes[0]);
    }

    [Fact]
    public async Task OpenAsync_SynchronizesTheControlledEndpointWithStagedDefaults()
    {
        var context = new RuntimeContext();
        var factory = CreateFactory(context, RfLabHostingTestSupport.SuccessfulOpenStream());

        await using RfLabOperationalConnection connection = await factory.OpenAsync(
            new EndpointId("rflab-test-01"),
            RfLabControlledSignalDefinition.EndpointDefinition,
            RfLabHostingTestSupport.SupportedOptions());

        Assert.Equal(
            RfLabControlledSignalDefinition.EndpointDefinition
                .Instruments.Single().Interface.Properties.Count,
            connection.RuntimeEndpoint.Instruments.Single().Properties.Count);
        Assert.All(
            connection.RuntimeEndpoint.Instruments.Single().Properties,
            property => Assert.NotNull(property.CurrentValue));
    }

    [Fact]
    public async Task OpenAsync_RejectsForeignNodeIdentityAndDisposesTheStream()
    {
        var context = new RuntimeContext();
        var stream = new ScriptedSerialByteStream(
            RfLabHostingTestSupport.ConnectivityResponse(),
            RfLabHostingTestSupport.SuccessResponse(0xAE, 0x63, 0x05, 0x80));
        var factory = CreateFactory(context, stream);

        await Assert.ThrowsAsync<InvalidDataException>(() => factory.OpenAsync(
            new EndpointId("rflab-test-01"),
            RfLabHostingTestSupport.SupportedOptions()));

        Assert.Equal(1, stream.DisposeCount);
    }

    [Theory]
    [InlineData(9600, true, true)]
    [InlineData(115200, false, true)]
    [InlineData(115200, true, false)]
    public async Task OpenAsync_RejectsSerialSettingsOutsideTheSupportedProfile(
        int baudRate,
        bool assertDtr,
        bool assertRts)
    {
        var context = new RuntimeContext();
        var factory = CreateFactory(context, RfLabHostingTestSupport.SuccessfulOpenStream());
        var options = new SerialTransportOptions(
            "TEST-PORT",
            baudRate,
            dataBits: 8,
            SerialParity.None,
            SerialStopBits.One,
            SerialHandshake.None,
            assertDtr,
            assertRts);

        await Assert.ThrowsAsync<ArgumentException>(() => factory.OpenAsync(
            new EndpointId("rflab-test-01"),
            options));
    }

    [Fact]
    public async Task OpenAsync_RejectsDefinitionsThatAreNotTheExactSupportedInstances()
    {
        var context = new RuntimeContext();
        var factory = CreateFactory(context, RfLabHostingTestSupport.SuccessfulOpenStream());
        var copy = new Core.Domain.Descriptors.EndpointDescriptorDefinition(
            new EndpointMetadata { DisplayName = "RF-Lab Signal Laboratory" },
            RfLabReadOnlyDefinition.EndpointDefinition.Instruments);

        await Assert.ThrowsAsync<InvalidDataException>(() => factory.OpenAsync(
            new EndpointId("rflab-test-01"),
            copy,
            RfLabHostingTestSupport.SupportedOptions()));
    }

    [Fact]
    public void Constructor_RejectsNegativeSettleDelays()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RfLabOperationalConnectionFactory(
                new RuntimeContext(),
                new RecordingSerialFactory(),
                settleDelay: TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void DefaultSettleDelay_CoversTheCharacterizedNodeReset()
    {
        Assert.Equal(
            TimeSpan.FromSeconds(3),
            RfLabOperationalConnectionFactory.DefaultSettleDelay);
    }
}
