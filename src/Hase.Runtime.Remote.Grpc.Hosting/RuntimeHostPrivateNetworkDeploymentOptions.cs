namespace Hase.Runtime.Remote.Grpc.Hosting;

/// <summary>
/// Defines the complete external configuration references required to deploy
/// one secured private-network runtime-host listener.
/// </summary>
public sealed record RuntimeHostPrivateNetworkDeploymentOptions
{
    /// <summary>
    /// Initializes one private-network deployment configuration.
    /// </summary>
    public RuntimeHostPrivateNetworkDeploymentOptions(
        PrivateNetworkGrpcBinding binding,
        RuntimeHostCertificateStoreReference serverCertificate,
        string clientEnrollmentFilePath)
    {
        ArgumentNullException.ThrowIfNull(
            binding);
        ArgumentNullException.ThrowIfNull(
            serverCertificate);
        ArgumentNullException.ThrowIfNull(
            clientEnrollmentFilePath);

        if (string.IsNullOrWhiteSpace(
                clientEnrollmentFilePath))
        {
            throw new ArgumentException(
                "The client-enrollment file path must not be empty or "
                + "whitespace.",
                nameof(clientEnrollmentFilePath));
        }

        if (!Path.IsPathFullyQualified(
                clientEnrollmentFilePath))
        {
            throw new ArgumentException(
                "The client-enrollment file path must be fully qualified.",
                nameof(clientEnrollmentFilePath));
        }

        Binding = binding;
        ServerCertificate = serverCertificate;
        ClientEnrollmentFilePath =
            Path.GetFullPath(
                clientEnrollmentFilePath);
    }

    /// <summary>
    /// Gets the explicitly configured private-network listener binding.
    /// </summary>
    public PrivateNetworkGrpcBinding Binding
    {
        get;
    }

    /// <summary>
    /// Gets the external operating-system store reference for the runtime-host
    /// server certificate.
    /// </summary>
    public RuntimeHostCertificateStoreReference ServerCertificate
    {
        get;
    }

    /// <summary>
    /// Gets the normalized fully qualified client-enrollment file path.
    /// </summary>
    public string ClientEnrollmentFilePath
    {
        get;
    }
}
