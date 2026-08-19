using Google.Protobuf;
using Google.Protobuf.Reflection;
using Hase.Runtime.Media.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Contracts.Tests;

public sealed class RuntimeHostMediaControlV1ContractTests
{
    [Fact]
    public void Contract_UsesIndependentVersionedPackageAndService()
    {
        FileDescriptor descriptor =
            RuntimeHostMediaControlV1Reflection.Descriptor;

        Assert.Equal(
            "hase.runtime.media.v1",
            descriptor.Package);

        ServiceDescriptor service =
            Assert.Single(descriptor.Services);

        Assert.Equal(
            "RuntimeHostMediaControl",
            service.Name);
        Assert.Equal(
            [
                "GetMediaCapabilities",
                "WatchMediaCapabilities",
                "StartMediaSession",
                "ExchangeMediaNegotiation",
                "GetMediaSessionStatus",
                "StopMediaSession"
            ],
            service.Methods.Select(method => method.Name));
        Assert.All(
            service.Methods.Where(method =>
                method.Name != "WatchMediaCapabilities"),
            method =>
            {
                Assert.False(method.IsClientStreaming);
                Assert.False(method.IsServerStreaming);
            });
        MethodDescriptor watch = service.Methods.Single(method =>
            method.Name == "WatchMediaCapabilities");
        Assert.False(watch.IsClientStreaming);
        Assert.True(watch.IsServerStreaming);
    }

    [Fact]
    public void Contract_DefinesExactUnaryOperationShapes()
    {
        AssertMethod(
            "GetMediaCapabilities",
            GetMediaCapabilitiesRequest.Descriptor,
            GetMediaCapabilitiesResponse.Descriptor);
        AssertStreamingMethod(
            "WatchMediaCapabilities",
            WatchMediaCapabilitiesRequest.Descriptor,
            GetMediaCapabilitiesResponse.Descriptor);
        AssertMethod(
            "StartMediaSession",
            StartMediaSessionRequest.Descriptor,
            StartMediaSessionResponse.Descriptor);
        AssertMethod(
            "ExchangeMediaNegotiation",
            ExchangeMediaNegotiationRequest.Descriptor,
            ExchangeMediaNegotiationResponse.Descriptor);
        AssertMethod(
            "GetMediaSessionStatus",
            GetMediaSessionStatusRequest.Descriptor,
            GetMediaSessionStatusResponse.Descriptor);
        AssertMethod(
            "StopMediaSession",
            StopMediaSessionRequest.Descriptor,
            StopMediaSessionResponse.Descriptor);
    }

    [Fact]
    public void CapabilityResponse_DefinesSanitizedSourcesAndLimits()
    {
        AssertFields(
            GetMediaCapabilitiesResponse.Descriptor,
            ("runtime_host_id", 1, FieldType.String, false),
            ("api_version", 2, FieldType.Message, false),
            ("sources", 3, FieldType.Message, true),
            ("limits", 4, FieldType.Message, false),
            ("capability_revision", 5, FieldType.UInt64, false));

        AssertFields(
            WatchMediaCapabilitiesRequest.Descriptor,
            ("after_revision", 1, FieldType.UInt64, false));

        AssertFields(
            MediaSourceCapability.Descriptor,
            ("target", 1, FieldType.Message, false),
            ("availability", 2, FieldType.Enum, false),
            ("supports_video", 3, FieldType.Bool, false),
            ("supports_audio", 4, FieldType.Bool, false),
            ("video_profiles", 5, FieldType.Message, true),
            ("audio_profiles", 6, FieldType.Message, true),
            ("display_name", 7, FieldType.String, false));

        string[] fieldNames =
            RuntimeHostMediaControlV1Reflection.Descriptor.MessageTypes
                .SelectMany(type => type.Fields.InDeclarationOrder())
                .Select(field => field.Name)
                .ToArray();

        Assert.DoesNotContain("device_id", fieldNames);
        Assert.DoesNotContain("device_name", fieldNames);
        Assert.DoesNotContain("friendly_name", fieldNames);
        Assert.DoesNotContain("network_address", fieldNames);
    }

