using Hase.ProtocolExplorer.Tracing.Model;

namespace Hase.ProtocolExplorer.Formatting;

internal sealed class ConsoleTraceFormatter
{
    public void Write(
        TraceDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        foreach (TraceSection section in document.Sections)
        {
            Console.WriteLine(section.Title);

            Console.WriteLine(
                new string('-', section.Title.Length));

            foreach (string line in section.Lines)
            {
                Console.WriteLine(line);
            }

            Console.WriteLine();
        }
    }
}