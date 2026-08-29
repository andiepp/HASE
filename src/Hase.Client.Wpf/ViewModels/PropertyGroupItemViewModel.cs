using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace Hase.Client.Wpf.ViewModels;

/// <summary>
/// Presents the Properties of one instrument that declare the same
/// presentation group as a single reading.
/// </summary>
/// <remarks>
/// The group is built from descriptor metadata alone. Nothing here knows which
/// device produced the Properties: a group renders as a curve when every
/// member declares an abscissa, and as a compact row otherwise.
/// </remarks>
public sealed record PropertyGroupItemViewModel(
    string GroupId,
    IReadOnlyList<PropertyInventoryItemViewModel> Members)
{
    /// <summary>
    /// Width of the plotted curve area in device-independent units.
    /// </summary>
    public const double ChartWidth = 460.0;

    /// <summary>
    /// Height of the plotted curve area in device-independent units.
    /// </summary>
    public const double ChartHeight = 170.0;

    /// <summary>
    /// Gets a display name derived from the group identifier.
    /// </summary>
    public string DisplayName =>
        string.Join(
            ' ',
            GroupId
                .Split(
                    '-',
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(
                    segment =>
                        char.ToUpperInvariant(segment[0])
                        + segment[1..]));

    /// <summary>
    /// Gets the unit shared by every member, or null when they differ.
    /// </summary>
    public string? Unit
    {
        get
        {
            string?[] units =
                Members
                    .Select(member => member.Unit)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();

            return units.Length == 1
                ? units[0]
                : null;
        }
    }

    /// <summary>
    /// Gets the timestamp of the least recently acquired member, so the group
    /// never reads as fresher than its stalest value.
    /// </summary>
    /// <remarks>
    /// Null while any member has no acquisition timestamp at all, because no
    /// single timestamp can honestly describe the group in that state.
    /// </remarks>
    public string? OldestTimestampUtc
    {
        get
        {
            var timestamps =
                new List<DateTimeOffset>(
                    Members.Count);

            foreach (PropertyInventoryItemViewModel member in Members)
            {
                if (!TryParseTimestamp(
                        member.TimestampUtc,
                        out DateTimeOffset timestamp))
                {
                    return null;
                }

                timestamps.Add(
                    timestamp);
            }

            return timestamps.Count == 0
                ? null
                : timestamps
                    .Min()
                    .ToString(
                        "O",
                        CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Gets whether at least one member can currently be read.
    /// </summary>
    public bool CanRead =>
        Members.Any(member => member.CanRead);

    /// <summary>
    /// Gets whether any member exposes a read operation at all.
    /// </summary>
    public bool SupportsRead =>
        Members.Any(member => member.SupportsRead);

    /// <summary>
    /// Gets the members that carry both an abscissa and a numeric value,
    /// ordered along the abscissa.
    /// </summary>
    public IReadOnlyList<PropertyCurvePointViewModel> CurvePoints
    {
        get
        {
            var samples =
                new List<(double Abscissa, double Value,
                    PropertyInventoryItemViewModel Member)>();

            foreach (PropertyInventoryItemViewModel member in Members)
            {
                double? abscissa =
                    member.AbscissaValue;

                if (abscissa is null
                    || !member.TryGetNumericValue(
                        out double value))
                {
                    continue;
                }

                samples.Add(
                    (abscissa.Value, value, member));
            }

            if (samples.Count < 2)
            {
                return [];
            }

            samples.Sort(
                (left, right) =>
                    left.Abscissa.CompareTo(
                        right.Abscissa));

            double minimumAbscissa =
                samples[0].Abscissa;
            double maximumAbscissa =
                samples[^1].Abscissa;
            double abscissaSpan =
                maximumAbscissa - minimumAbscissa;
            double maximumValue =
                samples.Max(sample => sample.Value);
            double valueSpan =
                maximumValue > 0.0
                    ? maximumValue
                    : 1.0;

            var points =
                new List<PropertyCurvePointViewModel>(
                    samples.Count);

            foreach ((double abscissa, double value,
                PropertyInventoryItemViewModel member) in samples)
            {
                double x =
                    abscissaSpan > 0.0
                        ? (abscissa - minimumAbscissa)
                            / abscissaSpan
                            * ChartWidth
                        : ChartWidth / 2.0;
                double y =
                    ChartHeight
                    - (value / valueSpan * ChartHeight);

                points.Add(
                    new PropertyCurvePointViewModel(
                        x,
                        y,
                        abscissa,
                        value,
                        member.DisplayName,
                        member.AbscissaUnitSymbol,
                        member.Unit));
            }

            return points;
        }
    }

    /// <summary>
    /// Gets whether this group describes a sampled curve.
    /// </summary>
    public bool IsCurve =>
        CurvePoints.Count >= 2;

    /// <summary>
    /// Gets the plotted polyline of the sampled curve.
    /// </summary>
    public PointCollection PolylinePoints
    {
        get
        {
            var points =
                new PointCollection();

            foreach (PropertyCurvePointViewModel point in CurvePoints)
            {
                points.Add(
                    new Point(
                        point.X,
                        point.Y));
            }

            return points;
        }
    }

    public string MinimumAbscissaText =>
        FormatAbscissa(
            CurvePoints.Count == 0
                ? null
                : CurvePoints[0]);

    public string MaximumAbscissaText =>
        FormatAbscissa(
            CurvePoints.Count == 0
                ? null
                : CurvePoints[^1]);

    public string MaximumValueText
    {
        get
        {
            IReadOnlyList<PropertyCurvePointViewModel> points =
                CurvePoints;

            if (points.Count == 0)
            {
                return string.Empty;
            }

            double maximum =
                points.Max(point => point.Value);

            return maximum.ToString(
                    "G6",
                    CultureInfo.InvariantCulture)
                + (Unit is null
                    ? string.Empty
                    : " " + Unit);
        }
    }

    private static string FormatAbscissa(
        PropertyCurvePointViewModel? point)
    {
        if (point is null)
        {
            return string.Empty;
        }

        return point.Abscissa.ToString(
                "G6",
                CultureInfo.InvariantCulture)
            + (point.AbscissaUnitSymbol is null
                ? string.Empty
                : " " + point.AbscissaUnitSymbol);
    }

    private static bool TryParseTimestamp(
        string? text,
        out DateTimeOffset timestamp)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            timestamp = default;
            return false;
        }

        return DateTimeOffset.TryParse(
            text,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out timestamp);
    }
}

/// <summary>
/// Presents one plotted sample of a Property group curve.
/// </summary>
public sealed record PropertyCurvePointViewModel(
    double X,
    double Y,
    double Abscissa,
    double Value,
    string DisplayName,
    string? AbscissaUnitSymbol,
    string? ValueUnitSymbol)
{
    public string Tooltip =>
        string.Format(
            CultureInfo.InvariantCulture,
            "{0}: {1}{2} at {3}{4}",
            DisplayName,
            Value.ToString("G6", CultureInfo.InvariantCulture),
            ValueUnitSymbol is null
                ? string.Empty
                : " " + ValueUnitSymbol,
            Abscissa.ToString("G6", CultureInfo.InvariantCulture),
            AbscissaUnitSymbol is null
                ? string.Empty
                : " " + AbscissaUnitSymbol);
}
