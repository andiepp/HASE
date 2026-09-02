using Hase.DesktopHost.Configuration;

namespace Hase.DesktopHost.EndpointProfileTool;

/// <summary>
/// What one composition edit is given to work with.
/// </summary>
/// <remarks>
/// The paths and the backup name are settled before any operation runs, so
/// every edit of one invocation retains its predecessor under the same
/// timestamped name.
/// </remarks>
public sealed record EndpointProfileOperationContext(
    IReadOnlyList<string> Arguments,
    DesktopRuntimeHostEndpointCompositionProfileEditor Editor,
    string ProfilePath,
    string BackupPath,
    string EndpointId);

/// <summary>
/// What one composition edit reports when it succeeds.
/// </summary>
/// <param name="EndpointKind">The endpoint kind to report.</param>
/// <param name="AdditionalReportLines">
/// Lines the operation adds to the report, for whatever it knows that the
/// tool does not.
/// </param>
/// <param name="BackupRetained">
/// Whether the retained backup is reported by name. A definition migration
/// reports it as retained rather than naming it.
/// </param>
public sealed record EndpointProfileOperationResult(
    string EndpointKind,
    IReadOnlyList<string>? AdditionalReportLines = null,
    bool BackupRetained = false);

/// <summary>
/// One composition edit an entry point contributes to the tool.
/// </summary>
/// <remarks>
/// The published tool edits the endpoint kinds that carry no device
/// knowledge. A composition root that ships instruments contributes their
/// operations, which is why the KEL-103 additions and definition migrations
/// are not in the published tool.
/// </remarks>
public interface IEndpointProfileOperation
{
    /// <summary>
    /// Gets the operation name, as typed on the command line.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the usage lines this operation contributes to the help text.
    /// </summary>
    IReadOnlyList<string> UsageLines { get; }

    /// <summary>
    /// Performs the edit.
    /// </summary>
    /// <remarks>
    /// Returns <see langword="null"/> when the arguments do not match, which
    /// the tool reports as a usage error rather than a failure.
    /// </remarks>
    Task<EndpointProfileOperationResult?> ExecuteAsync(
        EndpointProfileOperationContext context);

    /// <summary>
    /// Maps a failure to the message the operator sees.
    /// </summary>
    /// <remarks>
    /// An instrument operation says what went wrong in its own terms without
    /// disclosing the target it was reached through.
    /// </remarks>
    string DescribeFailure(Exception exception);
}
