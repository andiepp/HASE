using Hase.Runtime.Northbound;

namespace Hase.Runtime.Tests.Northbound;

public sealed class RuntimeHostCommandServiceContractTests
{
    [Fact]
    public void Contract_ExposesNormalizedCommandExecution()
    {
        Type serviceType =
            typeof(IRuntimeHostCommandService);

        Assert.True(
            serviceType.IsInterface);

        var executeMethod =
            serviceType.GetMethod(
                nameof(IRuntimeHostCommandService.ExecuteAsync));

        Assert.NotNull(
            executeMethod);

        Assert.Equal(
            typeof(Task<RuntimeHostCommandOperationResult>),
            executeMethod.ReturnType);

        var parameters =
            executeMethod.GetParameters();

        Assert.Collection(
            parameters,
            target =>
            {
                Assert.Equal(
                    "target",
                    target.Name);

                Assert.Equal(
                    typeof(RuntimeHostCommandTarget),
                    target.ParameterType);
            },
            argument =>
            {
                Assert.Equal(
                    "argument",
                    argument.Name);

                Assert.Equal(
                    typeof(object),
                    argument.ParameterType);
            },
            cancellationToken =>
            {
                Assert.Equal(
                    "cancellationToken",
                    cancellationToken.Name);

                Assert.Equal(
                    typeof(CancellationToken),
                    cancellationToken.ParameterType);

                Assert.True(
                    cancellationToken.HasDefaultValue);
            });
    }
}