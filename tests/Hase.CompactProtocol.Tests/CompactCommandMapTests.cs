using Hase.Core.Domain.Commands;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Xunit;

namespace Hase.CompactProtocol.Tests;

public sealed class CompactCommandMapTests
{
    private static readonly InstrumentId InstrumentId =
        new(
            "controller-one");

    private static readonly DescriptorPath CommandPath =
        new(
            "Controller",
            "ToggleLed");

    [Fact]
    public void Constructor_ValidMapping_SupportsBothLookups()
    {
        var mapping =
            new CompactCommandMapping(
                0x01,
                InstrumentId,
                CommandPath);

        var map =
            new CompactCommandMap(
                CreateDefinition(),
                [
                    mapping
                ]);

        Assert.Same(
            mapping,
            map.Find(
                compactCommandId: 0x01));

        Assert.Same(
            mapping,
            map.Find(
                InstrumentId,
                CommandPath));
    }

    [Fact]
    public void Constructor_DuplicateCompactIdentifier_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new CompactCommandMap(
                CreateDefinition(),
                [
                    new CompactCommandMapping(
                        0x01,
                        InstrumentId,
                        CommandPath),
                    new CompactCommandMapping(
                        0x01,
                        InstrumentId,
                        CreateSecondCommandPath())
                ]));
    }

    [Fact]
    public void Constructor_DuplicateLogicalTarget_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new CompactCommandMap(
                CreateDefinition(),
                [
                    new CompactCommandMapping(
                        0x01,
                        InstrumentId,
                        CommandPath),
                    new CompactCommandMapping(
                        0x02,
                        InstrumentId,
                        CommandPath)
                ]));
    }

    [Fact]
    public void Constructor_UnknownInstrument_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new CompactCommandMap(
                CreateDefinition(),
                [
                    new CompactCommandMapping(
                        0x01,
                        new InstrumentId(
                            "missing"),
                        CommandPath)
                ]));
    }

    [Fact]
    public void Constructor_UnknownCommand_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new CompactCommandMap(
                CreateDefinition(),
                [
                    new CompactCommandMapping(
                        0x01,
                        InstrumentId,
                        new DescriptorPath(
                            "Controller",
                            "Missing"))
                ]));
    }

    [Fact]
    public void Find_UnknownTargets_ReturnsNull()
    {
        var map =
            new CompactCommandMap(
                CreateDefinition(),
                []);

        Assert.Null(
            map.Find(
                compactCommandId: 0x01));

        Assert.Null(
            map.Find(
                InstrumentId,
                CommandPath));
    }

    [Fact]
    public void Mapping_ZeroIdentifier_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CompactCommandMapping(
                0,
                InstrumentId,
                CommandPath));
    }

    [Fact]
    public void Mapping_NullLogicalIdentities_Throw()
    {
        Assert.Throws<ArgumentNullException>(
            () => new CompactCommandMapping(
                0x01,
                null!,
                CommandPath));

        Assert.Throws<ArgumentNullException>(
            () => new CompactCommandMapping(
                0x01,
                InstrumentId,
                null!));
    }

    private static EndpointDescriptorDefinition CreateDefinition()
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
                                "Toggle LED"),
                            new CommandDescriptor(
                                CreateSecondCommandPath(),
                                "Reset LED")
                        ])
            };

        return new EndpointDescriptorDefinition(
            new EndpointMetadata(),
            [
                instrumentDescriptor
            ]);
    }

    private static DescriptorPath CreateSecondCommandPath()
    {
        return new DescriptorPath(
            "Controller",
            "ResetLed");
    }
}