using MediaV1 = global::Hase.Runtime.Media.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostMediaControlContractValidatorTests
{
    private readonly RuntimeHostMediaControlContractValidator validator =
        new();

    [Fact]
    public void ValidateSourceTarget_ValidTarget_ShouldSucceed()
    {
        validator.ValidateSourceTarget(
            new MediaV1.MediaSourceTarget
            {
                MediaSourceId = "primary-camera",
                MediaSourceGeneration = "generation-01"
            });
    }

    [Theory]
    [InlineData("", "generation-01")]
    [InlineData(" ", "generation-01")]
    [InlineData("primary-camera", "")]
    [InlineData("primary-camera", " ")]
    public void ValidateSourceTarget_MissingIdentity_ShouldThrow(
        string sourceId,
        string generation)
    {
        MediaV1.MediaSourceTarget target =
            new()
            {
                MediaSourceId = sourceId,
                MediaSourceGeneration = generation
            };

        Assert.Throws<ArgumentException>(
            () => validator.ValidateSourceTarget(target));
    }

    [Fact]
    public void ValidateSourceTarget_MultibyteIdentityBeyondLimit_ShouldThrow()
    {
        MediaV1.MediaSourceTarget target =
            new()
            {
                MediaSourceId = new string('\u00e4', 65),
                MediaSourceGeneration = "generation-01"
            };

        Assert.Throws<ArgumentException>(
            () => validator.ValidateSourceTarget(target));
    }

    [Fact]
    public void ValidateSessionId_AtUtf8Limit_ShouldSucceed()
    {
        validator.ValidateSessionId(
            new string(
                's',
                RuntimeHostMediaControlLimits.MaximumSessionIdUtf8Bytes));
    }

    [Fact]
    public void ValidateSessionId_BeyondUtf8Limit_ShouldThrow()
    {
        string sessionId =
            new(
                's',
                RuntimeHostMediaControlLimits.MaximumSessionIdUtf8Bytes + 1);

        Assert.Throws<ArgumentException>(
            () => validator.ValidateSessionId(sessionId));
    }

    [Theory]
    [InlineData(MediaV1.MediaNegotiationMessageKind.Offer)]
    [InlineData(MediaV1.MediaNegotiationMessageKind.Answer)]
    public void ValidateNegotiationMessage_ValidDescription_ShouldSucceed(
        MediaV1.MediaNegotiationMessageKind kind)
    {
        validator.ValidateNegotiationMessage(
            CreateMessage(
                kind,
                "v=0"));
    }

    [Fact]
    public void ValidateNegotiationMessage_ValidCandidate_ShouldSucceed()
    {
        validator.ValidateNegotiationMessage(
            CreateMessage(
                MediaV1.MediaNegotiationMessageKind.IceCandidate,
                "candidate:1"));
    }

    [Fact]
    public void ValidateNegotiationMessage_EmptyIceCompletion_ShouldSucceed()
    {
        validator.ValidateNegotiationMessage(
            CreateMessage(
                MediaV1.MediaNegotiationMessageKind.IceComplete,
                string.Empty));
    }

    [Fact]
    public void ValidateNegotiationMessage_ZeroSequence_ShouldThrow()
    {
        MediaV1.MediaNegotiationMessage message =
            CreateMessage(
                MediaV1.MediaNegotiationMessageKind.Offer,
                "v=0");
        message.Sequence = 0;

        Assert.Throws<ArgumentException>(
            () => validator.ValidateNegotiationMessage(message));
    }

    [Fact]
    public void ValidateNegotiationMessage_UnsupportedKind_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                validator.ValidateNegotiationMessage(
                    CreateMessage(
                        (MediaV1.MediaNegotiationMessageKind)int.MaxValue,
                        "value")));
    }

    [Fact]
    public void ValidateNegotiationMessage_OversizedDescription_ShouldThrow()
    {
        string payload =
            new(
                's',
                RuntimeHostMediaControlLimits
                    .MaximumSessionDescriptionUtf8Bytes + 1);

        Assert.Throws<ArgumentException>(
            () =>
                validator.ValidateNegotiationMessage(
                    CreateMessage(
                        MediaV1.MediaNegotiationMessageKind.Offer,
                        payload)));
    }

    [Fact]
    public void ValidateNegotiationMessage_OversizedCandidate_ShouldThrow()
    {
        string payload =
            new(
                'c',
                RuntimeHostMediaControlLimits
                    .MaximumIceCandidateUtf8Bytes + 1);

        Assert.Throws<ArgumentException>(
            () =>
                validator.ValidateNegotiationMessage(
                    CreateMessage(
                        MediaV1.MediaNegotiationMessageKind.IceCandidate,
                        payload)));
    }

    [Fact]
    public void ValidateNegotiationMessage_IceCompletionPayload_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            () =>
                validator.ValidateNegotiationMessage(
                    CreateMessage(
                        MediaV1.MediaNegotiationMessageKind.IceComplete,
                        "unexpected")));
    }

    private static MediaV1.MediaNegotiationMessage CreateMessage(
        MediaV1.MediaNegotiationMessageKind kind,
        string payload)
    {
        return new MediaV1.MediaNegotiationMessage
        {
            Sequence = 1,
            Kind = kind,
            SensitivePayload = payload
        };
    }
}
