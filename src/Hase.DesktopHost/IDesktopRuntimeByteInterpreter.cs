using Hase.Runtime.Diagnostics;

namespace Hase.DesktopHost;

/// <summary>
/// Interprets a copied diagnostic byte snapshot without affecting protocol or
/// transport execution.
/// </summary>
public interface IDesktopRuntimeByteInterpreter
{
    string ProtocolFamily { get; }

    DesktopRuntimeByteInterpretation Interpret(
        RuntimeDiagnosticByteSnapshot snapshot);
}
