namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostMediaAuthorizationRequirementsTests
{
    [Fact]
    public void Capabilities_ShouldRequireOnlyCapabilityRead()
    {
        Assert.Equal(
            [RuntimeHostPermission.ReadMediaCapabilities],
            RuntimeHostMediaAuthorizationRequirements.ForCapabilities);
    }

    [Fact]
    public void VideoStart_ShouldRequireVideoAndStartWithoutAudio()
    {
        IReadOnlyList<RuntimeHostPermission> requirements =
            RuntimeHostMediaAuthorizationRequirements.ForStart(
                includeAudio: false);

        Assert.Equal(
            [
                RuntimeHostPermission.ReceiveMediaVideo,
                RuntimeHostPermission.StartMediaSession
            ],
            requirements);
        Assert.DoesNotContain(
            RuntimeHostPermission.ReceiveMediaAudio,
            requirements);
    }

    [Fact]
    public void AudioVideoStart_ShouldRequireIndependentAudioGrant()
    {
        Assert.Equal(
            [
                RuntimeHostPermission.ReceiveMediaVideo,
                RuntimeHostPermission.ReceiveMediaAudio,
                RuntimeHostPermission.StartMediaSession
            ],
            RuntimeHostMediaAuthorizationRequirements.ForStart(
                includeAudio: true));
    }

    [Fact]
    public void SessionOperations_ShouldHaveSeparateRequirements()
    {
        Assert.Equal(
            [RuntimeHostPermission.NegotiateMediaSession],
            RuntimeHostMediaAuthorizationRequirements.ForNegotiation);
        Assert.Equal(
            [RuntimeHostPermission.ReceiveMediaVideo],
            RuntimeHostMediaAuthorizationRequirements.ForStatus);
        Assert.Equal(
            [RuntimeHostPermission.StopMediaSession],
            RuntimeHostMediaAuthorizationRequirements.ForStop);
    }

    [Fact]
    public void ReturnedRequirements_ShouldBeReadOnly()
    {
        IList<RuntimeHostPermission> requirements =
            Assert.IsAssignableFrom<IList<RuntimeHostPermission>>(
                RuntimeHostMediaAuthorizationRequirements.ForStart(
                    includeAudio: true));

        Assert.True(requirements.IsReadOnly);
        Assert.Throws<NotSupportedException>(
            () =>
                requirements.Add(
                    RuntimeHostPermission.ReadSnapshot));
    }
}
