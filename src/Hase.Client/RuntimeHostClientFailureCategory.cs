namespace Hase.Client;

/// <summary>
/// Identifies one transport-independent remote client failure category.
/// </summary>
public enum RuntimeHostClientFailureCategory
{
    Unspecified = 0,
    Authentication = 1,
    Authorization = 2,
    ApiCompatibility = 3,
    TransportUnavailable = 4,
    DeadlineExceeded = 5,
    Cancelled = 6,
    ObservationGap = 7,
    InvalidRemoteContract = 8,
    LocalConfiguration = 9,
    Unknown = 10
}
