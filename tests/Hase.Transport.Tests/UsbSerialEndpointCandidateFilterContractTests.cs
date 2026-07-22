using Hase.Transport.Discovery;

namespace Hase.Transport.Tests;

public sealed class UsbSerialEndpointCandidateFilterContractTests
{
    [Fact]
    public void IsMatch_MatchingCandidate_ShouldReturnTrue()
    {
        // Arrange
        IUsbSerialEndpointCandidateFilter filter =
            new StubUsbSerialEndpointCandidateFilter(
                result: true);

        var candidate =
            new UsbSerialEndpointCandidate(
                "COM10");

        // Act
        bool result =
            filter.IsMatch(
                candidate);

        // Assert
        Assert.True(
            result);
    }

    [Fact]
    public void IsMatch_NonMatchingCandidate_ShouldReturnFalse()
    {
        // Arrange
        IUsbSerialEndpointCandidateFilter filter =
            new StubUsbSerialEndpointCandidateFilter(
                result: false);

        var candidate =
            new UsbSerialEndpointCandidate(
                "COM10");

        // Act
        bool result =
            filter.IsMatch(
                candidate);

        // Assert
        Assert.False(
            result);
    }

    private sealed class StubUsbSerialEndpointCandidateFilter
        : IUsbSerialEndpointCandidateFilter
    {
        private readonly bool _result;

        public StubUsbSerialEndpointCandidateFilter(
            bool result)
        {
            _result =
                result;
        }

        public bool IsMatch(
            UsbSerialEndpointCandidate candidate)
        {
            return _result;
        }
    }
}