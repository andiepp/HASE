using Hase.Client;

namespace Hase.Client.Tests;

public sealed class RemoteCommandOperationResultTests
{
    [Fact]
    public void Successful_WithoutReturnValue_ShouldCreateSuccess()
    {
        RemoteCommandOperationResult result =
            RemoteCommandOperationResult.Successful();

        Assert.Equal(
            RemoteCommandOperationStatus.Success,
            result.Status);
        Assert.True(
            result.IsSuccess);
        Assert.Null(
            result.ReturnValue);
        Assert.Null(
            result.Diagnostic);
    }

    [Fact]
    public void Successful_ReturnValue_ShouldCreateSuccess()
    {
        RemoteValue returnValue =
            RemoteValue.FromString(
                "completed");

        RemoteCommandOperationResult result =
            RemoteCommandOperationResult.Successful(
                returnValue);

        Assert.True(
            result.IsSuccess);
        Assert.Same(
            returnValue,
            result.ReturnValue);
    }

    [Theory]
    [InlineData(RemoteCommandOperationStatus.AttachmentNotCurrent)]
    [InlineData(RemoteCommandOperationStatus.InstrumentNotFound)]
    [InlineData(RemoteCommandOperationStatus.CommandNotFound)]
    [InlineData(RemoteCommandOperationStatus.ArgumentNotSupported)]
    [InlineData(RemoteCommandOperationStatus.EndpointUnavailable)]
    [InlineData(RemoteCommandOperationStatus.EndpointRejected)]
    [InlineData(RemoteCommandOperationStatus.EndpointFailure)]
    [InlineData(RemoteCommandOperationStatus.TimedOut)]
    public void Failed_FailureStatus_ShouldCreateFailure(
        RemoteCommandOperationStatus status)
    {
        RemoteCommandOperationResult result =
            RemoteCommandOperationResult.Failed(
                status,
                "  diagnostic  ");

        Assert.Equal(
            status,
            result.Status);
        Assert.False(
            result.IsSuccess);
        Assert.Null(
            result.ReturnValue);
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
        RemoteCommandOperationResult result =
            RemoteCommandOperationResult.Failed(
                RemoteCommandOperationStatus.TimedOut,
                diagnostic);

        Assert.Null(
            result.Diagnostic);
    }

    [Fact]
    public void Failed_UnspecifiedStatus_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            "status",
            () => RemoteCommandOperationResult.Failed(
                RemoteCommandOperationStatus.Unspecified));
    }

    [Fact]
    public void Failed_UndefinedStatus_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            "status",
            () => RemoteCommandOperationResult.Failed(
                (RemoteCommandOperationStatus) 99));
    }

    [Fact]
    public void Failed_SuccessStatus_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            "status",
            () => RemoteCommandOperationResult.Failed(
                RemoteCommandOperationStatus.Success));
    }
}
