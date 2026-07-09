namespace Hase.Runtime.Runtime;

/// <summary>
/// Common interface for all nodes in the HASE runtime graph.
/// </summary>
public interface IRuntimeNode
{
    /// <summary>
    /// Parent node in the runtime graph.
    /// Null only for RuntimeContext.
    /// </summary>
    IRuntimeNode? Parent { get; }

    /// <summary>
    /// Child nodes in the runtime graph.
    /// </summary>
    IReadOnlyList<IRuntimeNode> Children { get; }
}