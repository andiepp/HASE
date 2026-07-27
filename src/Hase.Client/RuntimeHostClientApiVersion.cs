namespace Hase.Client;

/// <summary>
/// Identifies one version of the remote runtime-host API represented by the
/// normalized client model.
/// </summary>
public readonly record struct RuntimeHostClientApiVersion
{
    /// <summary>
    /// Initializes one remote runtime-host API version.
    /// </summary>
    public RuntimeHostClientApiVersion(
        ushort major,
        ushort minor)
    {
        if (major == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(major),
                major,
                "The API major version must be greater than zero.");
        }

        Major =
            major;
        Minor =
            minor;
    }

    /// <summary>
    /// Gets the API major version.
    /// </summary>
    public ushort Major
    {
        get;
    }

    /// <summary>
    /// Gets the API minor version.
    /// </summary>
    public ushort Minor
    {
        get;
    }

    /// <summary>
    /// Gets the remote API version supported by the first HASE client adapter.
    /// </summary>
    public static RuntimeHostClientApiVersion Current
    {
        get;
    } =
        new(
            1,
            0);

    /// <inheritdoc />
    public override string ToString()
    {
        return $"{Major}.{Minor}";
    }
}
