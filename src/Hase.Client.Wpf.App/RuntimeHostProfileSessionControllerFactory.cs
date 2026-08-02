using Hase.Client.Configuration;
using Hase.Client.Diagnostics;

namespace Hase.Client.Wpf.AppHost;

public sealed class RuntimeHostProfileSessionControllerFactory
    : IRuntimeHostProfileSessionControllerFactory
{
    private readonly IRuntimeHostProfileClientSessionFactory sessionFactory;
    private readonly ClientDiagnosticPublisher diagnostics;
    public RuntimeHostProfileSessionControllerFactory(
        IRuntimeHostProfileClientSessionFactory sessionFactory,
        ClientDiagnosticPublisher diagnostics)
    {
        this.sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
        this.diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
    }
    public IRuntimeHostProfileSessionController Create(RuntimeHostProfile profile) =>
        new RuntimeHostProfileSessionController(profile, sessionFactory, diagnostics);
}
