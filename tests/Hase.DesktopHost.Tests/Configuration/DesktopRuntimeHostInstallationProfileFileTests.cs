using System.IO;
using System.Text;
using System.Text.Json;
using Hase.DesktopHost.Configuration;
using Hase.Runtime.Diagnostics;

namespace Hase.DesktopHost.Tests.Configuration;

public sealed class DesktopRuntimeHostInstallationProfileFileTests
{
    [Fact]
    public async Task LoadAsync_CompleteDocument_ShouldLoadProfile()
    {
        string identityPath =
            MissingReferencedPath(
                "runtime-host.id");
        string deploymentPath =
            MissingReferencedPath(
                "desktop-private-network.json");
        string authorizationPolicyPath =
            MissingReferencedPath(
                "runtime-host-authorization.json");
        string document =
            $$"""
            {
              "formatVersion": 1,
              "identityFilePath": {{JsonSerializer.Serialize(identityPath)}},
              "privateNetworkConfigurationFilePath": {{JsonSerializer.Serialize(deploymentPath)}},
              "maximumDiagnosticLevel": "Bytes",
              "includeByteBufferSimulation": true,
              "remoteDiagnosticsEnabled": true,
              "remoteDiagnosticsMaximumLevel": "Protocol",
              "authorizationPolicyFilePath": {{JsonSerializer.Serialize(authorizationPolicyPath)}}
            }
            """;

        DesktopRuntimeHostInstallationProfile profile =
            await LoadDocumentAsync(
                document);

        Assert.Equal(
            identityPath,
            profile.IdentityFilePath);
        Assert.Equal(
            deploymentPath,
            profile.PrivateNetworkConfigurationFilePath);
        Assert.Equal(
            RuntimeDiagnosticLevel.Bytes,
            profile.MaximumDiagnosticLevel);
        Assert.True(
            profile.IncludeByteBufferSimulation);
        Assert.True(profile.RemoteDiagnosticsEnabled);
        Assert.Equal(
            RuntimeDiagnosticLevel.Protocol,
            profile.RemoteDiagnosticsMaximumLevel);
        Assert.Equal(
            authorizationPolicyPath,
            profile.AuthorizationPolicyFilePath);
    }

    [Fact]
    public async Task LoadAsync_OmittedOptionalValues_ShouldUseSafeDefaults()
    {
        DesktopRuntimeHostInstallationProfile profile =
            await LoadDocumentAsync(
                ValidDocument(
                    additionalProperties: string.Empty));

        Assert.Equal(
            RuntimeDiagnosticLevel.Operational,
            profile.MaximumDiagnosticLevel);
        Assert.False(
            profile.IncludeByteBufferSimulation);
        Assert.False(profile.RemoteDiagnosticsEnabled);
        Assert.Equal(
            RuntimeDiagnosticLevel.Operational,
            profile.RemoteDiagnosticsMaximumLevel);
        Assert.Null(profile.AuthorizationPolicyFilePath);
    }

