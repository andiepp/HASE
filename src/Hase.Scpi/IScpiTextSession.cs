namespace Hase.Scpi;

public interface IScpiTextSession : IAsyncDisposable
{
    ScpiTextSessionState State { get; }

    Task SendCommandAsync(string command, CancellationToken cancellationToken = default);

    Task<string> QueryAsync(string query, CancellationToken cancellationToken = default);
}
