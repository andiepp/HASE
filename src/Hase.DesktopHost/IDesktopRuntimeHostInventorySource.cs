namespace Hase.DesktopHost;

public interface IDesktopRuntimeHostInventorySource
{
    IReadOnlyList<DesktopRuntimeEndpointSnapshot> Capture();
}
