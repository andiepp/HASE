using Hase.Runtime.Diagnostics;

namespace Hase.Runtime.Tests;

public sealed class NullRuntimeDiagnosticSinkTests
{
    [Theory]
    [InlineData(RuntimeDiagnosticLevel.Operational)]
    [InlineData(RuntimeDiagnosticLevel.Protocol)]
    [InlineData(RuntimeDiagnosticLevel.Bytes)]
    public void IsEnabled_EveryLevel_ReturnsFalse(
        RuntimeDiagnosticLevel level)
    {
        Assert.False(
            NullRuntimeDiagnosticSink.Instance.IsEnabled(
                level));
    }
}
