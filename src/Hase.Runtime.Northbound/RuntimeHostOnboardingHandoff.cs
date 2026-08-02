namespace Hase.Runtime.Northbound;

public sealed class RuntimeHostOnboardingHandoff
{
    public RuntimeHostOnboardingHandoff(RuntimeHostId runtimeHostId)
    {
        RuntimeHostId = runtimeHostId
            ?? throw new ArgumentNullException(nameof(runtimeHostId));
    }

    public RuntimeHostId RuntimeHostId { get; }
}
