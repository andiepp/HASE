using Hase.ProtocolExplorer.Scenarios;
using Xunit;

namespace Hase.ProtocolExplorer.Tests;

public sealed class ProvisionClientEnrollmentScenarioTests
{
    [Fact]
    public void Name_ShouldBeProvisionClientEnrollment()
    {
        Assert.Equal(
            "provision-client-enrollment",
            new ProvisionClientEnrollmentScenario().Name);
    }

    [Fact]
    public void Scenario_ShouldImplementParameterizedScenario()
    {
        Assert.IsAssignableFrom<IParameterizedScenario>(
            new ProvisionClientEnrollmentScenario());
    }

    [Fact]
    public void ParseArguments_ValidValues_ShouldPreserveValues()
    {
        string publicCertificateFilePath =
            Path.Combine(
                Path.GetTempPath(),
                "public-client.cer");
        string enrollmentFilePath =
            Path.Combine(
                Path.GetTempPath(),
                "client-enrollments.json");

        ProvisionClientEnrollmentArguments arguments =
            ProvisionClientEnrollmentScenario.ParseArguments(
                [
                    publicCertificateFilePath,
                    enrollmentFilePath,
                    "remote-client",
                    "private-network-trust-v1"
                ]);

        Assert.Equal(
            publicCertificateFilePath,
            arguments.PublicCertificateFilePath);
        Assert.Equal(
            enrollmentFilePath,
            arguments.EnrollmentFilePath);
        Assert.Equal(
            "remote-client",
            arguments.PrincipalId);
        Assert.Equal(
            "private-network-trust-v1",
            arguments.TrustPolicyId);
    }

    [Theory]
    [InlineData()]
    [InlineData("one")]
    [InlineData("one", "two", "three")]
    [InlineData("one", "two", "three", "four", "five")]
    public void ParseArguments_InvalidShape_ShouldThrow(
        params string[] arguments)
    {
        Assert.Throws<ArgumentException>(
            "arguments",
            () =>
                ProvisionClientEnrollmentScenario.ParseArguments(
                    arguments));
    }

    [Theory]
    [MemberData(nameof(InvalidValues))]
    public void ParseArguments_InvalidValue_ShouldThrow(
        string publicCertificateFilePath,
        string enrollmentFilePath,
        string principalId,
        string trustPolicyId)
    {
        Assert.ThrowsAny<ArgumentException>(
            () =>
                ProvisionClientEnrollmentScenario.ParseArguments(
                    [
                        publicCertificateFilePath,
                        enrollmentFilePath,
                        principalId,
                        trustPolicyId
                    ]));
    }

    public static TheoryData<string, string, string, string> InvalidValues
    {
        get;
    } =
        new()
        {
            {
                "public-client.cer",
                Path.Combine(
                    Path.GetTempPath(),
                    "client-enrollments.json"),
                "remote-client",
                "private-network-trust-v1"
            },
            {
                Path.Combine(
                    Path.GetTempPath(),
                    "public-client.cer"),
                "client-enrollments.json",
                "remote-client",
                "private-network-trust-v1"
            },
            {
                Path.Combine(
                    Path.GetTempPath(),
                    "public-client.cer"),
                Path.Combine(
                    Path.GetTempPath(),
                    "client-enrollments.json"),
                " ",
                "private-network-trust-v1"
            },
            {
                Path.Combine(
                    Path.GetTempPath(),
                    "public-client.cer"),
                Path.Combine(
                    Path.GetTempPath(),
                    "client-enrollments.json"),
                "remote-client",
                " "
            }
        };
}
