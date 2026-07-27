namespace Hase.Client;

/// <summary>
/// Represents the closed set of normalized values supported by remote API
/// version 1.
/// </summary>
public sealed record RemoteValue
{
    private RemoteValue(
        RemoteValueKind kind,
        bool? booleanValue = null,
        string? stringValue = null,
        double? numericValue = null)
    {
        Kind =
            kind;
        BooleanValue =
            booleanValue;
        StringValue =
            stringValue;
        NumericValue =
            numericValue;
    }

    /// <summary>
    /// Gets the selected normalized value kind.
    /// </summary>
    public RemoteValueKind Kind
    {
        get;
    }

    /// <summary>
    /// Gets the Boolean value when <see cref="Kind"/> is
    /// <see cref="RemoteValueKind.Boolean"/>.
    /// </summary>
    public bool? BooleanValue
    {
        get;
    }

    /// <summary>
    /// Gets the string value when <see cref="Kind"/> is
    /// <see cref="RemoteValueKind.String"/>.
    /// </summary>
    public string? StringValue
    {
        get;
    }

    /// <summary>
    /// Gets the numeric value when <see cref="Kind"/> is
    /// <see cref="RemoteValueKind.Numeric"/>.
    /// </summary>
    public double? NumericValue
    {
        get;
    }

    /// <summary>
    /// Creates one normalized Boolean value.
    /// </summary>
    public static RemoteValue FromBoolean(
        bool value)
    {
        return new RemoteValue(
            RemoteValueKind.Boolean,
            booleanValue:
                value);
    }

    /// <summary>
    /// Creates one normalized string value.
    /// </summary>
    public static RemoteValue FromString(
        string value)
    {
        ArgumentNullException.ThrowIfNull(
            value);

        return new RemoteValue(
            RemoteValueKind.String,
            stringValue:
                value);
    }

    /// <summary>
    /// Creates one normalized numeric value.
    /// </summary>
    public static RemoteValue FromNumeric(
        double value)
    {
        return new RemoteValue(
            RemoteValueKind.Numeric,
            numericValue:
                value);
    }
}
