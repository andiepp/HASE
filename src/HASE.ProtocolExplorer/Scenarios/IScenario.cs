namespace Hase.ProtocolExplorer.Scenarios;

internal interface IScenario
{
    string Name { get; }

    void Execute();
}