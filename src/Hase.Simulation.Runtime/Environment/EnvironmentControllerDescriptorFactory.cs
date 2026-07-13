using Hase.Core.Domain.Commands;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;

namespace Hase.Simulation.Runtime.Environment;

/// <summary>
/// Creates the HASE descriptor for a simulated environment controller.
/// </summary>
public static class EnvironmentControllerDescriptorFactory
{
    public static readonly InstrumentId InstrumentId =
        new("simulation.environment-controller");

    public static readonly PropertyId TargetTemperaturePropertyId =
        new("simulation.environment-controller.target-temperature");

    public static readonly DescriptorPath TargetTemperaturePath =
        new("Environment", "TargetTemperature");

    public static readonly DescriptorPath ResetTargetTemperatureCommandPath =
        new("Environment", "ResetTargetTemperature");

    public static InstrumentDescriptor CreateDescriptor()
    {
        var targetTemperature =
            new PropertyDescriptor(
                id: TargetTemperaturePropertyId,
                path: TargetTemperaturePath,
                displayName: "Target Temperature",
                data: new NumericDataDescriptor(
                    quantity: Quantities.Temperature,
                    nativeUnit: Units.Celsius,
                    range: new ValueRange(-50.0, 100.0),
                    resolution: new Resolution(0.1)))
            {
                Description =
                    "Requested target temperature for the simulated environment.",

                AccessMode =
                    PropertyAccessMode.ReadWrite
            };

        var resetTargetTemperature =
            new CommandDescriptor(
                path: ResetTargetTemperatureCommandPath,
                displayName: "Reset Target Temperature")
            {
                Description =
                    "Resets the simulated target temperature to its default value."
            };

        return new InstrumentDescriptor(
            id: InstrumentId,
            name: "Simulated Environment Controller",
            kind: new InstrumentKind("environment-controller"))
        {
            Metadata = new InstrumentMetadata
            {
                Manufacturer = "HASE",
                Model = "Simulation Environment Controller",
                Description =
                    "Controls writable settings of a simulated environment."
            },

            Interface = new InstrumentInterface(
                properties:
                [
                    targetTemperature
                ],
                commands:
                [
                    resetTargetTemperature
                ])
        };
    }
}