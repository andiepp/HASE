using Hase.Client.Wpf.ViewModels;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;

namespace Hase.Client.Wpf.Tests;

public sealed class PropertyGroupItemViewModelTests
{
    [Fact]
    public void DisplayName_ShouldBeDerivedFromTheGroupIdentifier()
    {
        PropertyGroupItemViewModel group =
            Group(
                "spectral-scan",
                Member("f1", "F1", "10", 405.0),
                Member("f2", "F2", "20", 425.0));

        Assert.Equal(
            "Spectral Scan",
            group.DisplayName);
    }

    [Fact]
    public void OldestTimestampUtc_ShouldReportTheLeastRecentMember()
    {
        PropertyGroupItemViewModel group =
            Group(
                "uv-irradiance",
                Member("a", "UV-A", "3", timestampUtc: "2026-08-29T10:00:02.0000000+00:00"),
                Member("b", "UV-B", "2", timestampUtc: "2026-08-29T10:00:00.0000000+00:00"),
                Member("c", "UV-C", "1", timestampUtc: "2026-08-29T10:00:01.0000000+00:00"));

        Assert.Equal(
            "2026-08-29T10:00:00.0000000+00:00",
            group.OldestTimestampUtc);
    }

    [Fact]
    public void OldestTimestampUtc_MemberWithoutTimestamp_ShouldBeUnknown()
    {
        PropertyGroupItemViewModel group =
            Group(
                "uv-irradiance",
                Member("a", "UV-A", "3", timestampUtc: "2026-08-29T10:00:02.0000000+00:00"),
                Member("b", "UV-B", "No cached value"));

        Assert.Null(
            group.OldestTimestampUtc);
    }

    [Fact]
    public void Unit_MembersWithDifferentUnits_ShouldBeUnknown()
    {
        PropertyGroupItemViewModel group =
            Group(
                "mixed",
                Member("a", "A", "1", unit: "counts"),
                Member("b", "B", "2", unit: "V"));

        Assert.Null(
            group.Unit);
    }

    [Fact]
    public void Unit_MembersSharingOneUnit_ShouldBeThatUnit()
    {
        PropertyGroupItemViewModel group =
            Group(
                "shared",
                Member("a", "A", "1", unit: "counts"),
                Member("b", "B", "2", unit: "counts"));

        Assert.Equal(
            "counts",
            group.Unit);
    }

    [Fact]
    public void CurvePoints_ShouldBeOrderedAlongTheAbscissa()
    {
        PropertyGroupItemViewModel group =
            Group(
                "spectral-scan",
                Member("nir", "NIR", "100", 855.0),
                Member("f1", "F1", "50", 405.0),
                Member("f4", "F4", "25", 515.0));

        Assert.True(
            group.IsCurve);
        Assert.Equal(
            [405.0, 515.0, 855.0],
            group.CurvePoints
                .Select(point => point.Abscissa)
                .ToArray());
    }

    [Fact]
    public void CurvePoints_ShouldMapTheAbscissaRangeAcrossTheChartWidth()
    {
        PropertyGroupItemViewModel group =
            Group(
                "spectral-scan",
                Member("f1", "F1", "50", 400.0),
                Member("f4", "F4", "100", 600.0),
                Member("nir", "NIR", "0", 800.0));

        PropertyCurvePointViewModel[] points =
            group.CurvePoints.ToArray();

        Assert.Equal(0.0, points[0].X, precision: 6);
        Assert.Equal(
            PropertyGroupItemViewModel.ChartWidth / 2.0,
            points[1].X,
            precision: 6);
        Assert.Equal(
            PropertyGroupItemViewModel.ChartWidth,
            points[2].X,
            precision: 6);
    }

    [Fact]
    public void CurvePoints_ShouldInvertTheValueAxisAgainstTheMaximum()
    {
        PropertyGroupItemViewModel group =
            Group(
                "spectral-scan",
                Member("f1", "F1", "0", 400.0),
                Member("f4", "F4", "100", 600.0));

        PropertyCurvePointViewModel[] points =
            group.CurvePoints.ToArray();

        // Zero sits on the baseline, the maximum at the top of the plot.
        Assert.Equal(
            PropertyGroupItemViewModel.ChartHeight,
            points[0].Y,
            precision: 6);
        Assert.Equal(
            0.0,
            points[1].Y,
            precision: 6);
    }

    [Fact]
    public void CurvePoints_AllValuesZero_ShouldStillPlotOnTheBaseline()
    {
        PropertyGroupItemViewModel group =
            Group(
                "spectral-scan",
                Member("f1", "F1", "0", 400.0),
                Member("f4", "F4", "0", 600.0));

        Assert.True(
            group.IsCurve);
        Assert.All(
            group.CurvePoints,
            point =>
                Assert.Equal(
                    PropertyGroupItemViewModel.ChartHeight,
                    point.Y,
                    precision: 6));
    }

