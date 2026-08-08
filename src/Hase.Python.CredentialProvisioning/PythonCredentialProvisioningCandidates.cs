using System.Security.Cryptography;

namespace Hase.Python.CredentialProvisioning;

public sealed class PythonCredentialProvisioningCandidates : IDisposable
{
    private byte[] certificatePem;
    private byte[] privateKeyPem;
    private byte[] profileDocument;
    private byte[] enrollmentDocument;
    private byte[] authorizationPolicyDocument;
    private int disposed;

    internal PythonCredentialProvisioningCandidates(
        byte[] certificatePem,
        byte[] privateKeyPem,
        byte[] profileDocument,
        byte[] enrollmentDocument,
        byte[] authorizationPolicyDocument)
    {
        this.certificatePem = certificatePem;
        this.privateKeyPem = privateKeyPem;
        this.profileDocument = profileDocument;
        this.enrollmentDocument = enrollmentDocument;
        this.authorizationPolicyDocument = authorizationPolicyDocument;
    }

    public ReadOnlyMemory<byte> CertificatePem => Get(certificatePem);
    public ReadOnlyMemory<byte> PrivateKeyPem => Get(privateKeyPem);
    public ReadOnlyMemory<byte> ProfileDocument => Get(profileDocument);
    public ReadOnlyMemory<byte> EnrollmentDocument => Get(enrollmentDocument);
    public ReadOnlyMemory<byte> AuthorizationPolicyDocument =>
        Get(authorizationPolicyDocument);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        Zero(certificatePem);
        Zero(privateKeyPem);
        Zero(profileDocument);
        Zero(enrollmentDocument);
        Zero(authorizationPolicyDocument);
        certificatePem = [];
        privateKeyPem = [];
        profileDocument = [];
        enrollmentDocument = [];
        authorizationPolicyDocument = [];
    }

    private ReadOnlyMemory<byte> Get(byte[] value)
    {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref disposed) != 0,
            this);
        return value;
    }

    private static void Zero(byte[] value) =>
        CryptographicOperations.ZeroMemory(value);
}
