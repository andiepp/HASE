using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;

namespace Hase.Mcnf.RfLab;

/// <summary>
/// Identifiers of the read-only RF-Lab properties.
/// </summary>
public static class RfLabProperties
{
    public static readonly PropertyId ProductIdentity = new("product-identity");
    public static readonly DescriptorPath ProductIdentityPath =
        DescriptorPath.Parse("Identity.Product");

    public static readonly PropertyId NodeType = new("node-type");
    public static readonly DescriptorPath NodeTypePath =
        DescriptorPath.Parse("Identity.NodeType");

    public static readonly PropertyId SensorLevel = new("sensor-level");
    public static readonly DescriptorPath SensorLevelPath =
        DescriptorPath.Parse("Measurement.SensorLevel");

    public static readonly PropertyId SensorVoltage = new("sensor-voltage");
    public static readonly DescriptorPath SensorVoltagePath =
        DescriptorPath.Parse("Measurement.SensorVoltage");

    public static readonly PropertyId IndicatorEnabled = new("indicator-enabled");
    public static readonly DescriptorPath IndicatorEnabledPath =
        DescriptorPath.Parse("Indicator.Enabled");

    public static readonly PropertyId ClockGeneratorPresent = new("clock-generator-present");
    public static readonly DescriptorPath ClockGeneratorPresentPath =
        DescriptorPath.Parse("Clock.GeneratorPresent");
}
