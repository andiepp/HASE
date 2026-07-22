using Hase.Core.Domain.Descriptors;

namespace Hase.CompactProtocol;

/// <summary>
/// Resolves exact, versioned compact endpoint definitions held by the runtime
/// host.
/// </summary>
public interface ICompactEndpointDefinitionRepository
{
    /// <summary>
    /// Finds the compact endpoint definition identified by an exact descriptor
    /// reference, or returns <see langword="null"/> when that reference is
    /// unavailable.
    /// </summary>
    ValueTask<CompactEndpointDefinition?> FindAsync(
        DescriptorReference reference,
        CancellationToken cancellationToken = default);
}