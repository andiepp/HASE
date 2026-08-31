using Hase.Core.Domain.Commands;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Properties;

namespace Hase.Mcnf.RfLab;

/// <summary>
/// Defines the second, controlled version of the normalized RF-Lab
/// endpoint. It adds the writable, host-staged target properties and the
/// parameterless apply commands that push them to the node. The node offers
/// no state readback; the acknowledged MCNF response is the execution
/// confirmation.
/// </summary>
public static class RfLabControlledSignalDefinition
{
    public static DescriptorReference Reference { get; } =
        new(RfLabReadOnlyDefinition.Reference.Id, version: 2);

    public static EndpointDescriptorDefinition EndpointDefinition { get; } = Create();

    private static EndpointDescriptorDefinition Create()
    {
        var targetProperties = RfLabTargetMapping.All
            .Select(mapping => new PropertyDescriptor(
                mapping.PropertyId,
                mapping.PropertyPath,
                DisplayName(mapping),
                new NumericDataDescriptor(
                    mapping.Quantity,
                    mapping.Unit,
                    new ValueRange(mapping.Minimum, mapping.Maximum)))
            {
                Description = "Host-staged target; an apply command pushes it to the node.",
                AccessMode = PropertyAccessMode.ReadWrite
            })
            .ToArray();

        var commands = RfLabCommandMapping.All
            .Select(mapping => new CommandDescriptor(
                mapping.CommandPath,
                mapping.DisplayName))
            .ToArray();

        return new EndpointDescriptorDefinition(
            new EndpointMetadata
            {
                DisplayName = "RF-Lab Signal Laboratory",
                Description = "Controlled RF-Lab signal-generation definition."
            },
            [
                RfLabReadOnlyDefinition.CreateInstrument(
                    targetProperties,
                    commands)
            ]);
    }

    private static string DisplayName(RfLabTargetMapping mapping) =>
        mapping.PropertyId.Value switch
        {
            "target-frequency" => "Target frequency",
            "target-attenuation" => "Target attenuation",
            "modulation-frequency" => "Modulation frequency",
            "am-depth" => "AM depth",
            "fm-deviation" => "FM deviation",
            "sweep-start-frequency" => "Sweep start frequency",
            "sweep-stop-frequency" => "Sweep stop frequency",
            "sweep-time" => "Sweep time",
            "clock0-frequency" => "Clock 0 frequency",
            "clock1-frequency" => "Clock 1 frequency",
            "clock2-frequency" => "Clock 2 frequency",
            _ => mapping.PropertyId.Value
        };
}
