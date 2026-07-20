namespace Hase.CompactProtocol;

/// <summary>
/// Executes compact endpoint commands through an established Compact Serial
/// Protocol connection.
/// </summary>
internal sealed class CompactCommandExecutor
{
    private static int _nextCorrelationId;

    private readonly ICompactSerialProtocolConnection _connection;
    private readonly Func<byte> _correlationIdFactory;

    public CompactCommandExecutor(
        ICompactSerialProtocolConnection connection)
        : this(
            connection,
            CreateCorrelationId)
    {
    }

    internal CompactCommandExecutor(
        ICompactSerialProtocolConnection connection,
        Func<byte> correlationIdFactory)
    {
        _connection =
            connection
            ?? throw new ArgumentNullException(
                nameof(connection));

        _correlationIdFactory =
            correlationIdFactory
            ?? throw new ArgumentNullException(
                nameof(correlationIdFactory));
    }

    /// <summary>
    /// Executes one compact command and returns the endpoint-reported execution
    /// status.
    /// </summary>
    public async Task<CompactCommandExecutionStatus> ExecuteAsync(
        byte commandId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (commandId == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(commandId),
                commandId,
                "A compact command identifier must be nonzero.");
        }

        byte correlationId =
            _correlationIdFactory();

        if (correlationId == 0)
        {
            throw new InvalidOperationException(
                "The compact correlation-identifier factory returned "
                + "the reserved zero value.");
        }

        var request =
            new CompactExecuteCommandRequest(
                correlationId,
                commandId);

        CompactSerialFrame responseFrame =
            await _connection.ExchangeAsync(
                CompactExecuteCommandCodec.EncodeRequest(
                    request),
                cancellationToken);

        CompactExecuteCommandResponse response =
            CompactExecuteCommandCodec.DecodeResponse(
                responseFrame);

        if (response.CommandId != commandId)
        {
            throw new InvalidDataException(
                $"Compact command response identifier 0x{response.CommandId:X2} "
                + $"does not match requested command identifier "
                + $"0x{commandId:X2}.");
        }

        return response.Status;
    }

    private static byte CreateCorrelationId()
    {
        uint value =
            unchecked(
                (uint)Interlocked.Increment(
                    ref _nextCorrelationId));

        return checked(
            (byte)(((value - 1) % byte.MaxValue) + 1));
    }
}
