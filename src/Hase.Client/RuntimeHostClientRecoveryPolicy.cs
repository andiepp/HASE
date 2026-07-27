using System.Collections.ObjectModel;

namespace Hase.Client;

/// <summary>
/// Defines a finite, explicit remote-session recovery schedule.
/// </summary>
public sealed class RuntimeHostClientRecoveryPolicy
{
    public RuntimeHostClientRecoveryPolicy(
        IEnumerable<TimeSpan> delays)
    {
        ArgumentNullException.ThrowIfNull(
            delays);

        TimeSpan[] snapshot =
            delays.ToArray();

        if (snapshot.Any(
                delay =>
                    delay < TimeSpan.Zero))
        {
            throw new ArgumentOutOfRangeException(
                nameof(delays),
                "Recovery delays must not be negative.");
        }

        Delays =
            new ReadOnlyCollection<TimeSpan>(
                snapshot);
    }

    public IReadOnlyList<TimeSpan> Delays
    {
        get;
    }

    public static RuntimeHostClientRecoveryPolicy Conservative
    {
        get;
    } =
        new(
            [
                TimeSpan.Zero,
                TimeSpan.FromSeconds(
                    1),
                TimeSpan.FromSeconds(
                    2),
                TimeSpan.FromSeconds(
                    5),
                TimeSpan.FromSeconds(
                    10)
            ]);

    public bool TryGetDelay(
        RuntimeHostClientFailureCategory category,
        int attemptIndex,
        out TimeSpan delay)
    {
        if (attemptIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(attemptIndex));
        }

        bool recoverable =
            category is
                RuntimeHostClientFailureCategory.TransportUnavailable
                or RuntimeHostClientFailureCategory.ObservationGap;

        if (!recoverable
            || attemptIndex >= Delays.Count)
        {
            delay =
                default;

            return false;
        }

        delay =
            Delays[attemptIndex];

        return true;
    }
}
