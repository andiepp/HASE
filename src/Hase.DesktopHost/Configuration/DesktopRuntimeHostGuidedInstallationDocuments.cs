using System.Text.Json;

namespace Hase.DesktopHost.Configuration;

public static class DesktopRuntimeHostGuidedInstallationDocuments
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public static string CreateApplicationProfile(
        DesktopRuntimeHostGuidedInstallationPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return JsonSerializer.Serialize(
            new
            {
                formatVersion = 1,
                identityFilePath = plan.IdentityFilePath,
                privateNetworkConfigurationFilePath = plan.PrivateNetworkConfigurationFilePath,
                endpointCompositionFilePath = plan.EndpointCompositionFilePath,
                maximumDiagnosticLevel = "Bytes",
                includeByteBufferSimulation = false,
                remoteDiagnosticsEnabled = false,
                remoteDiagnosticsMaximumLevel = "Operational"
            },
            SerializerOptions);
    }

    public static string CreateEndpointComposition(
        DesktopRuntimeHostGuidedInstallationPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        IEnumerable<object> endpoints = plan.EndpointComposition.NativeNetworkEndpoints
            .Select(native => (object)new
            {
                kind = "NativeNetwork",
                expectedEndpointId = native.ExpectedEndpointId,
                host = native.Host,
                port = native.Port
            })
            .Concat(plan.EndpointComposition.CompactSerialEndpoints.Select(compact => (object)new
            {
                kind = "CompactSerial",
                expectedEndpointId = compact.ExpectedEndpointId,
                vendorId = compact.VendorId,
                productId = compact.ProductId,
                baudRate = compact.BaudRate,
                verificationTimeoutMilliseconds =
                    checked((int)compact.VerificationTimeout.TotalMilliseconds)
            }));

        return JsonSerializer.Serialize(
            new
            {
                formatVersion = 1,
                endpoints
            },
            SerializerOptions);
    }
}
