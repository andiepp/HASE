namespace Hase.Runtime.Northbound;

/// <summary>
/// Identifies one committed lifecycle change in the shared attachment
/// projection.
/// </summary>
internal enum RuntimeHostAttachmentProjectionChangeKind
{
    Published = 0,
    Ended = 1
}