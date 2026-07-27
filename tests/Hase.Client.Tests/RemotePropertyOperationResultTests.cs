using Hase.Client;

namespace Hase.Client.Tests;

public sealed class RemotePropertyOperationResultTests
{
    [Fact]
    public void Successful_ConfirmedValue_ShouldCreateSuccess()
    {
        var confirmedValue =
            new RemotePropertyValue(
                RemoteValue.FromBoolean(
                    true),
                DateTimeOffset.UnixEpoch,
                RemotePropertyQuality.Good);

        RemotePropertyOperationResult result =
            RemotePropertyOperationResult.Successful(
                confirmedValue);

        Assert.Equal(
            RemotePropertyOperationStatus.Success,
            result.Status);
        Assert.True(
            result.IsSuccess);
        Assert.Same(
            confirmedValue,
            result.ConfirmedValue);
        Assert.Null(
            result.Diagnostic);
    }

    [Fact]
    public void Successful_NullConfirmedValue_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "confirmedValue",
            () => RemotePropertyOperationResult.Successful(
                null!));
    }

    [Theory]
    [InlineData(RemotePropertyOperationStatus.AttachmentNotCurrent)]
    [InlineData(RemotePropertyOperationStatus.InstrumentNotFound)]
    [InlineData(RemotePropertyOperationStatus.PropertyNotFound)]
    [InlineData(RemotePropertyOperationStatus.ReadNotSupported)]
    [InlineData(RemotePropertyOperationStatus.WriteNotSupported)]
    [InlineData(RemotePropertyOperationStatus.InvalidValue)]
    [InlineData(RemotePropertyOperationStatus.EndpointUnavailable)]
    [InlineData(RemotePropertyOperationStatus.EndpointRejected)]
    [InlineData(RemotePropertyOperationStatus.EndpointFailure)]
    [InlineData(RemotePropertyOperationStatus.TimedOut)]
    public void Failed_FailureStatus_ShouldCreateFailure(
        RemotePropertyOperationStatus status)
    {
        RemotePropertyOperationResult result =
            RemotePropertyOperationResult.Failed(
                status,
                "  diagnostic  ");

        Assert.Equal(
            status,
            result.Status);
        Assert.False(
            result.IsSuccess);
        Assert.Null(
            result.ConfirmedValue);
        Assert.Equal(
            "diagnostic",
            result.Diagnostic);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Failed_MissingDiagnostic_ShouldNormalizeToNull(
        string? diagnostic)
    {
        RemotePropertyOperationResult result =
            RemotePropertyOperationResult.Failed(
                RemotePropertyOperationStatus.TimedOut,
                diagnostic);

        Assert.Null(
            result.Diagnostic);
    }

    [Fact]
    public void Failed_UnspecifiedStatus_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            "status",
            () => RemotePropertyOperationResult.Failed(
                RemotePropertyOperationStatus.Unspecified));
    }

    [Fact]
    public void Failed_UndefinedStatus_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            "status",
            () => RemotePropertyOperationResult.Failed(
                (RemotePropertyOperationStatus) 99));
    }

    [Fact]
    public void Failed_SuccessStatus_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            "status",
            () => RemotePropertyOperationResult.Failed(
                RemotePropertyOperationStatus.Success));
    }
}
