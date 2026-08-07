using System.Collections.ObjectModel;

namespace Hase.Client;

/// <summary>Owns one immutable bounded remote diagnostic byte snapshot.</summary>
public sealed class RemoteRuntimeDiagnosticByteSnapshot
{
    public const int MaximumCapturedByteCount = 256;

    private readonly ReadOnlyCollection<byte> bytes;

    public RemoteRuntimeDiagnosticByteSnapshot(
        int originalByteCount,
        ReadOnlySpan<byte> capturedBytes,
        bool isTruncated)
    {
        if (originalByteCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(originalByteCount));
        }
        if (capturedBytes.Length > MaximumCapturedByteCount)
        {
            throw new ArgumentOutOfRangeException(nameof(capturedBytes));
        }
        if (capturedBytes.Length > originalByteCount)
        {
            throw new ArgumentException(
                "Captured bytes must not exceed the original byte count.",
                nameof(capturedBytes));
        }

        bool expectedTruncation = capturedBytes.Length < originalByteCount;
        if (isTruncated != expectedTruncation)
        {
            throw new ArgumentException(
                "Truncation must match the original and captured byte counts.",
                nameof(isTruncated));
        }

        OriginalByteCount = originalByteCount;
        bytes = Array.AsReadOnly(capturedBytes.ToArray());
        IsTruncated = isTruncated;
    }

    public int OriginalByteCount { get; }

    public int CapturedByteCount => bytes.Count;

    public bool IsTruncated { get; }

    public IReadOnlyList<byte> Bytes => bytes;

    public byte[] ToArray() => bytes.ToArray();
}
