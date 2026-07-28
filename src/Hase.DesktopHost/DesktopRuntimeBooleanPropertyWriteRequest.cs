using Hase.Runtime.Northbound;

namespace Hase.DesktopHost;

public sealed record DesktopRuntimeBooleanPropertyWriteRequest(
    RuntimeHostPropertyTarget Target,
    bool RequestedValue);
