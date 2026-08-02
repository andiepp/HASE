using Hase.Client.Configuration;
using Hase.Client.Diagnostics;

namespace Hase.Client.Tests;

public sealed class ClientDiagnosticSessionContextTests
{
    [Fact]
    public void Constructor_PreservesHostIdentityAndNormalizesDisplayName()
    {
        var context = new ClientDiagnosticSessionContext(
            new RuntimeHostProfileId("alpha"),
            "  Alpha Host  ",
            new RemoteRuntimeHostId("expected-alpha"),
            new RemoteRuntimeHostId("authoritative-alpha"));

        Assert.Equal("alpha", context.ProfileId.Value);
        Assert.Equal("Alpha Host", context.ProfileDisplayName);
        Assert.Equal("expected-alpha", context.ExpectedRuntimeHostId.Value);
        Assert.Equal("authoritative-alpha", context.AuthoritativeRuntimeHostId!.Value);
    }

    [Fact]
    public void Constructor_RejectsEmptyDisplayName()
    {
        Assert.Throws<ArgumentException>(() => new ClientDiagnosticSessionContext(
            new RuntimeHostProfileId("alpha"),
            " ",
            new RemoteRuntimeHostId("expected-alpha")));
    }

    [Fact]
    public void Publisher_PreservesSessionContextInRecordProjection()
    {
        BoundedClientDiagnosticCollector collector = new(10);
        ClientDiagnosticPublisher publisher = new(collector);
        var context = new ClientDiagnosticSessionContext(
            new RuntimeHostProfileId("alpha"),
            "Alpha Host",
            new RemoteRuntimeHostId("expected-alpha"));

        publisher.Publish(new ClientDiagnosticEvent(
            ClientDiagnosticLevel.Operational,
            ClientDiagnosticCategory.ClientConnection,
            "Connected",
            sessionContext: context));

        ClientDiagnosticRecord record = Assert.Single(collector.GetSnapshot().Records);
        Assert.Same(context, record.SessionContext);
        Assert.Equal("alpha", record.RuntimeHostProfileId);
        Assert.Equal("Alpha Host", record.RuntimeHostProfileDisplayName);
        Assert.Equal("expected-alpha", record.ExpectedRuntimeHostId);
        Assert.Null(record.AuthoritativeRuntimeHostId);
    }

    [Fact]
    public void ContextFreeEvent_RemainsExplicitlyContextFree()
    {
        BoundedClientDiagnosticCollector collector = new(10);
        new ClientDiagnosticPublisher(collector).Publish(new ClientDiagnosticEvent(
            ClientDiagnosticLevel.Operational,
            ClientDiagnosticCategory.ClientConnection,
            "ProcessStarted"));

        ClientDiagnosticRecord record = Assert.Single(collector.GetSnapshot().Records);
        Assert.Null(record.SessionContext);
        Assert.Null(record.RuntimeHostProfileId);
    }
}
