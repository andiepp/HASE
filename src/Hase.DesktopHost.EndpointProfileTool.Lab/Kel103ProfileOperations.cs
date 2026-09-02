using Hase.Core.Domain.Descriptors;
using Hase.DesktopHost.Configuration;
using Hase.Scpi.Kel103;

namespace Hase.DesktopHost.EndpointProfileTool.Lab;

/// <summary>
/// Adds one KEL-103 endpoint to a composition.
/// </summary>
internal sealed class AddKel103Operation : IEndpointProfileOperation
{
    public string Name => "add-kel103";

    public IReadOnlyList<string> UsageLines =>
        ["  add-kel103 <composition> <endpoint-id> <serial-target>"];

    public async Task<EndpointProfileOperationResult?> ExecuteAsync(
        EndpointProfileOperationContext context)
    {
        if (context.Arguments.Count != 4)
        {
            return null;
        }

        await context.Editor.AddKel103Async(
            context.ProfilePath,
            context.BackupPath,
            new DesktopRuntimeHostKel103SerialEndpointProfile(
                context.EndpointId,
                Kel103ReadOnlyMeasurementDefinition.Reference.Id.Value,
                Kel103ReadOnlyMeasurementDefinition.Reference.Version,
                context.Arguments[3],
                DesktopRuntimeHostKel103SerialEndpointProfile.SupportedBaudRate));

        return new EndpointProfileOperationResult("Kel103Serial");
    }

    public string DescribeFailure(Exception exception) => exception.Message;
}

/// <summary>
/// Removes one KEL-103 endpoint from a composition.
/// </summary>
internal sealed class RemoveKel103Operation : IEndpointProfileOperation
{
    public string Name => "remove-kel103";

    public IReadOnlyList<string> UsageLines =>
        ["  remove-kel103 <composition> <endpoint-id> <same-id-confirmation>"];

    public async Task<EndpointProfileOperationResult?> ExecuteAsync(
        EndpointProfileOperationContext context)
    {
        if (context.Arguments.Count != 4
            || context.Arguments[3] != context.EndpointId)
        {
            return null;
        }

        await context.Editor.RemoveKel103Async(
            context.ProfilePath,
            context.BackupPath,
            context.EndpointId);

        return new EndpointProfileOperationResult("Kel103Serial");
    }

    public string DescribeFailure(Exception exception) => exception.Message;
}

/// <summary>
/// Moves one KEL-103 endpoint from one exact definition version to the next.
/// </summary>
/// <remarks>
/// The migration reports its retained backup rather than naming it, because
/// the editor replaces the composition in place and the previous file is
/// what it leaves behind.
/// </remarks>
internal sealed class MigrateKel103DefinitionOperation : IEndpointProfileOperation
{
    private readonly ushort previousVersion;
    private readonly ushort currentVersion;
    private readonly DescriptorReference expectedCurrent;
    private readonly DescriptorReference replacement;

    public MigrateKel103DefinitionOperation(
        string name,
        ushort previousVersion,
        ushort currentVersion,
        DescriptorReference expectedCurrent,
        DescriptorReference replacement)
    {
        Name = name;
        this.previousVersion = previousVersion;
        this.currentVersion = currentVersion;
        this.expectedCurrent = expectedCurrent;
        this.replacement = replacement;
    }

    public string Name { get; }

    public IReadOnlyList<string> UsageLines =>
        [$"  {Name} <composition> <endpoint-id> <same-endpoint-id-confirmation>"];

    public async Task<EndpointProfileOperationResult?> ExecuteAsync(
        EndpointProfileOperationContext context)
    {
        if (context.Arguments.Count != 4
            || context.Arguments[3] != context.EndpointId)
        {
            return null;
        }

        await context.Editor.MigrateKel103DefinitionAsync(
            context.ProfilePath,
            context.BackupPath,
            context.EndpointId,
            expectedCurrent,
            replacement);

        return new EndpointProfileOperationResult(
            "Kel103Serial",
            [
                $"Previous definition version: {previousVersion}",
                $"Current definition version: {currentVersion}"
            ],
            BackupRetained: true);
    }

    public string DescribeFailure(Exception exception) => exception switch
    {
        KeyNotFoundException => "The selected KEL-103 endpoint is not registered.",
        InvalidOperationException =>
            $"The selected KEL-103 endpoint does not use the exact version {previousVersion} definition.",
        InvalidDataException => "The endpoint composition is not valid for migration.",
        OperationCanceledException => "The KEL-103 definition migration was cancelled.",
        _ => "The KEL-103 definition migration failed. Inspect the active profile and retained backups before retrying."
    };
}
