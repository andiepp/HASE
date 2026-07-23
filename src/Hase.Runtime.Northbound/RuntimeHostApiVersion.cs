namespace Hase.Runtime.Northbound;

/// <summary>
/// Identifies a version of the transport-independent northbound
/// runtime-host API contract.
/// </summary>
public readonly record struct RuntimeHostApiVersion(
    ushort Major,
    ushort Minor)
{
    /// <summary>
    /// Gets the northbound API contract version implemented by this library.
    /// </summary>
    public static RuntimeHostApiVersion Current
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