    [Fact]
    public void SourceTarget_ContainsOnlySanitizedIdAndGeneration()
    {
        AssertFields(
            MediaSourceTarget.Descriptor,
            ("media_source_id", 1, FieldType.String, false),
            ("media_source_generation", 2, FieldType.String, false));
    }

    [Fact]
    public void Profiles_AllowOnlyVp8AndOpus()
    {
        Assert.Equal(
            ["Unspecified", "Vp8"],
            Enum.GetNames<MediaVideoCodec>());
        Assert.Equal(
            ["Unspecified", "Opus"],
            Enum.GetNames<MediaAudioCodec>());

        AssertFields(
            MediaVideoProfile.Descriptor,
            ("codec", 1, FieldType.Enum, false),
            ("width", 2, FieldType.UInt32, false),
            ("height", 3, FieldType.UInt32, false),
            ("maximum_frames_per_second", 4, FieldType.UInt32, false));
        AssertFields(
            MediaAudioProfile.Descriptor,
            ("codec", 1, FieldType.Enum, false),
            ("sample_rate_hertz", 2, FieldType.UInt32, false),
            ("maximum_channel_count", 3, FieldType.UInt32, false));
    }

    [Fact]
    public void Start_RequiresExactSourceGenerationAndIndependentAudioRequest()
    {
        AssertFields(
            StartMediaSessionRequest.Descriptor,
            ("target", 1, FieldType.Message, false),
            ("include_audio", 2, FieldType.Bool, false));
        AssertFields(
            StartMediaSessionResponse.Descriptor,
            ("status", 1, FieldType.Enum, false),
            ("session", 2, FieldType.Message, false));
    }

    [Fact]
    public void Negotiation_UsesSequencedBoundedUnaryExchangeShape()
    {
        AssertFields(
            ExchangeMediaNegotiationRequest.Descriptor,
            ("session_id", 1, FieldType.String, false),
            ("acknowledged_delivery_sequence", 2, FieldType.UInt32, false),
            ("submitted_message", 3, FieldType.Message, false));
        AssertFields(
            ExchangeMediaNegotiationResponse.Descriptor,
            ("status", 1, FieldType.Enum, false),
            ("session", 2, FieldType.Message, false),
            ("accepted_submission_sequence", 3, FieldType.UInt32, false),
            ("delivered_messages", 4, FieldType.Message, true),
            ("has_more", 5, FieldType.Bool, false));
        AssertFields(
            MediaNegotiationMessage.Descriptor,
            ("sequence", 1, FieldType.UInt32, false),
            ("kind", 2, FieldType.Enum, false),
            ("sensitive_payload", 3, FieldType.String, false));
    }

    [Fact]
    public void StatusAndStop_RequireOnlyOpaqueSessionId()
    {
        AssertFields(
            GetMediaSessionStatusRequest.Descriptor,
            ("session_id", 1, FieldType.String, false));
        AssertFields(
            StopMediaSessionRequest.Descriptor,
            ("session_id", 1, FieldType.String, false));
        AssertFields(
            GetMediaSessionStatusResponse.Descriptor,
            ("status", 1, FieldType.Enum, false),
            ("session", 2, FieldType.Message, false));
        AssertFields(
            StopMediaSessionResponse.Descriptor,
            ("status", 1, FieldType.Enum, false),
            ("session", 2, FieldType.Message, false));
    }

    [Fact]
    public void SessionSnapshot_ContainsOnlySanitizedStateAndAggregates()
    {
        AssertFields(
            MediaSessionSnapshot.Descriptor,
            ("session_id", 1, FieldType.String, false),
            ("target", 2, FieldType.Message, false),
            ("audio_requested", 3, FieldType.Bool, false),
            ("state", 4, FieldType.Enum, false),
            ("started_at_utc", 5, FieldType.Message, false),
            ("last_transition_at_utc", 6, FieldType.Message, false),
            ("lease_expires_at_utc", 7, FieldType.Message, false),
            ("terminal_reason", 8, FieldType.Enum, false),
            ("aggregate_video_bytes", 9, FieldType.UInt64, false),
            ("aggregate_audio_bytes", 10, FieldType.UInt64, false),
            ("aggregate_video_frames", 11, FieldType.UInt64, false));
    }

