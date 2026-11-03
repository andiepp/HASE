namespace Hase.Mcnf.Tests;

public sealed class McnfSessionTests
{
    private static readonly McnfFramingOptions Options = new(
        TimeSpan.FromSeconds(5),
        nodeBufferSize: 128);

    private static McnfRequestFrame SensorRequest() =>
        McnfRequestFrame.Create(0xA5, 0x20, [], responseLength: 4);

    [Fact]
    public async Task ExchangeAsync_TransmitsFrameAndParsesSuccessResponse()
    {
        var stream = new ScriptedMcnfByteStream(
            McnfResponseFrameTests.BuildSuccessResponse(0x02, 0x9A));
        await using var session = new McnfSession(stream, Options);

        McnfResponseFrame response = await session.ExchangeAsync(SensorRequest());

        Assert.True(response.IsSuccess);
        Assert.Equal(new byte[] { 0x02, 0x9A }, response.Payload.ToArray());
        Assert.Equal(SensorRequest().Bytes.ToArray(), Assert.Single(stream.Writes));
        Assert.Equal(McnfSessionState.Open, session.State);
    }

    [Fact]
    public async Task ExchangeAsync_AssemblesResponseFromFragmentedReads()
    {
        byte[] response = McnfResponseFrameTests.BuildSuccessResponse(0x02, 0x9A);
        var stream = new ScriptedMcnfByteStream(
            response[..1],
            response[1..3],
            response[3..]);
        await using var session = new McnfSession(stream, Options);

        McnfResponseFrame parsed = await session.ExchangeAsync(SensorRequest());

        Assert.Equal(new byte[] { 0x02, 0x9A }, parsed.Payload.ToArray());
    }

    [Fact]
    public async Task ExchangeAsync_ReturnsDeviceErrorWithoutFaultingTheSession()
    {
        var stream = new ScriptedMcnfByteStream(
            new byte[] { 0x10, 0x00, 0x00, 0x00 },
            McnfResponseFrameTests.BuildSuccessResponse(0x00, 0x10));
        await using var session = new McnfSession(stream, Options);

        McnfResponseFrame response = await session.ExchangeAsync(SensorRequest());

        Assert.False(response.IsSuccess);
        Assert.Equal(0x10, response.ErrorCode);
        Assert.Equal(McnfSessionState.Open, session.State);

        McnfResponseFrame next = await session.ExchangeAsync(SensorRequest());
        Assert.True(next.IsSuccess);
    }

