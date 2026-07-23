using Hase.Runtime.Northbound;

namespace Hase.Runtime.Tests.Northbound;

public sealed class RuntimeHostIdentityResolutionContractTests
{
    [Theory]
    [InlineData(RuntimeHostIdentityStoreCreateOutcome.Created)]
    [InlineData(RuntimeHostIdentityStoreCreateOutcome.Existing)]
    public void StoreCreateResult_StoresIdentityAndDefinedOutcome(
        RuntimeHostIdentityStoreCreateOutcome outcome)
    {
        var runtimeHostId =
            new RuntimeHostId(
                "workshop-runtime");

        var result =
            new RuntimeHostIdentityStoreCreateResult(
                runtimeHostId,
                outcome);

        Assert.Same(
            runtimeHostId,
            result.RuntimeHostId);

        Assert.Equal(
            outcome,
            result.Outcome);
    }

    [Fact]
    public void StoreCreateResult_NullIdentity_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RuntimeHostIdentityStoreCreateResult(
                null!,
                RuntimeHostIdentityStoreCreateOutcome.Created));
    }

    [Fact]
    public void StoreCreateResult_UndefinedOutcome_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new RuntimeHostIdentityStoreCreateResult(
                new RuntimeHostId(
                    "workshop-runtime"),
                (RuntimeHostIdentityStoreCreateOutcome)999));
    }

    [Fact]
    public void Generator_ExposesSynchronousGeneration()
    {
        var method =
            typeof(IRuntimeHostIdGenerator).GetMethod(
                nameof(IRuntimeHostIdGenerator.Generate));

        Assert.NotNull(
            method);

        Assert.Equal(
            typeof(RuntimeHostId),
            method.ReturnType);

        Assert.Empty(
            method.GetParameters());
    }

    [Fact]
    public void Store_ExposesReadAndAtomicCreateIfMissing()
    {
        var readMethod =
            typeof(IRuntimeHostIdentityStore).GetMethod(
                nameof(IRuntimeHostIdentityStore.ReadAsync));

        Assert.NotNull(
            readMethod);

        Assert.Equal(
            typeof(Task<RuntimeHostId>),
            readMethod.ReturnType);

        var readParameters =
            readMethod.GetParameters();

        Assert.Single(
            readParameters);

        Assert.Equal(
            typeof(CancellationToken),
            readParameters[0].ParameterType);

        Assert.True(
            readParameters[0].IsOptional);

        var createMethod =
            typeof(IRuntimeHostIdentityStore).GetMethod(
                nameof(IRuntimeHostIdentityStore.CreateIfMissingAsync));

        Assert.NotNull(
            createMethod);

        Assert.Equal(
            typeof(Task<RuntimeHostIdentityStoreCreateResult>),
            createMethod.ReturnType);

        var createParameters =
            createMethod.GetParameters();

        Assert.Equal(
            2,
            createParameters.Length);

        Assert.Equal(
            typeof(RuntimeHostId),
            createParameters[0].ParameterType);

        Assert.Equal(
            typeof(CancellationToken),
            createParameters[1].ParameterType);

        Assert.True(
            createParameters[1].IsOptional);
    }

    [Fact]
    public void Resolver_ExposesConfiguredIdentityAndCancellation()
    {
        var method =
            typeof(IRuntimeHostIdentityResolver).GetMethod(
                nameof(IRuntimeHostIdentityResolver.ResolveAsync));

        Assert.NotNull(
            method);

        Assert.Equal(
            typeof(Task<RuntimeHostIdentityResolution>),
            method.ReturnType);

        var parameters =
            method.GetParameters();

        Assert.Equal(
            2,
            parameters.Length);

        Assert.Equal(
            typeof(RuntimeHostId),
            parameters[0].ParameterType);

        Assert.True(
            parameters[0].IsOptional);

        Assert.Null(
            parameters[0].DefaultValue);

        Assert.Equal(
            typeof(CancellationToken),
            parameters[1].ParameterType);

        Assert.True(
            parameters[1].IsOptional);
    }
}