    [Fact]
    public void SessionStates_MatchAcceptedLifecycle()
    {
        Assert.Equal(
            [
                "Unspecified",
                "Unavailable",
                "Idle",
                "Starting",
                "Negotiating",
                "Streaming",
                "Stopping",
                "Ended",
                "Faulted"
            ],
            Enum.GetNames<MediaSessionState>());
    }

    [Fact]
    public void OperationStatus_DoesNotProjectAuthorizationDecisions()
    {
        string[] names =
            Enum.GetNames<MediaControlOperationStatus>();

        Assert.DoesNotContain("Unauthorized", names);
        Assert.DoesNotContain("Forbidden", names);
        Assert.Contains("SessionNotOwned", names);
        Assert.Contains("LimitExceeded", names);
    }

    [Fact]
    public void NegotiationMessage_RoundTripPreservesSensitivePayload()
    {
        MediaNegotiationMessage expected =
            new()
            {
                Sequence = 7,
                Kind = MediaNegotiationMessageKind.IceCandidate,
                SensitivePayload = "candidate-value"
            };

        MediaNegotiationMessage actual =
            MediaNegotiationMessage.Parser.ParseFrom(
                expected.ToByteArray());

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Limits_ArePublishedAsUnsignedCountsAndDurations()
    {
        AssertFields(
            MediaControlLimits.Descriptor,
            ("maximum_source_identity_utf8_bytes", 1, FieldType.UInt32, false),
            ("maximum_session_id_utf8_bytes", 2, FieldType.UInt32, false),
            ("maximum_session_description_utf8_bytes", 3, FieldType.UInt32, false),
            ("maximum_ice_candidate_utf8_bytes", 4, FieldType.UInt32, false),
            ("maximum_ice_candidates_per_peer", 5, FieldType.UInt32, false),
            ("maximum_negotiation_messages_per_peer", 6, FieldType.UInt32, false),
            ("maximum_pending_delivery_messages", 7, FieldType.UInt32, false),
            ("maximum_negotiation_exchanges", 8, FieldType.UInt32, false),
            ("negotiation_idle_timeout", 9, FieldType.Message, false),
            ("negotiation_lifetime", 10, FieldType.Message, false),
            ("session_lease_duration", 11, FieldType.Message, false));
    }

    private static void AssertMethod(
        string name,
        MessageDescriptor input,
        MessageDescriptor output)
    {
        ServiceDescriptor service =
            Assert.Single(
                RuntimeHostMediaControlV1Reflection.Descriptor.Services);
        MethodDescriptor method =
            Assert.Single(
                service.Methods,
                candidate => candidate.Name == name);

        Assert.Equal(input.FullName, method.InputType.FullName);
        Assert.Equal(output.FullName, method.OutputType.FullName);
        Assert.False(method.IsClientStreaming);
        Assert.False(method.IsServerStreaming);
    }

    private static void AssertFields(
        MessageDescriptor message,
        params (string Name, int Number, FieldType Type, bool Repeated)[]
            expected)
    {
        FieldDescriptor[] actual =
            message.Fields.InDeclarationOrder().ToArray();

        Assert.Equal(expected.Length, actual.Length);

        for (int index = 0; index < expected.Length; index++)
        {
            Assert.Equal(expected[index].Name, actual[index].Name);
            Assert.Equal(expected[index].Number, actual[index].FieldNumber);
            Assert.Equal(expected[index].Type, actual[index].FieldType);
            Assert.Equal(expected[index].Repeated, actual[index].IsRepeated);
        }
    }

    private static void AssertStreamingMethod(
        string name,
        MessageDescriptor input,
        MessageDescriptor output)
    {
        ServiceDescriptor service = Assert.Single(
            RuntimeHostMediaControlV1Reflection.Descriptor.Services);
        MethodDescriptor method = Assert.Single(
            service.Methods,
            candidate => candidate.Name == name);
        Assert.Equal(input.FullName, method.InputType.FullName);
        Assert.Equal(output.FullName, method.OutputType.FullName);
        Assert.False(method.IsClientStreaming);
        Assert.True(method.IsServerStreaming);
    }
}
