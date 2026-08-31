using Hase.Core.Domain.Data;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;

namespace Hase.Mcnf.RfLab;

/// <summary>
/// Defines the first, read-only version of the normalized RF-Lab endpoint:
/// identity, detector measurement, indicator state, and clock-generator
/// presence.
/// </summary>
public static class RfLabReadOnlyDefinition
{
    public static DescriptorReference Reference { get; } =
        new(new DescriptorId("rflab-signal-lab"), version: 1);

    public static EndpointDescriptorDefinition EndpointDefinition { get; } = Create();

    internal static InstrumentDescriptor CreateInstrument(
        IReadOnlyList<PropertyDescriptor> additionalProperties,
        IReadOnlyList<Core.Domain.Commands.CommandDescriptor> commands)
    {
        var properties = new List<PropertyDescriptor>
        {
            new(
                RfLabProperties.ProductIdentity,
                RfLabProperties.ProductIdentityPath,
                "Product identity",
                new StringDataDescriptor())
            {
                Description = "Verified instrument product identity.",
                AccessMode = PropertyAccessMode.Read
            },
            new(
                RfLabProperties.NodeType,
                RfLabProperties.NodeTypePath,
                "Node type",
                new StringDataDescriptor())
            {
                Description = "Verified MCNF node-type information bytes.",
                AccessMode = PropertyAccessMode.Read
            },
            new(
                RfLabProperties.SensorLevel,
                RfLabProperties.SensorLevelPath,
                "Sensor level",
                new NumericDataDescriptor(
                    RfLabUnits.PowerLevel,
                    RfLabUnits.DecibelLevel,
                    new ValueRange(
                        RfLabSensorConversion.SensorLevelMinimum,
                        RfLabSensorConversion.SensorLevelMaximum)))
            {
                Description = "AD8307 50 Ohm detector level.",
                AccessMode = PropertyAccessMode.Read
            },
            new(
                RfLabProperties.SensorVoltage,
                RfLabProperties.SensorVoltagePath,
                "Sensor voltage",
                new NumericDataDescriptor(
                    Quantities.Voltage,
                    Units.Millivolt,
                    new ValueRange(
                        RfLabSensorConversion.SensorVoltageMinimum,
                        RfLabSensorConversion.SensorVoltageMaximum)))
            {
                Description = "Raw detector voltage against the 2.56 V reference.",
                AccessMode = PropertyAccessMode.Read
            },
            new(
                RfLabProperties.IndicatorEnabled,
                RfLabProperties.IndicatorEnabledPath,
                "Indicator enabled",
                new BooleanDataDescriptor())
            {
                Description = "State of the green indicator LED.",
                AccessMode = PropertyAccessMode.Read
            },
            new(
                RfLabProperties.ClockGeneratorPresent,
                RfLabProperties.ClockGeneratorPresentPath,
                "Clock generator present",
                new BooleanDataDescriptor())
            {
                Description = "Si5351 clock generator detected at node start.",
                AccessMode = PropertyAccessMode.Read
            }
        };
        properties.AddRange(additionalProperties);

        return new InstrumentDescriptor(
            new InstrumentId("rf-minilab-01"),
            "RF Signal Lab",
            new InstrumentKind("SignalGenerator"))
        {
            Metadata = new InstrumentMetadata
            {
                Manufacturer = "AEP",
                Model = RfLabIdentity.ProductIdentity,
                Description = "AD9910 DDS RF signal laboratory with detector and clock generator."
            },
            Interface = new InstrumentInterface(
                properties,
                commands)
        };
    }

    private static EndpointDescriptorDefinition Create() =>
        new(
            new EndpointMetadata
            {
                DisplayName = "RF-Lab Signal Laboratory",
                Description = "Read-only RF-Lab endpoint definition."
            },
            [CreateInstrument(additionalProperties: [], commands: [])]);
}
