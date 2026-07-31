using System.Windows.Threading;
using Hase.Client.Diagnostics;
using Hase.Client.Grpc;
using Hase.Client.Wpf.Services;

namespace Hase.Client.Wpf.AppHost;

/// <summary>
/// Composes the concrete gRPC adapter and WPF dispatcher at the executable
/// application boundary.
/// </summary>
public static class RuntimeHostClientComposition
{
    public static IRuntimeHostClientSessionFactory CreateSessionFactory(
        ClientDiagnosticPublisher? diagnostics = null) =>
        diagnostics is null
            ? new RuntimeHostGrpcRecoveringClientSessionFactory()
            : new RuntimeHostGrpcRecoveringClientSessionFactory(diagnostics);

    public static IClientUiDispatcher CreateDispatcher(
        Dispatcher dispatcher) =>
        new WpfClientUiDispatcher(
            dispatcher);
}
