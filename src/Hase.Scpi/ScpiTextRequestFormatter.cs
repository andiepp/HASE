using System.Text;

namespace Hase.Scpi;

public sealed class ScpiTextRequestFormatter
{
    private readonly byte[] terminator;

    public ScpiTextRequestFormatter(ScpiTextFramingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        terminator = ScpiTextTerminators.GetBytes(options.CommandTerminator);
    }

    public byte[] Format(string request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request);

        foreach (var character in request)
        {
            if (character is < ' ' or > '~')
            {
                throw new ArgumentException(
                    "SCPI request text must contain printable ASCII characters only and must not contain a line terminator.",
                    nameof(request));
            }
        }

        var requestBytes = Encoding.ASCII.GetBytes(request);
        var framed = new byte[requestBytes.Length + terminator.Length];
        requestBytes.CopyTo(framed, 0);
        terminator.CopyTo(framed, requestBytes.Length);
        return framed;
    }
}
