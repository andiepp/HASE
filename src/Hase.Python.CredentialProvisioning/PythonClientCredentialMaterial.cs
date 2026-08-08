using System.Security.Cryptography;

namespace Hase.Python.CredentialProvisioning;

public sealed class PythonClientCredentialMaterial : IDisposable
{
    private byte[] certificatePem;
    private byte[] privateKeyPem;
    private readonly string credentialId;
    private int disposed;

    internal PythonClientCredentialMaterial(
        byte[] certificatePem,
        byte[] privateKeyPem,
        string credentialId)
    {
        this.certificatePem = certificatePem;
        this.privateKeyPem = privateKeyPem;
        this.credentialId = credentialId;
    }

    public ReadOnlyMemory<byte> CertificatePem
    {
        get
        {
            ThrowIfDisposed();
            return certificatePem;
        }
    }

    public ReadOnlyMemory<byte> PrivateKeyPem
    {
        get
        {
            ThrowIfDisposed();
            return privateKeyPem;
        }
    }

    public string CredentialId
    {
        get
        {
            ThrowIfDisposed();
            return credentialId;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        CryptographicOperations.ZeroMemory(certificatePem);
        CryptographicOperations.ZeroMemory(privateKeyPem);
        certificatePem = [];
        privateKeyPem = [];
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref disposed) != 0,
            this);
    }
}

