using Hase.Core.Domain.Commands;
using Hase.Core.Domain.Instruments;

namespace Hase.Scpi.Kel103.Tests;

/// <summary>
/// Covers version 6, which is version 5 plus the command declarations that
/// let a presentation layer offer the modes without knowing this instrument.
/// </summary>
public sealed class Kel103DeclaredControlDefinitionTests
{
    [Fact]
    public void Reference_ShouldBeVersionSixOfTheSameDefinition()
    {
        Assert.Equal(
            Kel103IdentityDefinition.Reference.Id,
            Kel103DeclaredControlDefinition.Reference.Id);
        Assert.Equal(
            (ushort)6,
            Kel103DeclaredControlDefinition.Reference.Version);
    }

    [Fact]
    public void ModeCommands_ShouldDeclareOneResolvableSelection()
    {
        CommandDescriptor[] modes = ModeCommands();

        Assert.Equal(5, modes.Length);
        Assert.All(
            modes,
            command =>
            {
                Assert.NotNull(command.Presentation);
                Assert.True(command.Presentation!.DeclaresResolvableSelection);
                Assert.Equal(
                    "operating-mode",
                    command.Presentation.SelectionGroupId);
                Assert.Equal(
                    "Operating.Mode",
                    command.Presentation.SelectionStatePath!.ToString());
            });
        Assert.Equal(
            ["CC", "CV", "CR", "CW", "SHORT"],
            modes.Select(command => command.Presentation!.ShortLabel));
    }

    [Fact]
    public void ModeCommands_ShouldResolveTheReportedValueRegardlessOfCasing()
    {
        CommandDescriptor shortCircuit =
            ModeCommands().Single(
                command => command.Presentation!.ShortLabel == "SHORT");

        Assert.True(shortCircuit.Presentation!.IsInEffect("SHORT"));
        Assert.True(shortCircuit.Presentation.IsInEffect("SHORt"));
        Assert.False(shortCircuit.Presentation.IsInEffect("CC"));
        Assert.False(shortCircuit.Presentation.IsInEffect(null));
    }

    [Fact]
    public void InputCommands_ShouldDeclareLabelsWithoutASelection()
    {
        CommandDescriptor[] inputs =
            Commands()
                .Where(command =>
                    command.Path.ToString().StartsWith(
                        "Input.",
                        StringComparison.Ordinal))
                .ToArray();

        Assert.Equal(2, inputs.Length);
        Assert.All(
            inputs,
            command =>
            {
                Assert.NotNull(command.Presentation);
                Assert.False(command.Presentation!.DeclaresResolvableSelection);
                Assert.Null(command.Presentation.SelectionGroupId);
                Assert.NotNull(command.Presentation.ShortLabel);
            });
    }

    [Fact]
    public void ShortCircuitActivation_ShouldDeclareItsConfirmation()
    {
        CommandDescriptor activation =
            Commands().Single(
                command =>
                    command.Path.ToString() == "ShortCircuit.Activate");

        Assert.True(activation.RequiresExplicitConfirmation);
        Assert.NotNull(activation.Argument);
    }

    [Fact]
    public void VersionFive_ShouldRemainWithoutDeclarations()
    {
        Assert.All(
            Kel103ControlledInputDefinition.EndpointDefinition
                .Instruments.Single().Interface.Commands,
            command => Assert.Null(command.Presentation));
    }

    private static IReadOnlyList<CommandDescriptor> Commands() =>
        Kel103DeclaredControlDefinition.EndpointDefinition
            .Instruments.Single().Interface.Commands;

    private static CommandDescriptor[] ModeCommands() =>
        Commands()
            .Where(command =>
                command.Presentation?.SelectionGroupId is not null)
            .ToArray();
}
