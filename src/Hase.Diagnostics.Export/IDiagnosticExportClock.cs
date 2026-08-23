namespace Hase.Diagnostics.Export;

/// <summary>
/// Supplies the export timestamp. Defined as an interface so that
/// dependency-injected view-models can take it as an optional parameter:
/// an unregistered interface resolves to null, whereas an optional
/// <c>Func&lt;DateTimeOffset&gt;</c> parameter would receive a DryIoc
/// wrapper delegate that fails on invocation.
/// </summary>
public interface IDiagnosticExportClock
{
    DateTimeOffset UtcNow();
}
