using Hase.Client.Configuration;

namespace Hase.Client.Wpf.AppHost;

public sealed class RuntimeHostProfileSessionControllerFactory
    : IRuntimeHostProfileSessionControllerFactory
{
    private readonly IRuntimeHostProfileClientSessionFactory sessionFactory;
    public RuntimeHostProfileSessionControllerFactory(IRuntimeHostProfileClientSessionFactory sessionFactory) =>
        this.sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
    public IRuntimeHostProfileSessionController Create(RuntimeHostProfile profile) =>
        new RuntimeHostProfileSessionController(profile, sessionFactory);
}
