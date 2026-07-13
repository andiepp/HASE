using Hase.Protocol;

namespace Hase.ProtocolExplorer.Formatting.Protocol;

internal interface IProtocolMessageFormatter
{
    bool CanFormat(
        ProtocolMessage message);

    IReadOnlyList<string> Format(
        ProtocolMessage message);
}