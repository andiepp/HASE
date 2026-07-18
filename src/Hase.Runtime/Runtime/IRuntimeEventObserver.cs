namespace Hase.Runtime.Runtime;

/// <summary>
/// Observes occurrences of runtime events.
/// </summary>
public interface IRuntimeEventObserver
{
    /// <summary>
    /// Receives one runtime event occurrence.
    /// </summary>
    /// <param name="occurrence">
    /// Event occurrence delivered by the runtime.
    /// </param>
    void OnRuntimeEventOccurred(
        RuntimeEventOccurrence occurrence);
}