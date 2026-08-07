using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Hase.Runtime.Northbound;

internal sealed class BufferedRuntimeHostDiagnosticProjectionSubscription
    : RuntimeHostDiagnosticProjectionSubscription
{
    private readonly Channel<RuntimeHostProjectedDiagnosticObservation> channel;
    private readonly Action<BufferedRuntimeHostDiagnosticProjectionSubscription>
        remove;
    private readonly object gate = new();
    private long nextSequence;
    private bool isEnded;

    public BufferedRuntimeHostDiagnosticProjectionSubscription(
        int bufferCapacity,
        Action<BufferedRuntimeHostDiagnosticProjectionSubscription> remove)
    {
        this.remove = remove
            ?? throw new ArgumentNullException(nameof(remove));
        channel = Channel.CreateBounded<RuntimeHostProjectedDiagnosticObservation>(
            new BoundedChannelOptions(bufferCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });
    }

    public bool TryEnqueue(RuntimeHostProjectedDiagnosticRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        lock (gate)
        {
            if (isEnded)
            {
                return false;
            }

            long sequenceValue = checked(nextSequence + 1);
            var observation = new RuntimeHostProjectedDiagnosticObservation(
                new RuntimeHostDiagnosticProjectionSequence(sequenceValue),
                record);

            if (!channel.Writer.TryWrite(observation))
            {
                isEnded = true;
                channel.Writer.TryComplete(
                    new RuntimeHostDiagnosticProjectionGapException());
                return false;
            }

            nextSequence = sequenceValue;
            return true;
        }
    }

    public override async IAsyncEnumerable<
        RuntimeHostProjectedDiagnosticObservation> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (await WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            while (channel.Reader.TryRead(
                       out RuntimeHostProjectedDiagnosticObservation? observation))
            {
                yield return observation;
            }
        }
    }

    public override ValueTask DisposeAsync()
    {
        End();
        return ValueTask.CompletedTask;
    }

    public void End()
    {
        bool shouldRemove;

        lock (gate)
        {
            shouldRemove = !isEnded;
            isEnded = true;
            channel.Writer.TryComplete();
        }

        if (shouldRemove)
        {
            remove(this);
        }
    }

    private async ValueTask<bool> WaitToReadAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            return await channel.Reader
                .WaitToReadAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ChannelClosedException exception)
            when (exception.InnerException
                  is RuntimeHostDiagnosticProjectionGapException gap)
        {
            throw gap;
        }
    }
}
