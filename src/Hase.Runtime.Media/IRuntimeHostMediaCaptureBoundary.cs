namespace Hase.Runtime.Media;

public interface IRuntimeHostMediaCaptureBoundary
{
    ValueTask OpenAsync(
        RuntimeHostMediaSourceConfiguration source,
        bool includeAudio,
        CancellationToken cancellationToken);

    ValueTask SubmitNegotiationAsync(
        RuntimeHostMediaNegotiationMessage message,
        CancellationToken cancellationToken);

    ValueTask CloseAsync(CancellationToken cancellationToken);
}
