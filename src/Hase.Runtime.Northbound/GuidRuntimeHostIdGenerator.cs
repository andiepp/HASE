namespace Hase.Runtime.Northbound;

/// <summary>
/// Generates runtime-host identities from canonical GUID values.
/// </summary>
public sealed class GuidRuntimeHostIdGenerator
    : IRuntimeHostIdGenerator
{
    private const string RuntimeHostIdPrefix =
        "runtime-host-";

    /// <inheritdoc />
    public RuntimeHostId Generate()
    {
        string value =
            string.Concat(
                RuntimeHostIdPrefix,
                Guid.NewGuid()
                    .ToString(
                        "D")
                    .ToLowerInvariant());

        return new RuntimeHostId(
            value);
    }
}