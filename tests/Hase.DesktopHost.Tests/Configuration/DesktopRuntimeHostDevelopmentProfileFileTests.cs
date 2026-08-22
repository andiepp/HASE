using System.IO;
using System.Text.Json;
using Hase.DesktopHost.Configuration;
using Hase.Runtime.Diagnostics;

namespace Hase.DesktopHost.Tests.Configuration;

public sealed class DesktopRuntimeHostDevelopmentProfileFileTests
{
    [Fact]
    public async Task ValidDocument_ShouldLoad()
    {
        string filePath = TemporaryFilePath();

        try
        {
            await File.WriteAllTextAsync(
                filePath,
                $$"""
                {
                  "formatVersion": 1,
                  "profileKind": "development-loopback",
                  "identityFilePath": {{JsonSerializer.Serialize(AbsolutePath("runtime-host.id"))}},
                  "loopbackAddress": "127.0.0.1",
                  "port": 52110,
                  "includeByteBufferSimulation": true,
                  "maximumDiagnosticLevel": "Protocol"
                }
                """);

            DesktopRuntimeHostDevelopmentProfile profile =
                await DesktopRuntimeHostDevelopmentProfileFile.LoadAsync(filePath);

            Assert.Equal(AbsolutePath("runtime-host.id"), profile.IdentityFilePath);
            Assert.Equal("http://127.0.0.1:52110", profile.BindingDisplay);
            Assert.True(profile.IncludeByteBufferSimulation);
            Assert.Equal(
                RuntimeDiagnosticLevel.Protocol,
                profile.MaximumDiagnosticLevel);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task MissingProfileKind_ShouldReject()
    {
        await AssertRejectsAsync(
            $$"""
            {
              "formatVersion": 1,
              "identityFilePath": {{JsonSerializer.Serialize(AbsolutePath("runtime-host.id"))}},
              "loopbackAddress": "127.0.0.1",
              "port": 52110,
              "includeByteBufferSimulation": true
            }
            """);
    }

    [Fact]
    public async Task WrongProfileKind_ShouldReject()
    {
        await AssertRejectsAsync(
            $$"""
            {
              "formatVersion": 1,
              "profileKind": "production",
              "identityFilePath": {{JsonSerializer.Serialize(AbsolutePath("runtime-host.id"))}},
              "loopbackAddress": "127.0.0.1",
              "port": 52110,
              "includeByteBufferSimulation": true
            }
            """);
    }

    [Fact]
    public async Task UnsupportedFormatVersion_ShouldReject()
    {
        await AssertRejectsAsync(
            $$"""
            {
              "formatVersion": 2,
              "profileKind": "development-loopback",
              "identityFilePath": {{JsonSerializer.Serialize(AbsolutePath("runtime-host.id"))}},
              "loopbackAddress": "127.0.0.1",
              "port": 52110,
              "includeByteBufferSimulation": true
            }
            """);
    }

    [Fact]
    public async Task UnknownMember_ShouldReject()
    {
        await AssertRejectsAsync(
            $$"""
            {
              "formatVersion": 1,
              "profileKind": "development-loopback",
              "identityFilePath": {{JsonSerializer.Serialize(AbsolutePath("runtime-host.id"))}},
              "loopbackAddress": "127.0.0.1",
              "port": 52110,
              "includeByteBufferSimulation": true,
              "serverCertificate": "unexpected"
            }
            """);
    }

    [Fact]
    public async Task NonLoopbackAddress_ShouldReject()
    {
        await AssertRejectsAsync(
            $$"""
            {
              "formatVersion": 1,
              "profileKind": "development-loopback",
              "identityFilePath": {{JsonSerializer.Serialize(AbsolutePath("runtime-host.id"))}},
              "loopbackAddress": "192.168.0.10",
              "port": 52110,
              "includeByteBufferSimulation": true
            }
            """);
    }

    [Fact]
    public async Task MissingPort_ShouldReject()
    {
        await AssertRejectsAsync(
            $$"""
            {
              "formatVersion": 1,
              "profileKind": "development-loopback",
              "identityFilePath": {{JsonSerializer.Serialize(AbsolutePath("runtime-host.id"))}},
              "loopbackAddress": "127.0.0.1",
              "includeByteBufferSimulation": true
            }
            """);
    }

    private static async Task AssertRejectsAsync(string document)
    {
        string filePath = TemporaryFilePath();

        try
        {
            await File.WriteAllTextAsync(filePath, document);

            await Assert.ThrowsAsync<InvalidDataException>(
                () => DesktopRuntimeHostDevelopmentProfileFile.LoadAsync(
                    filePath));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    private static string AbsolutePath(string fileName) =>
        Path.Combine(Path.GetTempPath(), "hase-60c1", fileName);

    private static string TemporaryFilePath() =>
        Path.Combine(Path.GetTempPath(), $"hase-60c1-{Guid.NewGuid():N}.json");
}
