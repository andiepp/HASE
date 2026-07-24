namespace Hase.Runtime.Northbound;

/// <summary>
/// Observes ordered committed changes from the shared northbound attachment
/// projection.
/// </summary>
internal interface IRuntimeHostAttachmentProjectionObserver
{
    void OnAttachmentProjectionChanged(
        RuntimeHostAttachmentProjectionChange change);
}