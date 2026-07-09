namespace Hase.Core.Domain.Properties;

public sealed record PropertyPath
{
    private readonly string _text;

    public PropertyPath(params string[] segments)
        : this((IEnumerable<string>)segments)
    {
    }

    public PropertyPath(IEnumerable<string> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);

        var normalizedSegments = segments
            .Select(segment => RequireSegment(segment))
            .ToArray();

        if (normalizedSegments.Length == 0)
        {
            throw new ArgumentException(
                "A property path must contain at least one segment.",
                nameof(segments));
        }

        Segments = normalizedSegments;
        Name = normalizedSegments[^1];
        Depth = normalizedSegments.Length;
        Parent = Depth > 1
            ? new PropertyPath(normalizedSegments.Take(Depth - 1))
            : null;

        _text = string.Join(".", normalizedSegments);
    }

    public IReadOnlyList<string> Segments { get; }

    public string Name { get; }

    public int Depth { get; }

    public PropertyPath? Parent { get; }

    public PropertyPath Append(string segment)
    {
        return new PropertyPath(Segments.Append(RequireSegment(segment)));
    }

    public override string ToString() => _text;

    private static string RequireSegment(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
        {
            throw new ArgumentException(
                "A property path segment must not be null, empty, or whitespace.",
                nameof(segment));
        }

        if (segment.Contains('.'))
        {
            throw new ArgumentException(
                "A property path segment must not contain '.'.",
                nameof(segment));
        }

        return segment.Trim();
    }
}