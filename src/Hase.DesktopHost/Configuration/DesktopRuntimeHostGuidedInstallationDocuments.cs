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
                includeByteBufferSimulation = false
            },
            SerializerOptions);
    }

    public static string CreateEndpointComposition(
        DesktopRuntimeHostGuidedInstallationPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        DesktopRuntimeHostNativeNetworkEndpointProfile native =
            AssertSingle(plan.EndpointComposition.NativeNetworkEndpoints, "native network");
        DesktopRuntimeHostCompactSerialEndpointProfile compact =
            AssertSingle(plan.EndpointComposition.CompactSerialEndpoints, "compact serial");

        return JsonSerializer.Serialize(
            new
            {
                formatVersion = 1,
                endpoints = new object[]
                {
                    new
                    {
                        kind = "NativeNetwork",
                        expectedEndpointId = native.ExpectedEndpointId,
                        host = native.Host,
                        port = native.Port
                    },
                    new
                    {
                        kind = "CompactSerial",
                        expectedEndpointId = compact.ExpectedEndpointId,
                        vendorId = compact.VendorId,
                        productId = compact.ProductId,
                        baudRate = compact.BaudRate,
                        verificationTimeoutMilliseconds =
                            checked((int)compact.VerificationTimeout.TotalMilliseconds)
                    }
                }
            },
            SerializerOptions);
    }

    private static T AssertSingle<T>(IReadOnlyList<T> values, string role) =>
        values.Count == 1
            ? values[0]
            : throw new InvalidOperationException(
                $"The guided installation requires exactly one {role} endpoint.");
}