    [Fact]
    public async Task LoadAsync_EnabledRemoteDiagnosticsWithoutPolicy_ShouldReject()
    {
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            LoadDocumentAsync(
                ValidDocument(
                    """
                    ,
                      "maximumDiagnosticLevel": "Bytes",
                      "remoteDiagnosticsEnabled": true
                    """)));
    }

    [Fact]
    public async Task LoadAsync_MediaConfigurationWithPolicy_ShouldLoadExactPath()
    {
        string policyPath = MissingReferencedPath("runtime-host-authorization.json");
        string mediaPath = MissingReferencedPath("desktop-runtime-media.json");
        DesktopRuntimeHostInstallationProfile profile = await LoadDocumentAsync(
            ValidDocument(
                $$"""
                ,
                  "authorizationPolicyFilePath": {{JsonSerializer.Serialize(policyPath)}},
                  "mediaConfigurationFilePath": {{JsonSerializer.Serialize(mediaPath)}}
                """));

        Assert.Equal(policyPath, profile.AuthorizationPolicyFilePath);
        Assert.Equal(mediaPath, profile.MediaConfigurationFilePath);
    }

    [Fact]
    public async Task LoadAsync_MediaConfigurationWithoutPolicy_ShouldReject()
    {
        string mediaPath = MissingReferencedPath("desktop-runtime-media.json");
        await Assert.ThrowsAsync<InvalidDataException>(() => LoadDocumentAsync(
            ValidDocument(
                $$"""
                ,
                  "mediaConfigurationFilePath": {{JsonSerializer.Serialize(mediaPath)}}
                """)));
    }

    [Theory]
    [InlineData("Operational", RuntimeDiagnosticLevel.Operational)]
    [InlineData("Protocol", RuntimeDiagnosticLevel.Protocol)]
    [InlineData("Bytes", RuntimeDiagnosticLevel.Bytes)]
    public async Task LoadAsync_ExactRemoteDiagnosticName_ShouldSucceed(
        string name,
        RuntimeDiagnosticLevel expected)
    {
        DesktopRuntimeHostInstallationProfile profile =
            await LoadDocumentAsync(
                ValidDocument(
                    $"""
                    ,
                      "maximumDiagnosticLevel": "Bytes",
                      "remoteDiagnosticsEnabled": false,
                      "remoteDiagnosticsMaximumLevel": "{name}"
                    """));

        Assert.False(profile.RemoteDiagnosticsEnabled);
        Assert.Equal(expected, profile.RemoteDiagnosticsMaximumLevel);
    }

    [Theory]
    [InlineData("operational")]
    [InlineData("bytes")]
    [InlineData("Unknown")]
    public async Task LoadAsync_InvalidRemoteDiagnosticName_ShouldReject(
        string name)
    {
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            LoadDocumentAsync(
                ValidDocument(
                    $"""
                    ,
                      "remoteDiagnosticsMaximumLevel": "{name}"
                    """)));
    }

    [Fact]
    public async Task LoadAsync_EnabledRemoteCeilingAboveLocal_ShouldReject()
    {
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            LoadDocumentAsync(
                ValidDocument(
                    """
                    ,
                      "maximumDiagnosticLevel": "Operational",
                      "remoteDiagnosticsEnabled": true,
                      "remoteDiagnosticsMaximumLevel": "Protocol"
                    """)));
    }

    [Fact]
    public async Task LoadAsync_Utf8ByteOrderMark_ShouldSucceed()
    {
        string filePath =
            TemporaryFilePath();
        byte[] json =
            Encoding.UTF8.GetBytes(
                ValidDocument(
                    additionalProperties: string.Empty));
        byte[] document =
            Encoding.UTF8.GetPreamble()
                .Concat(
                    json)
                .ToArray();

        try
        {
            await File.WriteAllBytesAsync(
                filePath,
                document);

            DesktopRuntimeHostInstallationProfile profile =
                await DesktopRuntimeHostInstallationProfileFile.LoadAsync(
                    filePath);

            Assert.Equal(
                RuntimeDiagnosticLevel.Operational,
                profile.MaximumDiagnosticLevel);
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
    [InlineData("desktop-runtime-host.json")]
    public async Task LoadAsync_InvalidTopLevelPath_ShouldThrow(
        string? filePath)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => DesktopRuntimeHostInstallationProfileFile.LoadAsync(
                filePath!));
    }

    [Fact]
    public async Task LoadAsync_CancelledBeforeRead_ShouldThrow()
    {
        using var cancellation =
            new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => DesktopRuntimeHostInstallationProfileFile.LoadAsync(
                TemporaryFilePath(),
                cancellation.Token));
    }

    [Fact]
    public async Task LoadAsync_MissingIdentityPath_ShouldReject()
    {
        await Assert.ThrowsAsync<InvalidDataException>(
            () => LoadDocumentAsync(
                $$"""
                {
                  "formatVersion": 1,
                  "privateNetworkConfigurationFilePath": {{JsonSerializer.Serialize(MissingReferencedPath("desktop.json"))}}
                }
                """));
    }

    [Fact]
    public async Task LoadAsync_RelativeReferencedPath_ShouldReject()
    {
        await Assert.ThrowsAsync<InvalidDataException>(
            () => LoadDocumentAsync(
                $$"""
                {
                  "formatVersion": 1,
                  "identityFilePath": "runtime-host.id",
                  "privateNetworkConfigurationFilePath": {{JsonSerializer.Serialize(MissingReferencedPath("desktop.json"))}}
                }
                """));
    }

    [Fact]
    public async Task LoadAsync_IdenticalReferencedPaths_ShouldReject()
    {
        string path =
            MissingReferencedPath(
                "configuration.json");

        await Assert.ThrowsAsync<InvalidDataException>(
            () => LoadDocumentAsync(
                $$"""
                {
                  "formatVersion": 1,
                  "identityFilePath": {{JsonSerializer.Serialize(path)}},
                  "privateNetworkConfigurationFilePath": {{JsonSerializer.Serialize(path)}}
                }
                """));
    }

    [Theory]
    [InlineData("Operational", RuntimeDiagnosticLevel.Operational)]
    [InlineData("Protocol", RuntimeDiagnosticLevel.Protocol)]
    [InlineData("Bytes", RuntimeDiagnosticLevel.Bytes)]
    public async Task LoadAsync_ExactDiagnosticName_ShouldSucceed(
        string name,
        RuntimeDiagnosticLevel expected)
    {
        DesktopRuntimeHostInstallationProfile profile =
            await LoadDocumentAsync(
                ValidDocument(
                    $"""
                    ,
                      "maximumDiagnosticLevel": "{name}"
                    """));

        Assert.Equal(
            expected,
            profile.MaximumDiagnosticLevel);
    }

    [Theory]
    [InlineData("bytes")]
    [InlineData("2")]
    [InlineData("Unknown")]
    public async Task LoadAsync_InvalidDiagnosticName_ShouldReject(
        string name)
    {
        await Assert.ThrowsAsync<InvalidDataException>(
            () => LoadDocumentAsync(
                ValidDocument(
                    $"""
                    ,
                      "maximumDiagnosticLevel": "{name}"
                    """)));
    }

    [Fact]
    public async Task LoadAsync_NumericDiagnosticLevel_ShouldReject()
    {
        await Assert.ThrowsAsync<InvalidDataException>(
            () => LoadDocumentAsync(
                ValidDocument(
                    """
                    ,
                      "maximumDiagnosticLevel": 2
                    """)));
    }

    [Fact]
    public async Task LoadAsync_UnknownProperty_ShouldReject()
    {
        await Assert.ThrowsAsync<InvalidDataException>(
            () => LoadDocumentAsync(
                ValidDocument(
                    """
                    ,
                      "endpoints": []
                    """)));
    }

    [Fact]
    public async Task LoadAsync_UnsupportedVersion_ShouldReject()
    {
        await Assert.ThrowsAsync<InvalidDataException>(
            () => LoadDocumentAsync(
                ValidDocument(
                    additionalProperties: string.Empty)
                    .Replace(
                        "\"formatVersion\": 1",
                        "\"formatVersion\": 2",
                        StringComparison.Ordinal)));
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
                    64 * 1024
                    + 1]);

            await Assert.ThrowsAsync<InvalidDataException>(
                () => DesktopRuntimeHostInstallationProfileFile.LoadAsync(
                    filePath));
        }
        finally
        {
            File.Delete(
                filePath);
        }
    }

    private static string ValidDocument(
        string additionalProperties) =>
        $$"""
        {
          "formatVersion": 1,
          "identityFilePath": {{JsonSerializer.Serialize(MissingReferencedPath("runtime-host.id"))}},
          "privateNetworkConfigurationFilePath": {{JsonSerializer.Serialize(MissingReferencedPath("desktop-private-network.json"))}}
          {{additionalProperties}}
        }
        """;

    private static async Task<DesktopRuntimeHostInstallationProfile>
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

            return await DesktopRuntimeHostInstallationProfileFile.LoadAsync(
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
