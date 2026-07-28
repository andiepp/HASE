using Hase.DesktopHost.App.ViewModels;

namespace Hase.DesktopHost.Tests;

public sealed class OperatorActivityViewModelTests
{
    [Fact]
    public void Record_ShouldCaptureImmutableUtcEntry()
    {
        var timeProvider =
            new StubTimeProvider(
                new DateTimeOffset(
                    2026,
                    7,
                    28,
                    14,
                    30,
                    0,
                    TimeSpan.FromHours(2)));
        var activity =
            new OperatorActivityViewModel(
                timeProvider);

        activity.Record(
            DesktopRuntimeOperatorActivityKind.BooleanPropertyWrite,
            "endpoint-1",
            "generation-1",
            "instrument-1",
            "Controller.StatusLedEnabled",
            "False",
            DesktopRuntimeOperatorActivityOutcome.Succeeded);

        DesktopRuntimeOperatorActivityEntry entry =
            Assert.Single(
                activity.Entries);
        Assert.Equal(
            DateTimeOffset.Parse(
                "2026-07-28T12:30:00+00:00"),
            entry.TimestampUtc);
        Assert.Equal(
            "2026-07-28T12:30:00.0000000+00:00",
            entry.TimestampUtcText);
        Assert.Equal(
            DesktopRuntimeOperatorActivityKind.BooleanPropertyWrite,
            entry.Kind);
        Assert.Equal(
            "endpoint-1",
            entry.EndpointId);
        Assert.Equal(
            "generation-1",
            entry.AttachmentGeneration);
        Assert.Equal(
            "instrument-1",
            entry.InstrumentId);
        Assert.Equal(
            "Controller.StatusLedEnabled",
            entry.OperationPath);
        Assert.Equal(
            "False",
            entry.InputSummary);
        Assert.Equal(
            DesktopRuntimeOperatorActivityOutcome.Succeeded,
            entry.Outcome);
        Assert.Empty(
            entry.Diagnostic);
        Assert.Empty(
            entry.Reconciliation);
    }

    [Fact]
    public void Record_ShouldInsertNewestEntryFirst()
    {
        var activity =
            new OperatorActivityViewModel();

        Record(
            activity,
            "first");
        Record(
            activity,
            "second");

        Assert.Equal(
            [
                "second",
                "first"
            ],
            activity.Entries.Select(
                entry =>
                    entry.OperationPath));
    }

    [Fact]
    public void Record_ShouldRetainLatestOneHundredEntries()
    {
        var activity =
            new OperatorActivityViewModel();

        for (
            int index = 0;
            index <= OperatorActivityViewModel.Capacity;
            index++)
        {
            Record(
                activity,
                $"operation-{index}");
        }

        Assert.Equal(
            OperatorActivityViewModel.Capacity,
            activity.Entries.Count);
        Assert.Equal(
            "operation-100",
            activity.Entries[0].OperationPath);
        Assert.Equal(
            "operation-1",
            activity.Entries[^1].OperationPath);
    }

    [Fact]
    public void Constructor_WithNullTimeProvider_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            () =>
                new OperatorActivityViewModel(
                    null!));
    }

    private static void Record(
        OperatorActivityViewModel activity,
        string operationPath)
    {
        activity.Record(
            DesktopRuntimeOperatorActivityKind
                .ParameterlessCommandExecution,
            "endpoint-1",
            "generation-1",
            "instrument-1",
            operationPath,
            "None",
            DesktopRuntimeOperatorActivityOutcome.Succeeded);
    }

    private sealed class StubTimeProvider(
        DateTimeOffset utcNow)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            utcNow;
    }
}
