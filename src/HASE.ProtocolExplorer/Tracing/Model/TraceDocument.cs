namespace Hase.ProtocolExplorer.Tracing.Model;

internal sealed class TraceDocument
{
    private readonly List<TraceSection> _sections = [];

    public IReadOnlyList<TraceSection> Sections => _sections;

    public void AddSection(
        string title,
        params string[] lines)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(lines);

        _sections.Add(
            new TraceSection(
                title,
                lines));
    }
}