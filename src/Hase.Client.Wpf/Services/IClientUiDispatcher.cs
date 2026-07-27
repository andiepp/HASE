namespace Hase.Client.Wpf.Services;

/// <summary>
/// Marshals presentation updates onto the client application's UI thread.
/// </summary>
public interface IClientUiDispatcher
{
    void Post(
        Action action);
}
