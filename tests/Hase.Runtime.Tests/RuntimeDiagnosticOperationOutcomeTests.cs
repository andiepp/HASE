using Hase.Runtime.Diagnostics;

namespace Hase.Runtime.Tests;

public sealed class RuntimeDiagnosticOperationOutcomeTests
{
    [Fact]
    public async Task RunAsync_ResultOutcomeIsPublishedWithoutChangingResult()
    {
        BoundedRuntimeDiagnosticCollector collector =
            new(
                4);

        RuntimeDiagnosticOperation operation =
            CreateOperation(
                collector);

        int result =
            await operation.RunAsync(
                _ =>
                    Task.FromResult(
                        42),
                _ =>
                    RuntimeDiagnosticOutcome.Failed);

        Assert.Equal(
            42,
            result);
        Assert.Equal(
            RuntimeDiagnosticOutcome.Failed,
            collector.GetSnapshot()[1].Outcome);
    }

    [Fact]
    public async Task RunAsync_ThrowingOutcomeSelectorDoesNotChangeResult()
    {
        BoundedRuntimeDiagnosticCollector collector =
            new(
                4);

        RuntimeDiagnosticOperation operation =
            CreateOperation(
                collector);

        int result =
            await operation.RunAsync(
                _ =>
                    Task.FromResult(
                        42),
                _ =>
                    throw new InvalidOperationException(
                        "Diagnostic classifier failure."));

        Assert.Equal(
            42,
            result);
        Assert.Equal(
            RuntimeDiagnosticOutcome.Failed,
            collector.GetSnapshot()[1].Outcome);
    }

    private static RuntimeDiagnosticOperation CreateOperation(
        BoundedRuntimeDiagnosticCollector collector)
    {
        return new RuntimeDiagnosticOperation(
            new RuntimeDiagnosticPublisher(
                collector),
            RuntimeDiagnosticCategory.RuntimeProperty,
            "PropertyReadStarted",
            "PropertyReadCompleted",
            "PropertyReadFailed");
    }
}
