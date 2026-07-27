namespace Hase.Client;

/// <summary>
/// Composes one unconnected normalized runtime-host client session from an
/// external configuration selected by the consumer.
/// </summary>
public interface IRuntimeHostClientSessionFactory
{
    /// <summary>
    /// Loads the external configuration and creates one unconnected session.
    /// </summary>
    Task<IRuntimeHostClientSession> CreateAsync(
        string configurationFilePath,
        CancellationToken cancellationToken = default);
}
