using System.Globalization;
using System.IO;

namespace Hase.DesktopHost.Configuration;

/// <summary>
/// One configured endpoint, named by the provider that supplies it.
/// </summary>
/// <remarks>
/// The settings are carried as text and are not interpreted here. The
/// provider named by <see cref="ProviderId"/> is the only component that
/// knows what its own settings mean, which is what lets a composition name an
/// endpoint kind this library has never heard of.
/// </remarks>
public sealed class DesktopRuntimeHostEndpointEntry
{
    private readonly IReadOnlyDictionary<string, string> settings;

    /// <summary>
    /// Initializes one configured endpoint.
    /// </summary>
    public DesktopRuntimeHostEndpointEntry(
        string providerId,
        string expectedEndpointId,
        IEnumerable<KeyValuePair<string, string>>? settings = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedEndpointId);

        ProviderId = providerId.Trim();
        ExpectedEndpointId = expectedEndpointId.Trim();

        var collected = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (KeyValuePair<string, string> setting in settings ?? [])
        {
            if (string.IsNullOrWhiteSpace(setting.Key))
            {
                throw new ArgumentException(
                    "An endpoint setting name must not be empty.",
                    nameof(settings));
            }

            if (setting.Value is null)
            {
                throw new ArgumentException(
                    $"Endpoint setting '{setting.Key}' must not be null.",
                    nameof(settings));
            }

            if (!collected.TryAdd(setting.Key.Trim(), setting.Value))
            {
                throw new ArgumentException(
                    $"Endpoint setting '{setting.Key}' occurs more than once.",
                    nameof(settings));
            }
        }

        this.settings = collected;
    }

    /// <summary>
    /// Gets the identifier of the provider that supplies this endpoint.
    /// </summary>
    public string ProviderId { get; }

    /// <summary>
    /// Gets the authoritative endpoint identity this entry expects.
    /// </summary>
    public string ExpectedEndpointId { get; }

    /// <summary>
    /// Gets the provider-specific settings, uninterpreted.
    /// </summary>
    public IReadOnlyDictionary<string, string> Settings => settings;

    /// <summary>
    /// Indicates whether the named setting is present.
    /// </summary>
    public bool HasSetting(string name) =>
        !string.IsNullOrWhiteSpace(name) && settings.ContainsKey(name.Trim());

    /// <summary>
    /// Reads a required text setting.
    /// </summary>
    /// <exception cref="InvalidDataException">The setting is absent.</exception>
    public string RequireString(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return settings.TryGetValue(name.Trim(), out string? value)
            ? value
            : throw new InvalidDataException(
                $"Endpoint '{ExpectedEndpointId}' requires setting '{name}'.");
    }

    /// <summary>
    /// Reads a required 32-bit integer setting.
    /// </summary>
    /// <exception cref="InvalidDataException">
    /// The setting is absent or is not an integer.
    /// </exception>
    public int RequireInt32(string name) =>
        int.TryParse(
            RequireString(name),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out int value)
            ? value
            : throw new InvalidDataException(
                $"Endpoint '{ExpectedEndpointId}' setting '{name}' is not an "
                + "integer.");

    /// <summary>
    /// Reads a required 16-bit unsigned integer setting.
    /// </summary>
    /// <exception cref="InvalidDataException">
    /// The setting is absent or is not a 16-bit unsigned integer.
    /// </exception>
    public ushort RequireUInt16(string name) =>
        ushort.TryParse(
            RequireString(name),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out ushort value)
            ? value
            : throw new InvalidDataException(
                $"Endpoint '{ExpectedEndpointId}' setting '{name}' is not a "
                + "16-bit unsigned integer.");

    public override string ToString() =>
        $"Endpoint '{ExpectedEndpointId}' from provider '{ProviderId}'";
}
