using System.Collections.ObjectModel;

namespace Hase.Runtime.Diagnostics;

/// <summary>
/// Owns one immutable, bounded snapshot of exact diagnostic bytes.
/// </summary>
public sealed class RuntimeDiagnosticByteSnapshot
{
    public const int MaximumCapturedByteCount =
        256;

    private readonly ReadOnlyCollection<byte> bytes;

    public RuntimeDiagnosticByteSnapshot(
        int originalByteCount,
        ReadOnlySpan<byte> capturedBytes,
        bool isTruncated)
    {
        if (originalByteCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(originalByteCount),
                originalByteCount,
                "Original byte count must not be negative.");
        }

        if (capturedBytes.Length
            > MaximumCapturedByteCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capturedBytes),
                capturedBytes.Length,
                $"Captured bytes must not exceed "
                + $"{MaximumCapturedByteCount} bytes.");
        }

        if (capturedBytes.Length > originalByteCount)
        {
            throw new ArgumentException(
                "Captured byte count must not exceed the original byte "
                + "count.",
                nameof(capturedBytes));
        }

        bool expectedTruncation =
            capturedBytes.Length < originalByteCount;

        if (isTruncated != expectedTruncation)
        {
            throw new ArgumentException(
                "Truncation status must match the original and captured "
                + "byte counts.",
                nameof(isTruncated));
        }

        OriginalByteCount =
            originalByteCount;

        byte[] ownedBytes =
            capturedBytes.ToArray();

        bytes =
            Array.AsReadOnly(
                ownedBytes);

        IsTruncated =
            isTruncated;
    }

    public int OriginalByteCount
    {
        get;
    }

    public int CapturedByteCount =>
        bytes.Count;

    public bool IsTruncated
    {
        get;
    }

    public IReadOnlyList<byte> Bytes =>
        bytes;

    public byte[] ToArray()
    {
        return bytes.ToArray();
    }
}
