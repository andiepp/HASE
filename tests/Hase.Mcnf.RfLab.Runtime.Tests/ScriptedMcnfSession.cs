using Hase.Mcnf;

namespace Hase.Mcnf.RfLab.Runtime.Tests;

/// <summary>
/// Replays scripted MCNF results in order and records every exchanged
/// request frame. A scripted exception is thrown in place of a response.
/// </summary>
internal sealed class ScriptedMcnfSession : IMcnfSession
{
    private readonly Queue<object> results = new();

    public List<McnfRequestFrame> Requests { get; } = [];

    public int ConnectivityTestCount { get; private set; }

    public Exception? ConnectivityTestFailure { get; set; }

    public bool Disposed { get; private set; }

    public McnfSessionState State { get; set; } = McnfSessionState.Open;

    public void EnqueueSuccess(params byte[] payload) =>
        results.Enqueue(BuildSuccessResponse(payload));

    public void EnqueueDeviceError(byte errorCode) =>
        results.Enqueue(McnfResponseFrame.Parse([errorCode, 0x00]));

    public void EnqueueFailure(Exception exception) =>
        results.Enqueue(exception);

    public static McnfResponseFrame BuildSuccessResponse(params byte[] payload)
    {
        var frame = new byte[payload.Length + 2];
        payload.CopyTo(frame, 1);
        frame[^1] = McnfChecksum.Compute(frame.AsSpan(0, frame.Length - 1));
        return McnfResponseFrame.Parse(frame);
    }

    public Task<McnfResponseFrame> ExchangeAsync(
        McnfRequestFrame request,
        CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        if (results.Count == 0)
        {
            throw new InvalidOperationException(
                "The scripted MCNF session has no result for the exchange.");
        }

        object result = results.Dequeue();
        if (result is Exception exception)
        {
            throw exception;
        }

        return Task.FromResult((McnfResponseFrame)result);
    }

    public Task ConnectivityTestAsync(CancellationToken cancellationToken = default)
    {
        ConnectivityTestCount++;
        return ConnectivityTestFailure is null
            ? Task.CompletedTask
            : Task.FromException(ConnectivityTestFailure);
    }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }
}
