using Hase.Core.Domain.Commands;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;

namespace Hase.CompactProtocol.Tests;

public sealed class CompactEndpointCommandDefinitionTests
{
    private static readonly InstrumentId InstrumentId =
        new(
            "controller-one");

    private static readonly DescriptorPath CommandPath =
        new(
            "Controller",
            "ToggleLed");

    [Fact]
    public void FiveArgumentConstructor_RetainsCommandMappings()
    {
        var commandMappings =
            new List<CompactCommandMapping>
            {
                CreateCommandMapping()
            };

        var definition =
            new CompactEndpointDefinition(
                CreateDescriptorReference(),
                CreateDescriptorDefinition(),
                propertyMappings: [],
                eventMappings: [],
                commandMappings:
                    commandMappings);

        commandMappings.Clear();

        CompactCommandMapping retainedMapping =
            Assert.Single(
                definition.CommandMappings);

        Assert.Equal(
            0x01,
            retainedMapping.CompactCommandId);

        CompactCommandMap commandMap =
            definition.CreateCommandMap();

        Assert.Same(
            retainedMapping,
            commandMap.Find(
                InstrumentId,
                CommandPath));
    }

    [Fact]
    public void ExistingConstructors_UseEmptyCommandMappings()
    {
        var threeArgumentDefinition =
            new CompactEndpointDefinition(
                CreateDescriptorReference(),
                CreateDescriptorDefinition(),
                propertyMappings: []);

        var fourArgumentDefinition =
            new CompactEndpointDefinition(
                CreateDescriptorReference(),
                CreateDescriptorDefinition(),
                propertyMappings: [],
                eventMappings: []);

        Assert.Empty(
            threeArgumentDefinition.CommandMappings);

        Assert.Empty(
            fourArgumentDefinition.CommandMappings);
    }

    [Fact]
    public void FiveArgumentConstructor_NullCommandMappings_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new CompactEndpointDefinition(
                CreateDescriptorReference(),
                CreateDescriptorDefinition(),
                propertyMappings: [],
                eventMappings: [],
                commandMappings: null!));
    }

    [Fact]
    public void FiveArgumentConstructor_InvalidCommandTarget_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new CompactEndpointDefinition(
                CreateDescriptorReference(),
                new EndpointDescriptorDefinition(),
                propertyMappings: [],
                eventMappings: [],
                commandMappings:
                [
                    CreateCommandMapping()
                ]));
    }

    private static CompactCommandMapping CreateCommandMapping()
    {
        return new CompactCommandMapping(
            compactCommandId: 0x01,
            InstrumentId,
            CommandPath);
    }

    private static DescriptorReference CreateDescriptorReference()
    {
        return new DescriptorReference(
            new DescriptorId(
                "compact-command-definition"),
            version: 1);
    }

    private static EndpointDescriptorDefinition
        CreateDescriptorDefinition()
    {
        var instrumentDescriptor =
            new InstrumentDescriptor(
                InstrumentId,
                "Controller",
                new InstrumentKind(
                    "test"))
            {
                Interface =
                    new InstrumentInterface(
                        commands:
                        [
                            new CommandDescriptor(
                                CommandPath,
                                "Toggle LED")
                        ])
            };

        return new EndpointDescriptorDefinition(
            new EndpointMetadata(),
            [
                instrumentDescriptor
            ]);
    }
}