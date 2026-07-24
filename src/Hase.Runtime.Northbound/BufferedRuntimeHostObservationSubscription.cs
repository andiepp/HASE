using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Hase.Runtime.Northbound;

internal sealed class BufferedRuntimeHostObservationSubscription
    : RuntimeHostObservationSubscription
{
    private readonly Channel<RuntimeHostObservation> _channel;

    private readonly Action<BufferedRuntimeHostObservationSubscription>
        _remove;

    private readonly object _syncRoot =
        new();

    private bool _isEnded;

    private long _nextSequence;

    public BufferedRuntimeHostObservationSubscription(
        PublishedRuntimeHostSnapshot initialSnapshot,
        RuntimeHostObservationSequence snapshotSequence,
        long projectionBoundaryOrder,
        int bufferCapacity,
        Action<BufferedRuntimeHostObservationSubscription> remove)
        : base(
            initialSnapshot,
            snapshotSequence)
    {
        if (projectionBoundaryOrder < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(projectionBoundaryOrder));
        }

        ArgumentNullException.ThrowIfNull(
            remove);

        ProjectionBoundaryOrder =
            projectionBoundaryOrder;

        _nextSequence =
            snapshotSequence.Value;

        _remove =
            remove;

        _channel =
            Channel.CreateBounded<RuntimeHostObservation>(
                new BoundedChannelOptions(
                    bufferCapacity)
                {
                    FullMode =
                        BoundedChannelFullMode.Wait,
                    SingleReader =
                        true,
                    SingleWriter =
                        false,
                    AllowSynchronousContinuations =
                        false
                });
    }

    public long ProjectionBoundaryOrder
    {
        get;
    }

    public bool TryEnqueue(
        Func<RuntimeHostObservationSequence, RuntimeHostObservation>
            createObservation)
    {
        ArgumentNullException.ThrowIfNull(
            createObservation);

        lock (_syncRoot)
        {
            if (_isEnded)
            {
                return false;
            }

            long sequenceValue =
                checked(
                    _nextSequence + 1);

            var sequence =
                new RuntimeHostObservationSequence(
                    sequenceValue);

            RuntimeHostObservation observation =
                createObservation(
                    sequence);

            if (!_channel.Writer.TryWrite(
                    observation))
            {
                _isEnded =
                    true;

                _channel.Writer.TryComplete(
                    new RuntimeHostObservationGapException());

                return false;
            }

            _nextSequence =
                sequenceValue;

            return true;
        }
    }

    public override async IAsyncEnumerable<RuntimeHostObservation>
        ReadAllAsync(
            [EnumeratorCancellation]
            CancellationToken cancellationToken = default)
    {
        while (await WaitToReadAsync(
                   cancellationToken))
        {
            while (_channel.Reader.TryRead(
                       out RuntimeHostObservation? observation))
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
        bool remove;

        lock (_syncRoot)
        {
            remove =
                !_isEnded;

            _isEnded =
                true;

            _channel.Writer.TryComplete();
        }

        if (remove)
        {
            _remove(
                this);
        }
    }

    private async ValueTask<bool> WaitToReadAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            return await _channel.Reader.WaitToReadAsync(
                cancellationToken);
        }
        catch (ChannelClosedException exception)
            when (exception.InnerException
                  is RuntimeHostObservationGapException gap)
        {
            throw gap;
        }
    }
}