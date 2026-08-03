namespace Hase.Scpi.Tests;

public sealed class ScpiTextSessionContractTests
{
    [Fact]
    public void SessionContract_IsAsynchronouslyDisposable()
    {
        Assert.True(typeof(IAsyncDisposable).IsAssignableFrom(typeof(IScpiTextSession)));
    }

    [Fact]
    public void SessionStateValues_AreStable()
    {
        Assert.Equal(0, (int)ScpiTextSessionState.Open);
        Assert.Equal(1, (int)ScpiTextSessionState.Faulted);
        Assert.Equal(2, (int)ScpiTextSessionState.Disposed);
    }
}
