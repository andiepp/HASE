using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;

namespace Hase.Scpi.Kel103;

public static class Kel103InputStateMapping
{
    public static PropertyId PropertyId { get; } = new("input-enabled");

    public static DescriptorPath PropertyPath { get; } =
        DescriptorPath.Parse("Input.Enabled");

    public static string Query { get; } = ":INPut?";

    public static bool ParseResponse(string response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return response switch
        {
            "OFF" => false,
            "ON" => true,
            _ => throw new InvalidDataException(
                "The input-state response does not match the supported KEL-103 format.")
        };
    }
}
