namespace Hase.Scpi.Tests;

internal sealed class ScriptedScpiByteStream : IScpiByteStream
{
    private readonly Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> write;
    private readonly Func<Memory<byte>, CancellationToken, ValueTask<int>> read;
    private readonly Func<ValueTask> dispose;

    public ScriptedScpiByteStream(
        Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask>? write = null,
        Func<Memory<byte>, CancellationToken, ValueTask<int>>? read = null,
        Func<ValueTask>? dispose = null)
    {
        this.write = write ?? ((_, _) => ValueTask.CompletedTask);
        this.read = read ?? ((_, _) => ValueTask.FromResult(0));
        this.dispose = dispose ?? (() => ValueTask.CompletedTask);
    }

    public List<byte[]> Writes { get; } = [];

    public int DisposeCount { get; private set; }

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
    {
        Writes.Add(bytes.ToArray());
        await write(bytes, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        read(buffer, cancellationToken);

    public async ValueTask DisposeAsync()
    {
        DisposeCount++;
        await dispose().ConfigureAwait(false);
    }

    public static ScriptedScpiByteStream FromResponseChunks(params byte[][] chunks)
    {
        var pending = new Queue<byte[]>(chunks);
        return new ScriptedScpiByteStream(read: (buffer, _) =>
        {
            if (pending.Count == 0)
            {
                return ValueTask.FromResult(0);
            }

            var chunk = pending.Dequeue();
            if (chunk.Length > buffer.Length)
            {
                throw new InvalidOperationException("The scripted chunk does not fit in the supplied read buffer.");
            }

            chunk.AsSpan().CopyTo(buffer.Span);
            return ValueTask.FromResult(chunk.Length);
        });
    }
}