    [Fact]
    public void IsCurve_WithoutAbscissae_ShouldBeFalse()
    {
        PropertyGroupItemViewModel group =
            Group(
                "uv-irradiance",
                Member("a", "UV-A", "3"),
                Member("b", "UV-B", "2"),
                Member("c", "UV-C", "1"));

        Assert.False(
            group.IsCurve);
        Assert.Empty(
            group.CurvePoints);
        Assert.Empty(
            group.PolylinePoints);
    }

    [Fact]
    public void IsCurve_SingleAbscissa_ShouldBeFalse()
    {
        PropertyGroupItemViewModel group =
            Group(
                "spectral-scan",
                Member("f1", "F1", "50", 405.0),
                Member("ready", "Ready", "True"));

        Assert.False(
            group.IsCurve);
    }

    [Fact]
    public void CurvePoints_MemberWithoutCachedValue_ShouldBeExcluded()
    {
        PropertyGroupItemViewModel group =
            Group(
                "spectral-scan",
                Member("f1", "F1", "50", 405.0),
                Member("f2", "F2", "No cached value", 425.0),
                Member("f3", "F3", "70", 475.0));

        Assert.Equal(
            [405.0, 475.0],
            group.CurvePoints
                .Select(point => point.Abscissa)
                .ToArray());
    }

    [Fact]
    public void AxisText_ShouldReportTheAbscissaRangeAndMaximumValue()
    {
        PropertyGroupItemViewModel group =
            Group(
                "spectral-scan",
                Member("f1", "F1", "50", 405.0, unit: "counts"),
                Member("nir", "NIR", "120", 855.0, unit: "counts"));

        Assert.Equal("405 nm", group.MinimumAbscissaText);
        Assert.Equal("855 nm", group.MaximumAbscissaText);
        Assert.Equal("120 counts", group.MaximumValueText);
    }

    [Fact]
    public void Instrument_ShouldSeparateGroupedFromUngroupedProperties()
    {
        var instrument =
            new InstrumentInventoryItemViewModel(
                "instrument-01",
                "Sensor",
                "sensor",
                [
                    Member("f1", "F1", "50", 405.0),
                    Member("f2", "F2", "60", 425.0),
                    Member("ready", "Ready", "True")
                ],
                []);

        PropertyGroupItemViewModel group =
            Assert.Single(
                instrument.PropertyGroups);

        Assert.True(
            instrument.HasPropertyGroups);
        Assert.Equal(
            "spectral-scan",
            group.GroupId);
        Assert.Equal(
            2,
            group.Members.Count);
        Assert.Equal(
            "Ready",
            Assert.Single(
                instrument.UngroupedProperties).DisplayName);
    }

    private static PropertyGroupItemViewModel Group(
        string groupId,
        params PropertyInventoryItemViewModel[] members)
    {
        return new PropertyGroupItemViewModel(
            groupId,
            members);
    }

    private static PropertyInventoryItemViewModel Member(
        string propertyId,
        string displayName,
        string value,
        double? abscissaNanometres = null,
        string? unit = null,
        string? timestampUtc = null)
    {
        PropertyPresentation? presentation =
            abscissaNanometres is null
                ? null
                : new PropertyPresentation
                {
                    GroupId = "spectral-scan",
                    Abscissa =
                        new QuantityValue(
                            abscissaNanometres.Value,
                            Units.Nanometre)
                };

        var descriptor =
            new PropertyDescriptor(
                new PropertyId(propertyId),
                new DescriptorPath("Group", displayName.Replace("-", string.Empty)),
                displayName,
                new NumericDataDescriptor(
                    Quantities.Count,
                    Units.Count))
            {
                AccessMode = PropertyAccessMode.Read,
                Presentation = presentation
            };

        var target =
            new RemotePropertyTarget(
                new RemoteEndpointAttachmentKey(
                    new EndpointId("endpoint-01"),
                    new RemoteEndpointAttachmentGeneration(
                        Guid.Parse("31bda489-b8ec-49bf-bf69-1947b13e37cd"))),
                new InstrumentId("instrument-01"),
                descriptor.Id);

        return new PropertyInventoryItemViewModel(
            target,
            descriptor.Id.Value,
            descriptor.Path.ToString(),
            descriptor.DisplayName,
            descriptor.AccessMode.ToString(),
            "Numeric",
            unit,
            value,
            timestampUtc,
            "Good",
            false,
            true,
            true,
            false,
            false,
            descriptor);
    }
}
