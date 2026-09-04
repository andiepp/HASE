namespace Hase.Client.Wpf.Services;

/// <summary>
/// Identifies one published instrument and the operations a panel may perform
/// on it.
/// </summary>
/// <remarks>
/// <see cref="InstrumentId"/> and <see cref="Operations"/> name the instrument
/// whose declaration opened the panel. An endpoint may publish more than one
/// instrument, and a panel presenting a whole endpoint reaches the others
/// through <see cref="Instruments"/>.
/// </remarks>
public sealed record ClientInstrumentPanelContext(
    string PanelId,
    string EndpointId,
    string InstrumentId,
    string DisplayName,
    IRuntimeHostInstrumentOperations Operations)
{
    /// <summary>
    /// Gets every instrument the attachment publishes, in the order the
    /// descriptor declares them, each with operations bounded to it.
    /// </summary>
    /// <remarks>
    /// Empty when the workspace supplied none, in which case a panel has only
    /// the declaring instrument's <see cref="Operations"/>. The declaring
    /// instrument is a member of this list as well, so a panel that spans an
    /// endpoint need not treat it as a special case.
    /// </remarks>
    public IReadOnlyList<ClientPanelInstrument> Instruments
    {
        get;
        init;
    } = [];

    /// <summary>
    /// Resolves the instrument of this attachment that publishes the named
    /// Property, or null when none does.
    /// </summary>
    /// <remarks>
    /// A panel spanning several instruments of one endpoint selects each of
    /// its parts by what the descriptor publishes rather than by an instrument
    /// identifier it would otherwise have to spell out, which would break as
    /// soon as a second endpoint of the same kind were published.
    /// </remarks>
    public ClientPanelInstrument? FindInstrumentPublishing(string propertyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyId);

        return Instruments.FirstOrDefault(
            instrument =>
                instrument.PropertyIds.Contains(
                    propertyId,
                    StringComparer.Ordinal));
    }
}

/// <summary>
/// One instrument of the attachment a panel was opened for, with the
/// operations bounded to it.
/// </summary>
/// <param name="PropertyIds">
/// The identifiers of the Properties this instrument publishes, as the
/// descriptor declares them.
/// </param>
public sealed record ClientPanelInstrument(
    string InstrumentId,
    string DisplayName,
    string Kind,
    IReadOnlyList<string> PropertyIds,
    IRuntimeHostInstrumentOperations Operations);

/// <summary>
/// Presents one dedicated operating surface for instruments that declare its
/// panel identifier.
/// </summary>
/// <remarks>
/// A panel is opened for one attachment at a time and is closed when the
/// workspace closes. It receives its device access through the context and has
/// no other route to the endpoint.
/// </remarks>
public interface IClientInstrumentPanel
{
    /// <summary>
    /// Gets the panel identifier this panel is declared by.
    /// </summary>
    string PanelId { get; }

    /// <summary>
    /// Opens or re-activates the surface for the supplied instrument.
    /// </summary>
    void Open(ClientInstrumentPanelContext context);

    /// <summary>
    /// Closes the surface if it is open.
    /// </summary>
    void Close();
}

/// <summary>
/// Resolves the instrument panels this client can host.
/// </summary>
public interface IClientInstrumentPanelRegistry
{
    /// <summary>
    /// Gets the panel identifiers this client can host.
    /// </summary>
    IReadOnlySet<string> AvailablePanelIds { get; }

    /// <summary>
    /// Resolves the panel declared by the supplied identifier.
    /// </summary>
    bool TryResolve(
        string panelId,
        out IClientInstrumentPanel panel);
}
