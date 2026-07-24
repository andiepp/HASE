using Hase.CompactProtocol;
using Hase.ProtocolExplorer.Scenarios;
using Xunit;

namespace Hase.ProtocolExplorer.Tests;

public sealed class PhysicalArduinoUnoCompactDescriptorFactoryTests
{
    [Fact]
    public void CreateCompactDefinition_RegistersToggleLedCommandMapping()
    {
        CompactEndpointDefinition definition =
            PhysicalArduinoUnoCompactDescriptorFactory
                .CreateCompactDefinition();

        CompactCommandMapping mapping =
            Assert.Single(
                definition.CommandMappings);

        Assert.Equal(
            PhysicalArduinoUnoCompactDescriptorFactory
                .ToggleBuiltInLedCompactCommandId,
            mapping.CompactCommandId);

        Assert.Equal(
            PhysicalArduinoUnoCompactDescriptorFactory
                .ControllerInstrumentId,
            mapping.InstrumentId);

        Assert.Equal(
            PhysicalArduinoUnoCompactDescriptorFactory
                .ToggleBuiltInLedCommandPath,
            mapping.CommandPath);
    }
}