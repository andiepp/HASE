using Hase.Runtime.Northbound;

namespace Hase.DesktopHost;

public sealed record DesktopRuntimePropertyWriteRequest(
    RuntimeHostPropertyTarget Target,
    object RequestedValue,
    string InputSummary);
