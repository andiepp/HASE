using Hase.DesktopHost.EndpointProfileTool;
using Hase.DesktopHost.EndpointProfileTool.Lab;
using Hase.Scpi.Kel103;

// This laboratory's composition tool: the published operations plus its own
// instrument's, and the name of the Runtime Host it must not edit beneath.
return await EndpointProfileToolApplication.RunAsync(
    args,
    [
        new AddKel103Operation(),
        new RemoveKel103Operation(),
        new MigrateKel103DefinitionOperation(
            "migrate-kel103-v4",
            previousVersion: 2,
            currentVersion: 4,
            Kel103ReadOnlyMeasurementDefinition.Reference,
            Kel103ControlledSetpointDefinition.Reference),
        new MigrateKel103DefinitionOperation(
            "migrate-kel103-v5",
            previousVersion: 4,
            currentVersion: 5,
            Kel103ControlledSetpointDefinition.Reference,
            Kel103ControlledInputDefinition.Reference)
    ],
    ["Hase.DesktopHost.App.Lab"]);
