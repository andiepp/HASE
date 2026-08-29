namespace Hase.Client.Wpf.ViewModels;

/// <summary>
/// Presents one published endpoint attachment in the client inventory.
/// </summary>
/// <remarks>
/// Equality is identity equality on <see cref="Key"/> alone, deliberately
/// excluding the live values carried by the projected instruments.
///
/// The projection is immutable and is replaced completely whenever the
/// observation state changes, which happens continuously for an endpoint whose
/// values move. A selection control holds the item instance it selected, so
/// with the default record equality it could never match the replacement and
/// the visual selection was lost on the first refresh. Identity equality lets
/// the control re-match its retained item against the rebuilt inventory, which
/// is the same rule <see cref="MainWindowViewModel.SelectedEndpoint"/> already
/// applies when it resolves the logical selection by key.
/// </remarks>
public sealed record EndpointInventoryItemViewModel(
    RemoteEndpointAttachmentKey Key,
    string EndpointId,
    string AttachmentGeneration,
    string DisplayName,
    string ConnectionState,
    bool IsReady,
    bool IsStale,
    IReadOnlyList<InstrumentInventoryItemViewModel> Instruments)
{
    /// <summary>
    /// Gets or sets whether this attachment is the selected one.
    /// </summary>
    /// <remarks>
    /// The selection indication is drawn from this flag rather than from the
    /// selection control's own visual state, because the projection is
    /// replaced on every observation change and the control clears its
    /// visual selection when its item source is replaced. The Runtime Host
    /// list already presents its selection the same way.
    ///
    /// It is excluded from equality: selection is presentation state, not
    /// attachment identity.
    /// </remarks>
    public bool IsSelected
    {
        get;
        set;
    }

    public bool Equals(
        EndpointInventoryItemViewModel? other)
    {
        return other is not null
            && Key == other.Key;
    }

    public override int GetHashCode()
    {
        return Key.GetHashCode();
    }
}
