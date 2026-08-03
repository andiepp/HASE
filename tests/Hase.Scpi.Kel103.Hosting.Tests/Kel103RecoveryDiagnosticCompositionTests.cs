using Hase.Core.Domain.Identity;
using Hase.Runtime.Diagnostics;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport;

namespace Hase.Scpi.Kel103.Hosting.Tests;

public sealed class Kel103RecoveryDiagnosticCompositionTests
{
    [Fact]
    public void CreateRecoveryPolicy_PreservesDefaultScheduleWithSanitizedOperationalRecords()
    {
        var collector = new BoundedRuntimeDiagnosticCollector(
            10,
            RuntimeDiagnosticLevel.Bytes);
        var context = new RuntimeContext(new RuntimeDiagnosticPublisher(collector));
        RuntimeEndpoint endpoint = context.CreateEndpoint(
            Kel103ReadOnlyMeasurementDefinition.EndpointDefinition.Materialize(
                new EndpointId("kel-diagnostic-test")));
        IRuntimeEndpointReconnectPolicy policy =
            Kel103SupervisedAttachmentFactory.CreateRecoveryPolicy(
                endpoint,
                new DefaultRuntimeEndpointReconnectPolicy());

        TimeSpan[] delays =
        [
            policy.GetDelay(0),
            policy.GetDelay(1),
            policy.GetDelay(2),
            policy.GetDelay(3),
            policy.GetDelay(4),
            policy.GetDelay(5)
        ];

        Assert.Equal(
            [
                TimeSpan.Zero,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(10)
            ],
            delays);
        IReadOnlyList<RuntimeDiagnosticRecord> records = collector.GetSnapshot();
        Assert.Equal(6, records.Count);
        Assert.All(
            records,
            record =>
            {
                Assert.Equal(RuntimeDiagnosticLevel.Operational, record.Level);
                Assert.Equal(RuntimeDiagnosticCategory.RuntimeRecovery, record.Category);
                Assert.Equal("RecoveryScheduled", record.EventName);
                Assert.Equal("kel-diagnostic-test", record.EndpointId);
                Assert.Null(record.AttachmentGeneration);
                Assert.Null(record.ByteSnapshot);
                Assert.Equal(3, record.Details.Count);
            });
        Assert.Equal("0", records[0].Details["DelayMilliseconds"]);
        Assert.Equal("10000", records[^1].Details["DelayMilliseconds"]);
    }
}
