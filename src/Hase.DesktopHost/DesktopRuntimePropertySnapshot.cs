using Hase.Core.Domain.Properties;
using Hase.Runtime.Northbound;

namespace Hase.DesktopHost;

public sealed record DesktopRuntimePropertySnapshot(
    RuntimeHostPropertyTarget Target,
    string PropertyId,
    string DisplayName,
    string Path,
    string Access,
    string Value,
    string Quality,
    string TimestampUtc,
    bool IsKnown,
    DesktopRuntimePropertyDataKind DataKind,
    bool CanRead,
    bool CanWrite,
    bool? BooleanValue,
    bool IsEndpointReady,
    PropertyDescriptor? Descriptor = null,
    object? CurrentValue = null);
