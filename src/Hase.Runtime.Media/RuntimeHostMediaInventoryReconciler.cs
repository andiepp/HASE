using System.Security.Cryptography;
using System.Text;

namespace Hase.Runtime.Media;

/// <summary>
/// Reconciles browser-local camera identities into opaque logical sources.
/// Raw device identities remain process-local and are never included in a
/// capability snapshot sent to a Client.
/// </summary>
public sealed class RuntimeHostMediaInventoryReconciler
{
    public const int MaximumSources = 16;
    public const int IdentityKeyByteCount = 32;

    private readonly IReadOnlyDictionary<string, RuntimeHostMediaSourceConfiguration>
        configuredByDeviceId;
    private readonly byte[] identityKey;
    private readonly Dictionary<string, string> generations =
        new(StringComparer.Ordinal);

    public RuntimeHostMediaInventoryReconciler(
        IReadOnlyList<RuntimeHostMediaSourceConfiguration> configuredSources,
        ReadOnlySpan<byte> identityKey)
    {
        ArgumentNullException.ThrowIfNull(configuredSources);
        if (configuredSources.Count > MaximumSources)
        {
            throw new ArgumentException(
                "At most sixteen configured camera aliases are supported.",
                nameof(configuredSources));
        }
        if (identityKey.Length != IdentityKeyByteCount)
        {
            throw new ArgumentException(
                "A 256-bit host-local media identity key is required.",
                nameof(identityKey));
        }

        configuredByDeviceId = configuredSources.ToDictionary(
            source => source.VideoDeviceId,
            StringComparer.Ordinal);
        this.identityKey = identityKey.ToArray();
    }

    public IReadOnlyList<RuntimeHostMediaSourceConfiguration> Reconcile(
        IReadOnlyList<RuntimeHostMediaDeviceObservation> observations)
    {
        ArgumentNullException.ThrowIfNull(observations);
        if (observations.Count > MaximumSources)
        {
            throw new ArgumentException(
                "At most sixteen observed cameras are supported.",
                nameof(observations));
        }

        string[] deviceIds = observations
            .Select(item => item?.VideoDeviceId?.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        if (deviceIds.Length != observations.Count)
        {
            throw new ArgumentException(
                "Observed camera identities must be non-empty and unique.",
                nameof(observations));
        }

        var present = deviceIds.ToHashSet(StringComparer.Ordinal);
        foreach (string removed in generations.Keys
            .Where(item => !present.Contains(item)).ToArray())
        {
            generations.Remove(removed);
        }

        return deviceIds
            .Select(CreateSource)
            .OrderBy(item => item.DisplayName, StringComparer.Ordinal)
            .ThenBy(item => item.Target.MediaSourceId, StringComparer.Ordinal)
            .ToArray();
    }

    private RuntimeHostMediaSourceConfiguration CreateSource(string deviceId)
    {
        configuredByDeviceId.TryGetValue(deviceId, out var configured);
        string mediaSourceId = configured?.Target.MediaSourceId
            ?? CreateOpaqueSourceId(deviceId);
        string displayName = configured?.DisplayName
            ?? $"Camera {mediaSourceId[^8..]}";
        string generation = generations.TryGetValue(deviceId, out var current)
            ? current
            : generations[deviceId] = CreateGeneration();

        return new RuntimeHostMediaSourceConfiguration(
            new RuntimeHostMediaSourceTarget(mediaSourceId, generation),
            deviceId,
            configured?.AudioDeviceId,
            RuntimeHostMediaSourceAvailability.Idle,
            displayName);
    }

    private string CreateOpaqueSourceId(string deviceId)
    {
        byte[] digest = HMACSHA256.HashData(
            identityKey,
            Encoding.UTF8.GetBytes(deviceId));
        return "camera-" + Convert.ToHexString(digest.AsSpan(0, 12));
    }

    private static string CreateGeneration()
    {
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes);
    }
}
