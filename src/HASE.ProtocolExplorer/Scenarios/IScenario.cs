namespace Hase.ProtocolExplorer.Scenarios;

public interface IScenario
{
    string Name { get; }

    void Execute();
}