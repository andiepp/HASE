namespace Hase.Runtime.Remote.Grpc.Hosting.Tests;

public sealed class RuntimeHostDevelopmentLoopbackClientOptionsFileTests
{
    [Fact]
    public async Task ValidDocument_ShouldLoad()
    {
        string filePath = TemporaryFilePath();

        try
        {
            await File.WriteAllTextAsync(
                filePath,
                """
                {
                  "formatVersion": 1,
                  "profileKind": "development-loopback",
                  "address": "http://127.0.0.1:52110"
                }
                """);

            RuntimeHostDevelopmentLoopbackClientOptions options =
                await RuntimeHostDevelopmentLoopbackClientOptionsFile
                    .LoadAsync(filePath);

            Assert.Equal(
                new Uri("http://127.0.0.1:52110"),
                options.Address);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Theory]
    [InlineData(
        """
        {
          "formatVersion": 1,
          "address": "http://127.0.0.1:52110"
        }
        """)]
    [InlineData(
        """
        {
          "formatVersion": 1,
          "profileKind": "private-network",
          "address": "http://127.0.0.1:52110"
        }
        """)]
    [InlineData(
        """
        {
          "formatVersion": 2,
          "profileKind": "development-loopback",
          "address": "http://127.0.0.1:52110"
        }
        """)]
    [InlineData(
        """
        {
          "formatVersion": 1,
          "profileKind": "development-loopback",
          "address": "http://127.0.0.1:52110",
          "clientCertificate": "unexpected"
        }
        """)]
    [InlineData(
        """
        {
          "formatVersion": 1,
          "profileKind": "development-loopback",
          "address": "http://192.168.0.10:52110"
        }
        """)]
    [InlineData(
        """
        {
          "formatVersion": 1,
          "profileKind": "development-loopback",
          "address": "https://127.0.0.1:52110"
        }
        """)]
    [InlineData(
        """
        {
          "formatVersion": 1,
          "profileKind": "development-loopback",
          "address": "not-an-address"
        }
        """)]
    public async Task InvalidDocument_ShouldReject(string document)
    {
        string filePath = TemporaryFilePath();

        try
        {
            await File.WriteAllTextAsync(filePath, document);

            await Assert.ThrowsAsync<InvalidDataException>(
                () => RuntimeHostDevelopmentLoopbackClientOptionsFile
                    .LoadAsync(filePath));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task Probe_DevelopmentDocument_ShouldReturnTrue()
    {
        string filePath = TemporaryFilePath();

        try
        {
            await File.WriteAllTextAsync(
                filePath,
                """
                {
                  "formatVersion": 1,
                  "profileKind": "development-loopback",
                  "address": "http://127.0.0.1:52110"
                }
                """);

            Assert.True(
                await RuntimeHostClientConfigurationDocument
                    .IsDevelopmentLoopbackAsync(filePath));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Theory]
    [InlineData(
        """
        {
          "formatVersion": 1,
          "address": "https://192.0.2.10:5000",
          "clientCertificate": {},
          "trustedServerCertificate": {}
        }
        """)]
    [InlineData("not json at all")]
    [InlineData("[1, 2, 3]")]
    public async Task Probe_NonDevelopmentDocument_ShouldReturnFalse(
        string document)
    {
        string filePath = TemporaryFilePath();

        try
        {
            await File.WriteAllTextAsync(filePath, document);

            Assert.False(
                await RuntimeHostClientConfigurationDocument
                    .IsDevelopmentLoopbackAsync(filePath));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task Probe_RelativePath_ShouldReject()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => RuntimeHostClientConfigurationDocument
                .IsDevelopmentLoopbackAsync("client.json"));
    }

    private static string TemporaryFilePath() =>
        Path.Combine(
            Path.GetTempPath(),
            $"hase-60c2-{Guid.NewGuid():N}.json");
}
