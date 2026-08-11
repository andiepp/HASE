using System.Security.Cryptography;

namespace Hase.Python.CredentialProvisioning;

/// <summary>
/// Holds an entirely in-memory planned-rotation candidate set. The
/// authorization policy and profile candidates are exact copies used to prove
/// that rotation does not change either boundary.
/// </summary>
public sealed class PythonCredentialRotationCandidates : IDisposable
{
    private byte[] certificate;
    private byte[] privateKey;
    private byte[] profile;
    private byte[] overlapEnrollment;
    private byte[] finalEnrollment;
    private byte[] authorizationPolicy;
    private int disposed;

    internal PythonCredentialRotationCandidates(
        byte[] certificate,
        byte[] privateKey,
        byte[] profile,
        byte[] overlapEnrollment,
        byte[] finalEnrollment,
        byte[] authorizationPolicy,
        string currentCredentialId,
        string replacementCredentialId,
        string principalId,
        string trustPolicyId,
        IReadOnlyList<string> authorizationGrants)
    {
        this.certificate = certificate;
        this.privateKey = privateKey;
        this.profile = profile;
        this.overlapEnrollment = overlapEnrollment;
        this.finalEnrollment = finalEnrollment;
        this.authorizationPolicy = authorizationPolicy;
        CurrentCredentialId = currentCredentialId;
        ReplacementCredentialId = replacementCredentialId;
        PrincipalId = principalId;
        TrustPolicyId = trustPolicyId;
        AuthorizationGrants = authorizationGrants;
    }

    public string CurrentCredentialId { get; }
    public string ReplacementCredentialId { get; }
    public string PrincipalId { get; }
    public string TrustPolicyId { get; }
    public IReadOnlyList<string> AuthorizationGrants { get; }
    public ReadOnlyMemory<byte> ReplacementCertificatePem => Get(certificate);
    public ReadOnlyMemory<byte> ReplacementPrivateKeyPem => Get(privateKey);
    public ReadOnlyMemory<byte> ProfileDocument => Get(profile);
    public ReadOnlyMemory<byte> OverlapEnrollmentDocument => Get(overlapEnrollment);
    public ReadOnlyMemory<byte> FinalEnrollmentDocument => Get(finalEnrollment);
    public ReadOnlyMemory<byte> AuthorizationPolicyDocument => Get(authorizationPolicy);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        foreach (byte[] value in new[]
        {
            certificate, privateKey, profile, overlapEnrollment,
            finalEnrollment, authorizationPolicy,
        })
        {
            CryptographicOperations.ZeroMemory(value);
        }
        certificate = privateKey = profile = overlapEnrollment =
            finalEnrollment = authorizationPolicy = [];
    }

    private ReadOnlyMemory<byte> Get(byte[] value)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        return value;
    }
}
