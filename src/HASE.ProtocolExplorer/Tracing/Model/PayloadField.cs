namespace Hase.ProtocolExplorer.Tracing.Model;

internal sealed record PayloadField(
    int Offset,
    ReadOnlyMemory<byte> Bytes,
    string Description)
{
    public int Length =>
        Bytes.Length;
}