    [Fact]
    public async Task ExchangeAsync_FaultsOnCorruptSuccessResponse()
    {
        byte[] corrupt = McnfResponseFrameTests.BuildSuccessResponse(0x02, 0x9A);
        corrupt[^1] ^= 0xFF;
        var stream = new ScriptedMcnfByteStream(corrupt);
        await using var session = new McnfSession(stream, Options);

        var failure = await Assert.ThrowsAsync<McnfExchangeException>(
            () => session.ExchangeAsync(SensorRequest()));

        Assert.True(failure.ExecutionMayHaveOccurred);
        Assert.Equal(McnfSessionState.Faulted, session.State);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.ExchangeAsync(SensorRequest()));
    }

    [Fact]
    public async Task ExchangeAsync_FaultsUncertainOnEndOfStream()
    {
        var stream = new ScriptedMcnfByteStream(new byte[] { 0x00, 0x02 });
        await using var session = new McnfSession(stream, Options);

        var failure = await Assert.ThrowsAsync<McnfExchangeException>(
            () => session.ExchangeAsync(SensorRequest()));

        Assert.True(failure.ExecutionMayHaveOccurred);
        Assert.IsType<EndOfStreamException>(failure.InnerException);
        Assert.Equal(McnfSessionState.Faulted, session.State);
    }

    [Fact]
    public async Task ExchangeAsync_FaultsUncertainOnTimeout()
    {
        var options = new McnfFramingOptions(
            TimeSpan.FromMilliseconds(50),
            nodeBufferSize: 128);
        await using var session = new McnfSession(new PendingMcnfByteStream(), options);

        var failure = await Assert.ThrowsAsync<McnfExchangeException>(
            () => session.ExchangeAsync(SensorRequest()));

        Assert.True(failure.ExecutionMayHaveOccurred);
        Assert.IsType<TimeoutException>(failure.InnerException);
        Assert.Equal(McnfSessionState.Faulted, session.State);
    }

    [Fact]
    public async Task ExchangeAsync_RejectsRequestsExceedingTheNodeBuffer()
    {
        var options = new McnfFramingOptions(TimeSpan.FromSeconds(1), nodeBufferSize: 8);
        await using var session = new McnfSession(new ScriptedMcnfByteStream(), options);

        var request = McnfRequestFrame.Create(0xA5, 0x11, new byte[14], responseLength: 2);
        await Assert.ThrowsAsync<ArgumentException>(
            () => session.ExchangeAsync(request));
    }

    [Fact]
    public async Task ExchangeAsync_RejectsExpectedResponsesExceedingTheNodeBuffer()
    {
        var options = new McnfFramingOptions(TimeSpan.FromSeconds(1), nodeBufferSize: 8);
        await using var session = new McnfSession(new ScriptedMcnfByteStream(), options);

        var request = McnfRequestFrame.Create(0xA5, 0x20, [], responseLength: 9);
        await Assert.ThrowsAsync<ArgumentException>(
            () => session.ExchangeAsync(request));
    }

    [Fact]
    public async Task ConnectivityTest_SendsSingleByteAndAcceptsFixedResponse()
    {
        var stream = new ScriptedMcnfByteStream(new byte[] { 0x21 });
        await using var session = new McnfSession(stream, Options);

        await session.ConnectivityTestAsync();

        Assert.Equal(new byte[] { 0xA1 }, Assert.Single(stream.Writes));
        Assert.Equal(McnfSessionState.Open, session.State);
    }

    [Fact]
    public async Task ConnectivityTest_FaultsOnUnexpectedResponseByte()
    {
        var stream = new ScriptedMcnfByteStream(new byte[] { 0x20 });
        await using var session = new McnfSession(stream, Options);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => session.ConnectivityTestAsync());
        Assert.Equal(McnfSessionState.Faulted, session.State);
    }

    [Fact]
    public async Task DisposeAsync_DisposesTheStreamAndRejectsFurtherExchanges()
    {
        var stream = new ScriptedMcnfByteStream();
        var session = new McnfSession(stream, Options);

        await session.DisposeAsync();

        Assert.True(stream.Disposed);
        Assert.Equal(McnfSessionState.Disposed, session.State);
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => session.ExchangeAsync(SensorRequest()));
    }

    [Fact]
    public async Task ExchangeAsync_PublishesDiagnosticLifecycle()
    {
        var observer = new RecordingDiagnosticObserver();
        var stream = new ScriptedMcnfByteStream(
            McnfResponseFrameTests.BuildSuccessResponse(0x02, 0x9A));
        await using var session = new McnfSession(stream, Options, observer);

        await session.ExchangeAsync(SensorRequest());

        Assert.IsType<McnfDiagnosticExchangeStarted>(observer.Events[0]);
        var transmitted = Assert.IsType<McnfDiagnosticBytesObserved>(observer.Events[1]);
        Assert.Equal(McnfDiagnosticDirection.Transmit, transmitted.Direction);
        Assert.Equal(SensorRequest().FrameLength, transmitted.ByteCount);
        var received = Assert.IsType<McnfDiagnosticBytesObserved>(observer.Events[2]);
        Assert.Equal(McnfDiagnosticDirection.Receive, received.Direction);
        Assert.IsType<McnfDiagnosticExchangeCompleted>(observer.Events[^1]);
        Assert.All(
            observer.Events,
            diagnosticEvent => Assert.Equal(
                McnfDiagnosticExchangeKind.Exchange,
                diagnosticEvent.ExchangeKind));
    }

    [Fact]
    public async Task ConnectivityTest_PublishesConnectivityDiagnosticKind()
    {
        var observer = new RecordingDiagnosticObserver();
        var stream = new ScriptedMcnfByteStream(new byte[] { 0x21 });
        await using var session = new McnfSession(stream, Options, observer);

        await session.ConnectivityTestAsync();

        Assert.All(
            observer.Events,
            diagnosticEvent => Assert.Equal(
                McnfDiagnosticExchangeKind.ConnectivityTest,
                diagnosticEvent.ExchangeKind));
    }

    [Fact]
    public async Task ExchangeAsync_PublishesUncertainFailureDiagnostics()
    {
        var observer = new RecordingDiagnosticObserver();
        var stream = new ScriptedMcnfByteStream(new byte[] { 0x00 });
        await using var session = new McnfSession(stream, Options, observer);

        await Assert.ThrowsAsync<McnfExchangeException>(
            () => session.ExchangeAsync(SensorRequest()));

        var failed = Assert.IsType<McnfDiagnosticExchangeFailed>(observer.Events[^1]);
        Assert.Equal(McnfDiagnosticOutcome.Uncertain, failed.Outcome);
        Assert.True(failed.ExecutionMayHaveOccurred);
    }

    private static readonly McnfFramingOptions SkippingOptions = new(
        TimeSpan.FromSeconds(5),
        nodeBufferSize: 128,
        skipsLeadingText: true);

    // as the RF-Lab firmware prints it before answering AM and FM
    private static readonly byte[] DebugText =
        "highByte((uint16_t)Step_Rate)=1\r\nFM wave saved to RAM...\r\n"u8.ToArray();

    private static McnfRequestFrame AcknowledgedRequest() =>
        McnfRequestFrame.Create(0xA5, 0x12, [], responseLength: 2);

    [Fact]
    public void FramingOptions_DoNotSkipLeadingTextByDefault()
    {
        Assert.False(Options.SkipsLeadingText);
        Assert.True(SkippingOptions.SkipsLeadingText);
    }

    [Fact]
    public async Task ExchangeAsync_WithoutSkipping_TakesLeadingTextForTheResponse()
    {
        var stream = new ScriptedMcnfByteStream(
            DebugText,
            McnfResponseFrameTests.BuildSuccessResponse());
        await using var session = new McnfSession(stream, Options);

        McnfResponseFrame response = await session.ExchangeAsync(AcknowledgedRequest());

        Assert.Equal((byte)'h', response.ErrorCode);
    }

    [Fact]
    public async Task ExchangeAsync_SkipsLeadingTextAndStaysInStep()
    {
        var stream = new ScriptedMcnfByteStream(
            [.. DebugText, .. McnfResponseFrameTests.BuildSuccessResponse()],
            McnfResponseFrameTests.BuildSuccessResponse(0x02, 0x9A));
        await using var session = new McnfSession(stream, SkippingOptions);

        McnfResponseFrame acknowledged = await session.ExchangeAsync(AcknowledgedRequest());
        McnfResponseFrame next = await session.ExchangeAsync(SensorRequest());

        Assert.True(acknowledged.IsSuccess);
        Assert.True(next.IsSuccess);
        Assert.Equal(new byte[] { 0x02, 0x9A }, next.Payload.ToArray());
        Assert.Equal(McnfSessionState.Open, session.State);
    }

    [Fact]
    public async Task ExchangeAsync_KeepsPrintableBytesInsideTheResponse()
    {
        var stream = new ScriptedMcnfByteStream(
            "debug\r\n"u8.ToArray(),
            McnfResponseFrameTests.BuildSuccessResponse(0x41, 0x0A));
        await using var session = new McnfSession(stream, SkippingOptions);

        McnfResponseFrame response = await session.ExchangeAsync(SensorRequest());

        Assert.Equal(new byte[] { 0x41, 0x0A }, response.Payload.ToArray());
    }

    [Theory]
    [InlineData(0x0A)]
    [InlineData(0x0D)]
    public async Task ExchangeAsync_RecognizesLineEndErrorCodesWithoutText(byte errorCode)
    {
        var stream = new ScriptedMcnfByteStream(new byte[] { errorCode, 0x00 });
        await using var session = new McnfSession(stream, SkippingOptions);

        McnfResponseFrame response = await session.ExchangeAsync(AcknowledgedRequest());

        Assert.Equal(errorCode, response.ErrorCode);
    }

    [Fact]
    public async Task ConnectivityTest_DoesNotSkipItsPrintableAnswer()
    {
        var stream = new ScriptedMcnfByteStream(new byte[] { 0x21 });
        await using var session = new McnfSession(stream, SkippingOptions);

        await session.ConnectivityTestAsync();

        Assert.Equal(McnfSessionState.Open, session.State);
    }

    [Fact]
    public async Task ExchangeAsync_PublishesSkippedTextApartFromTheResponse()
    {
        var observer = new RecordingDiagnosticObserver();
        byte[] response = McnfResponseFrameTests.BuildSuccessResponse();
        var stream = new ScriptedMcnfByteStream([.. DebugText, .. response]);
        await using var session = new McnfSession(stream, SkippingOptions, observer);

        await session.ExchangeAsync(AcknowledgedRequest());

        var text = Assert.Single(observer.Events.OfType<McnfDiagnosticTextSkipped>());
        Assert.Equal(DebugText, text.ToArray());
        Assert.Equal(
            response,
            observer.Events
                .OfType<McnfDiagnosticBytesObserved>()
                .Where(observed => observed.Direction == McnfDiagnosticDirection.Receive)
                .SelectMany(observed => observed.Bytes)
                .ToArray());
        Assert.True(
            observer.Events.IndexOf(text)
            < observer.Events.FindIndex(diagnosticEvent =>
                diagnosticEvent is McnfDiagnosticBytesObserved
                {
                    Direction: McnfDiagnosticDirection.Receive
                }));
    }

    [Fact]
    public async Task ExchangeAsync_PublishesSkippedTextBeforeAFailure()
    {
        var observer = new RecordingDiagnosticObserver();
        var stream = new ScriptedMcnfByteStream(DebugText);
        await using var session = new McnfSession(stream, SkippingOptions, observer);

        var failure = await Assert.ThrowsAsync<McnfExchangeException>(
            () => session.ExchangeAsync(AcknowledgedRequest()));

        Assert.IsType<EndOfStreamException>(failure.InnerException);
        var text = Assert.IsType<McnfDiagnosticTextSkipped>(observer.Events[^2]);
        Assert.Equal(DebugText, text.ToArray());
        Assert.IsType<McnfDiagnosticExchangeFailed>(observer.Events[^1]);
    }

    private sealed class RecordingDiagnosticObserver : IMcnfDiagnosticObserver
    {
        public List<McnfDiagnosticEvent> Events { get; } = [];

        public void Observe(McnfDiagnosticEvent diagnosticEvent) =>
            Events.Add(diagnosticEvent);
    }
}
