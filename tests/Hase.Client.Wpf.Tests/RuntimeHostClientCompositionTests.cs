using Hase.Client.Grpc;
using Hase.Client.Wpf.AppHost;

namespace Hase.Client.Wpf.Tests;

public sealed class RuntimeHostClientCompositionTests
{
    [Fact]
    public void CreateSessionFactory_ShouldReturnGrpcAdapter()
    {
        IRuntimeHostClientSessionFactory factory =
            RuntimeHostClientComposition.CreateSessionFactory();

        Assert.IsType<
            RuntimeHostGrpcRecoveringClientSessionFactory>(
            factory);
    }

    [Fact]
    public void CreateDispatcher_Null_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "dispatcher",
            () =>
                RuntimeHostClientComposition.CreateDispatcher(
                    null!));
    }
}
