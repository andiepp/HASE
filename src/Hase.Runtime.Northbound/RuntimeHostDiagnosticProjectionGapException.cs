namespace Hase.Runtime.Northbound;

/// <summary>
/// Indicates that one slow diagnostic subscriber lost its contiguous live
/// delivery boundary and must open a new subscription.
/// </summary>
public sealed class RuntimeHostDiagnosticProjectionGapException : Exception
{
    public RuntimeHostDiagnosticProjectionGapException()
        : base(
            "The Runtime Host diagnostic projection subscription ended because its bounded buffer was exceeded.")
    {
    }
}
