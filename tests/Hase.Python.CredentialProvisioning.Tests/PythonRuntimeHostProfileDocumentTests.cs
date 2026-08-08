using System.Text;

namespace Hase.Python.CredentialProvisioning.Tests;

public sealed class PythonRuntimeHostProfileDocumentTests
{
    private static readonly string Root = Path.GetFullPath(
        Path.Combine(
            Path.GetTempPath(),
            $"hase-python-profile-document-{Guid.NewGuid():N}"));

    [Fact]
    public void Load_ValidUnpublishedPaths_ShouldNotAccessFileSystem()
    {
        PythonRuntimeHostProfileDocument profile =
            PythonRuntimeHostProfileDocument.Load(ValidDocument());

        Assert.Equal("https://127.0.0.1:7443", profile.Address);
        Assert.Equal(Path.Combine(Root, "client.cer"),
            profile.ClientCertificateChainPath);
        Assert.False(Directory.Exists(Root));
    }

    [Fact]
    public void Serialize_RoundTripsThroughAuthoritativeLoader()
    {
        PythonRuntimeHostProfileDocument original =
            PythonRuntimeHostProfileDocument.Load(ValidDocument());

        byte[] serialized = original.Serialize();
        PythonRuntimeHostProfileDocument roundTrip =
            PythonRuntimeHostProfileDocument.Load(serialized);

        Assert.Equal(original, roundTrip);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"formatVersion\":2}")]
    [InlineData("{\"formatVersion\":1,\"formatVersion\":1}")]
    [InlineData("{\"formatVersion\":1,\"unexpected\":true}")]
    [InlineData("not-json")]
    public void Load_InvalidShape_ShouldThrow(string document)
    {
        Assert.Throws<InvalidDataException>(() =>
            PythonRuntimeHostProfileDocument.Load(Encoding.UTF8.GetBytes(document)));
    }

    [Fact]
    public void Load_OversizedDocument_ShouldThrow()
    {
        Assert.Throws<InvalidDataException>(() =>
            PythonRuntimeHostProfileDocument.Load(new byte[(64 * 1024) + 1]));
    }

    [Fact]
    public void Load_DuplicateCredentialPaths_ShouldThrow()
    {
        string path = Path.Combine(Root, "same.pem");
        byte[] document = Encoding.UTF8.GetBytes(
            "{\"formatVersion\":1,\"address\":\"https://127.0.0.1:7443\","
            + "\"clientCertificate\":{\"certificateChainPath\":\""
            + Escape(path) + "\",\"privateKeyPath\":\"" + Escape(path) + "\"},"
            + "\"trustedServerCertificate\":{\"certificatePath\":\""
            + Escape(Path.Combine(Root, "trusted.cer")) + "\"}}" );

        Assert.Throws<InvalidDataException>(() =>
            PythonRuntimeHostProfileDocument.Load(document));
    }

    private static byte[] ValidDocument()
    {
        return Encoding.UTF8.GetBytes(
            "{\"formatVersion\":1,\"address\":\"https://127.0.0.1:7443\","
            + "\"clientCertificate\":{\"certificateChainPath\":\""
            + Escape(Path.Combine(Root, "client.cer"))
            + "\",\"privateKeyPath\":\""
            + Escape(Path.Combine(Root, "client.key")) + "\"},"
            + "\"trustedServerCertificate\":{\"certificatePath\":\""
            + Escape(Path.Combine(Root, "trusted.cer")) + "\"}}");
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\");
}
