namespace Hase.Client.Configuration;

/// <summary>
/// Creates one independent session controller for one enabled runtime-host
/// profile.
/// </summary>
public interface IRuntimeHostProfileSessionControllerFactory
{
    IRuntimeHostProfileSessionController Create(
        RuntimeHostProfile profile);
}
