namespace Hase.Client.Wpf.ViewModels;

/// <summary>
/// Presents one published endpoint attachment in the client inventory.
/// </summary>
/// <remarks>
/// The default record value equality is deliberate and load-bearing. The
/// selected-endpoint detail pane binds this item into a content control, and
/// the property system discards an update whose new value compares equal to
/// the current one. An equality that ignored the projected live values froze
/// the pane at its first projection, so values only changed on reselection
/// (ADR-0065 Increment 65E). Two consecutive projections must therefore
/// compare unequal.
///
/// The visual selection indication does not rely on equality. It is drawn
/// from <see cref="IsSelected"/>, which
/// <see cref="MainWindowViewModel.SelectedEndpoint"/> re-applies to every
/// rebuilt projection, the same way the Runtime Host list presents its
/// selection.
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
    /// visual selection when its item source is replaced.
    /// </remarks>
    public bool IsSelected
    {
        get;
        set;
    }

    /// <summary>
    /// Gets the panel identifier declared by an instrument of this endpoint
    /// that this client can host, or null when there is none.
    /// </summary>
    /// <remarks>
    /// This is set only when the declaration and a hosted panel agree. An
    /// endpoint that declares a panel this client does not host presents
    /// exactly as one that declares none.
    /// </remarks>
    public string? PanelId
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the identity of the instrument whose panel <see cref="PanelId"/>
    /// names.
    /// </summary>
    public string? PanelInstrumentId
    {
        get;
        init;
    }

    /// <summary>
    /// Gets whether a hosted panel can be opened for this endpoint.
    /// </summary>
    public bool CanOpenPanel =>
        PanelId is not null
        && PanelInstrumentId is not null
        && IsReady;
}
