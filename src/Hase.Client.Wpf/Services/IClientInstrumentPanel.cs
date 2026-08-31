namespace Hase.Client.Wpf.Services;

/// <summary>
/// Identifies one published instrument and the operations a panel may perform
/// on it.
/// </summary>
public sealed record ClientInstrumentPanelContext(
    string PanelId,
    string EndpointId,
    string InstrumentId,
    string DisplayName,
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
