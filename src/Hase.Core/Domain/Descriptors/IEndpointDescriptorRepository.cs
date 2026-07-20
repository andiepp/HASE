namespace Hase.Core.Domain.Descriptors;

/// <summary>
/// Resolves exact, versioned endpoint descriptor definitions held by the
/// runtime host.
/// </summary>
public interface IEndpointDescriptorRepository
{
    /// <summary>
    /// Finds the definition identified by an exact descriptor reference, or
    /// returns <see langword="null"/> when that reference is unavailable.
    /// </summary>
    ValueTask<EndpointDescriptorDefinition?> FindAsync(
        DescriptorReference reference,
        CancellationToken cancellationToken = default);
}
