using Hase.Runtime.Northbound;

namespace Hase.DesktopHost.Configuration;

public sealed class DesktopRuntimeHostInstallationAuditResult
{
    public DesktopRuntimeHostInstallationAuditResult(RuntimeHostId runtimeHostId)
    {
        RuntimeHostId = runtimeHostId
            ?? throw new ArgumentNullException(nameof(runtimeHostId));
    }

    public RuntimeHostId RuntimeHostId { get; }
}
