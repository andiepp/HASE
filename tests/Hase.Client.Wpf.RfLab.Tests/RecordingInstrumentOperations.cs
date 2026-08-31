using Hase.Client;
using Hase.Client.Wpf.Services;
using Hase.Core.Domain.Identity;

namespace Hase.Client.Wpf.RfLab.Tests;

/// <summary>
/// Records every Property and Command operation a panel performs and replays
/// scripted results, so the panel can be exercised without a runtime host.
/// </summary>
internal sealed class RecordingInstrumentOperations : IRuntimeHostInstrumentOperations
{
    private readonly Dictionary<string, RemoteValue> readValues =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Holds every Command execution open until released, so a test can
    /// observe what the panel does while an apply is still in flight.
    /// </summary>
    private TaskCompletionSource? gate;

    public RecordingInstrumentOperations()
    {
        Attachment = new RemoteEndpointAttachmentKey(
            new EndpointId("rf-minilab-01"),
            new RemoteEndpointAttachmentGeneration(
                Guid.Parse("2f1c5c30-3f4c-4d9c-9d0a-1b2c3d4e5f60")));
    }

    public RemoteEndpointAttachmentKey Attachment { get; }

    public List<string> Reads { get; } = [];

    public List<(string PropertyId, double Value)> Writes { get; } = [];

    public List<string> Executions { get; } = [];

    public bool FailNextCommand { get; set; }

    public void CloseGate() =>
        gate = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

    public void OpenGate() =>
        Interlocked.Exchange(ref gate, null)?.TrySetResult();

    public void SetRead(string propertyId, RemoteValue value) =>
        readValues[propertyId] = value;

    public Task<RemotePropertyOperationResult> ReadAsync(
        string propertyId,
        CancellationToken cancellationToken = default)
    {
        Reads.Add(propertyId);

        return Task.FromResult(
            readValues.TryGetValue(propertyId, out RemoteValue? value)
                ? RemotePropertyOperationResult.Successful(
                    new RemotePropertyValue(
                        value,
                        DateTimeOffset.UnixEpoch,
                        RemotePropertyQuality.Good))
                : RemotePropertyOperationResult.Failed(
                    RemotePropertyOperationStatus.PropertyNotFound));
    }

    public Task<RemotePropertyOperationResult> WriteAsync(
        string propertyId,
        RemoteValue requestedValue,
        CancellationToken cancellationToken = default)
    {
        Writes.Add((propertyId, requestedValue.NumericValue ?? double.NaN));

        return Task.FromResult(
            RemotePropertyOperationResult.Successful(
                new RemotePropertyValue(
                    requestedValue,
                    DateTimeOffset.UnixEpoch,
                    RemotePropertyQuality.Good)));
    }

    public async Task<RemoteCommandOperationResult> ExecuteAsync(
        string commandPath,
        RemoteValue? argument = null,
        CancellationToken cancellationToken = default)
    {
        Executions.Add(commandPath);

        TaskCompletionSource? held = gate;
        if (held is not null)
        {
            await held.Task.ConfigureAwait(false);
        }

        if (FailNextCommand)
        {
            FailNextCommand = false;
            return RemoteCommandOperationResult.Failed(
                RemoteCommandOperationStatus.EndpointRejected,
                "rejected");
        }

        return RemoteCommandOperationResult.Successful();
    }
}
