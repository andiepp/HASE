using System.Security.Cryptography.X509Certificates;

namespace Hase.Runtime.Remote.Grpc.Hosting;

/// <summary>
/// Owns one configured private-network gRPC client and the externally
/// provisioned certificates used by its TLS channel.
/// </summary>
public sealed class RuntimeHostPrivateNetworkClientDeployment
    : IDisposable
{
    private readonly X509Certificate2 clientCertificate;
    private readonly X509Certificate2 trustedServerCertificate;
    private bool disposed;

    private RuntimeHostPrivateNetworkClientDeployment(
        RuntimeHostPrivateNetworkGrpcClient client,
        X509Certificate2 clientCertificate,
        X509Certificate2 trustedServerCertificate)
    {
        Client =
            client;
        this.clientCertificate =
            clientCertificate;
        this.trustedServerCertificate =
            trustedServerCertificate;
    }

    /// <summary>
    /// Gets the configured private-network gRPC client.
    /// </summary>
    public RuntimeHostPrivateNetworkGrpcClient Client
    {
        get;
    }

    /// <summary>
    /// Creates one client deployment from externally provisioned
    /// certificate-store references.
    /// </summary>
    public static RuntimeHostPrivateNetworkClientDeployment Create(
        RuntimeHostPrivateNetworkClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(
            options);

        X509Certificate2 clientCertificate =
            RuntimeHostCertificateStoreLoader.Load(
                options.ClientCertificate,
                requirePrivateKey: true);

        try
        {
            X509Certificate2 trustedServerCertificate =
                RuntimeHostCertificateStoreLoader.Load(
                    options.TrustedServerCertificate,
                    requirePrivateKey: false);

            try
            {
                RuntimeHostPrivateNetworkGrpcClient client =
                    RuntimeHostPrivateNetworkGrpcClient.Create(
                        options.Address,
                        clientCertificate,
                        trustedServerCertificate);

                return new RuntimeHostPrivateNetworkClientDeployment(
                    client,
                    clientCertificate,
                    trustedServerCertificate);
            }
            catch
            {
                trustedServerCertificate.Dispose();
                throw;
            }
        }
        catch
        {
            clientCertificate.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed =
            true;

        try
        {
            Client.Dispose();
        }
        finally
        {
            try
            {
                trustedServerCertificate.Dispose();
            }
            finally
            {
                clientCertificate.Dispose();
            }
        }
    }
}
