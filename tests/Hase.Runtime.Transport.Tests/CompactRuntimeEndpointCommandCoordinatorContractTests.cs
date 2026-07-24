using Hase.CompactProtocol;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class CompactRuntimeEndpointCommandCoordinatorContractTests
{
    [Fact]
    public void Coordinator_ExposesCancellableCompactCommandExecution()
    {
        System.Reflection.MethodInfo? method =
            typeof(CompactRuntimeEndpointConnectionCoordinator)
                .GetMethod(
                    "ExecuteCommandAsync");

        Assert.NotNull(
            method);

        Assert.Equal(
            typeof(Task<CompactCommandExecutionStatus>),
            method.ReturnType);

        System.Reflection.ParameterInfo[] parameters =
            method.GetParameters();

        Assert.Collection(
            parameters,
            commandId =>
                Assert.Equal(
                    typeof(byte),
                    commandId.ParameterType),
            cancellation =>
            {
                Assert.Equal(
                    typeof(CancellationToken),
                    cancellation.ParameterType);

                Assert.True(
                    cancellation.IsOptional);
            });
    }
}