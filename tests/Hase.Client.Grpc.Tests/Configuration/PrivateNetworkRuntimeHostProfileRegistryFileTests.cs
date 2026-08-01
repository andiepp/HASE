using System.IO;
using System.Text;
using System.Text.Json;
using Hase.Client.Grpc.Configuration;

namespace Hase.Client.Grpc.Tests.Configuration;

public sealed class PrivateNetworkRuntimeHostProfileRegistryFileTests
{
    [Fact]
    public async Task LoadAsync_EmptyRegistry_ShouldSucceed()
    {
        PrivateNetworkRuntimeHostProfileRegistry registry =
            await LoadDocumentAsync(
                """
                {
                  "formatVersion": 1,
                  "hosts": []
                }
                """);

        Assert.Empty(
            registry.Profiles);
    }

    [Fact]
    public async Task LoadAsync_MultipleHosts_ShouldPreserveOrderAndValues()
    {
        PrivateNetworkRuntimeHostProfileRegistry registry =
            await LoadDocumentAsync(
                RegistryDocument(
                    HostDocument(
                        "first",
                        "First Host",
                        "host-01",
                        enabled: true),
                    HostDocument(
                        "second",
                        "Second Host",
                        "host-02",
                        enabled: false)));

        Assert.Equal(
            new[]
            {
                "first",
                "second"
            },
            registry.Profiles
                .Select(
                    profile =>
                        profile.Profile.ProfileId.Value));
        Assert.True(
            registry.Profiles[0].Profile.IsEnabled);
        Assert.False(
            registry.Profiles[1].Profile.IsEnabled);
        Assert.Equal(
            "host-01",
            registry.Profiles[0]
                .Profile
                .ExpectedRuntimeHostId
                .Value);
    }

