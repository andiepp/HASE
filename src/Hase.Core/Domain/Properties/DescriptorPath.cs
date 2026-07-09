using System.Collections;

namespace Hase.Core.Domain.Properties;

public sealed record DescriptorPath : IEnumerable<string>
{
    private readonly string _text;

    public DescriptorPath(params string[] segments)
        : this((IEnumerable<string>)segments)
    {
    }

    public DescriptorPath(IEnumerable<string> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);

        var normalizedSegments = segments
            .Select(RequireSegment)
            .ToArray();

        if (normalizedSegments.Length == 0)
            throw new ArgumentException(
                "A descriptor path must contain at least one segment.",
                nameof(segments));

        Segments = normalizedSegments;

        Name = normalizedSegments[^1];

        Depth = normalizedSegments.Length;

        Parent = Depth > 1
            ? new DescriptorPath(normalizedSegments.Take(Depth - 1))
            : null;

        _text = string.Join(".", normalizedSegments);
    }

    public IReadOnlyList<string> Segments { get; }

    public string Name { get; }

    public int Depth { get; }

    public bool IsRoot => Depth == 1;

    public DescriptorPath? Parent { get; }

    public DescriptorPath Append(string segment)
    {
        return new DescriptorPath(Segments.Append(RequireSegment(segment)));
    }

    public static DescriptorPath Parse(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException(
                "Path must not be empty.",
                nameof(path));

        return new DescriptorPath(path.Split('.'));
    }

    public IEnumerator<string> GetEnumerator() =>
        Segments.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() =>
        GetEnumerator();

    public override string ToString() => _text;

    private static string RequireSegment(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
            throw new ArgumentException(
                "A path segment must not be empty.",
                nameof(segment));

        if (segment.Contains('.'))
            throw new ArgumentException(
                "A path segment must not contain '.'.",
                nameof(segment));

        return segment.Trim();
    }

    public bool Equals(DescriptorPath? other)
    {
        return other is not null &&
               string.Equals(_text, other._text, StringComparison.Ordinal);
    }

    public override int GetHashCode()
    {
        return StringComparer.Ordinal.GetHashCode(_text);
    }

}