    [Fact]
    public async Task LoadAsync_Utf8ByteOrderMark_ShouldSucceed()
    {
        string filePath =
            TemporaryFilePath();
        byte[] document =
            Encoding.UTF8.GetPreamble()
                .Concat(
                    Encoding.UTF8.GetBytes(
                        RegistryDocument(
                            HostDocument(
                                "laboratory",
                                "Laboratory",
                                "host-01",
                                enabled: true))))
                .ToArray();

        try
        {
            await File.WriteAllBytesAsync(
                filePath,
                document);

            PrivateNetworkRuntimeHostProfileRegistry registry =
                await PrivateNetworkRuntimeHostProfileRegistryFile.LoadAsync(
                    filePath);

            Assert.Single(
                registry.Profiles);
        }
        finally
        {
            File.Delete(
                filePath);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("hase-client-hosts.json")]
    public async Task LoadAsync_InvalidTopLevelPath_ShouldThrow(
        string? filePath)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => PrivateNetworkRuntimeHostProfileRegistryFile.LoadAsync(
                filePath!));
    }

    [Fact]
    public async Task LoadAsync_CancelledBeforeRead_ShouldThrow()
    {
        using var cancellation =
            new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => PrivateNetworkRuntimeHostProfileRegistryFile.LoadAsync(
                TemporaryFilePath(),
                cancellation.Token));
    }

    [Fact]
    public async Task LoadAsync_MissingHosts_ShouldReject()
    {
        await Assert.ThrowsAsync<InvalidDataException>(
            () => LoadDocumentAsync(
                """
                {
                  "formatVersion": 1
                }
                """));
    }

    [Fact]
    public async Task LoadAsync_MissingEnabled_ShouldReject()
    {
        string path =
            MissingReferencedPath(
                "client.json");

        await Assert.ThrowsAsync<InvalidDataException>(
            () => LoadDocumentAsync(
                $$"""
                {
                  "formatVersion": 1,
                  "hosts": [
                    {
                      "profileId": "laboratory",
                      "displayName": "Laboratory",
                      "expectedRuntimeHostId": "host-01",
                      "privateNetworkConfigurationFilePath": {{JsonSerializer.Serialize(path)}}
                    }
                  ]
                }
                """));
    }

    [Theory]
    [InlineData("profileId")]
    [InlineData("displayName")]
    [InlineData("expectedRuntimeHostId")]
    [InlineData("privateNetworkConfigurationFilePath")]
    public async Task LoadAsync_MissingRequiredHostField_ShouldReject(
        string fieldName)
    {
        string document =
            RegistryDocument(
                HostDocument(
                    "laboratory",
                    "Laboratory",
                    "host-01",
                    enabled: true));
        using JsonDocument parsed =
            JsonDocument.Parse(
                document);
        Dictionary<string, object?> host =
            parsed.RootElement
                .GetProperty(
                    "hosts")[0]
                .EnumerateObject()
                .Where(
                    property =>
                        property.Name != fieldName)
                .ToDictionary(
                    property =>
                        property.Name,
                    property =>
                        (object?)property.Value.Clone());
        string incomplete =
            JsonSerializer.Serialize(
                new
                {
                    formatVersion = 1,
                    hosts =
                        new[]
                        {
                            host
                        }
                });

        await Assert.ThrowsAsync<InvalidDataException>(
            () => LoadDocumentAsync(
                incomplete));
    }

    [Fact]
    public async Task LoadAsync_InvalidProfileId_ShouldReject()
    {
        await Assert.ThrowsAsync<InvalidDataException>(
            () => LoadDocumentAsync(
                RegistryDocument(
                    HostDocument(
                        "Laboratory",
                        "Laboratory",
                        "host-01",
                        enabled: true))));
    }

    [Fact]
    public async Task LoadAsync_RelativeReferencedPath_ShouldReject()
    {
        await Assert.ThrowsAsync<InvalidDataException>(
            () => LoadDocumentAsync(
                RegistryDocument(
                    HostDocument(
                        "laboratory",
                        "Laboratory",
                        "host-01",
                        enabled: true,
                        configurationPath: "client.json"))));
    }

    [Fact]
    public async Task LoadAsync_DuplicateProfileId_ShouldReject()
    {
        await Assert.ThrowsAsync<InvalidDataException>(
            () => LoadDocumentAsync(
                RegistryDocument(
                    HostDocument(
                        "laboratory",
                        "First",
                        "host-01",
                        enabled: true),
                    HostDocument(
                        "laboratory",
                        "Second",
                        "host-02",
                        enabled: true))));
    }

    [Fact]
    public async Task LoadAsync_DuplicateEnabledRuntimeHostId_ShouldReject()
    {
        await Assert.ThrowsAsync<InvalidDataException>(
            () => LoadDocumentAsync(
                RegistryDocument(
                    HostDocument(
                        "first",
                        "First",
                        "host-01",
                        enabled: true),
                    HostDocument(
                        "second",
                        "Second",
                        "host-01",
                        enabled: true))));
    }

    [Fact]
    public async Task LoadAsync_DisabledDuplicateRuntimeHostId_ShouldSucceed()
    {
        PrivateNetworkRuntimeHostProfileRegistry registry =
            await LoadDocumentAsync(
                RegistryDocument(
                    HostDocument(
                        "first",
                        "First",
                        "host-01",
                        enabled: true),
                    HostDocument(
                        "second",
                        "Second",
                        "host-01",
                        enabled: false)));

        Assert.Equal(
            2,
            registry.Profiles.Count);
    }

    [Fact]
    public async Task LoadAsync_OverMaximumHosts_ShouldReject()
    {
        string[] hosts =
            Enumerable.Range(
                    0,
                    65)
                .Select(
                    index =>
                        HostDocument(
                            $"profile-{index}",
                            $"Profile {index}",
                            $"host-{index}",
                            enabled: true))
                .ToArray();

        await Assert.ThrowsAsync<InvalidDataException>(
            () => LoadDocumentAsync(
                RegistryDocument(
                    hosts)));
    }

    [Fact]
    public async Task LoadAsync_UnknownRootProperty_ShouldReject()
    {
        await Assert.ThrowsAsync<InvalidDataException>(
            () => LoadDocumentAsync(
                """
                {
                  "formatVersion": 1,
                  "hosts": [],
                  "address": "withheld"
                }
                """));
    }

    [Fact]
    public async Task LoadAsync_UnknownHostProperty_ShouldReject()
    {
        string host =
            HostDocument(
                "laboratory",
                "Laboratory",
                "host-01",
                enabled: true)
                .Replace(
                    "\"enabled\": true",
                    "\"enabled\": true, \"certificate\": \"withheld\"",
                    StringComparison.Ordinal);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => LoadDocumentAsync(
                RegistryDocument(
                    host)));
    }

    [Fact]
    public async Task LoadAsync_UnsupportedVersion_ShouldReject()
    {
        await Assert.ThrowsAsync<InvalidDataException>(
            () => LoadDocumentAsync(
                """
                {
                  "formatVersion": 2,
                  "hosts": []
                }
                """));
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("{")]
    [InlineData("[]")]
    public async Task LoadAsync_InvalidDocument_ShouldReject(
        string document)
    {
        await Assert.ThrowsAsync<InvalidDataException>(
            () => LoadDocumentAsync(
                document));
    }

    [Fact]
    public async Task LoadAsync_OversizedDocument_ShouldReject()
    {
        string filePath =
            TemporaryFilePath();

        try
        {
            await File.WriteAllBytesAsync(
                filePath,
                new byte[
                    128 * 1024
                    + 1]);

            await Assert.ThrowsAsync<InvalidDataException>(
                () => PrivateNetworkRuntimeHostProfileRegistryFile.LoadAsync(
                    filePath));
        }
        finally
        {
            File.Delete(
                filePath);
        }
    }

    private static string RegistryDocument(
        params string[] hosts) =>
        $$"""
        {
          "formatVersion": 1,
          "hosts": [
            {{string.Join("," + Environment.NewLine, hosts)}}
          ]
        }
        """;

    private static string HostDocument(
        string profileId,
        string displayName,
        string runtimeHostId,
        bool enabled,
        string? configurationPath = null) =>
        $$"""
        {
          "profileId": {{JsonSerializer.Serialize(profileId)}},
          "displayName": {{JsonSerializer.Serialize(displayName)}},
          "expectedRuntimeHostId": {{JsonSerializer.Serialize(runtimeHostId)}},
          "privateNetworkConfigurationFilePath": {{JsonSerializer.Serialize(configurationPath ?? MissingReferencedPath(profileId + ".json"))}},
          "enabled": {{enabled.ToString().ToLowerInvariant()}}
        }
        """;

    private static async Task<PrivateNetworkRuntimeHostProfileRegistry>
        LoadDocumentAsync(
            string document)
    {
        string filePath =
            TemporaryFilePath();

        try
        {
            await File.WriteAllTextAsync(
                filePath,
                document,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false));

            return await PrivateNetworkRuntimeHostProfileRegistryFile.LoadAsync(
                filePath);
        }
        finally
        {
            File.Delete(
                filePath);
        }
    }

    private static string MissingReferencedPath(
        string fileName) =>
        Path.Combine(
            Path.GetTempPath(),
            "hase-43a3-references",
            fileName);

    private static string TemporaryFilePath() =>
        Path.Combine(
            Path.GetTempPath(),
            $"hase-43a3-{Guid.NewGuid():N}.json");
}
