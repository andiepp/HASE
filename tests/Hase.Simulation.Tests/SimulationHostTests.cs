namespace Hase.Simulation.Tests;

public sealed class SimulationHostTests
{
    [Fact]
    public void Constructor_ZeroInterval_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SimulationHost(TimeSpan.Zero));
    }

    [Fact]
    public void Add_NullSimulation_Throws()
    {
        var host =
            new SimulationHost(
                TimeSpan.FromMilliseconds(10));

        Assert.Throws<ArgumentNullException>(
            () => host.Add(null!));
    }

    [Fact]
    public async Task RunAsync_UpdatesRegisteredSimulation()
    {
        var host =
            new SimulationHost(
                TimeSpan.FromMilliseconds(10));

        var simulation =
            new RecordingSimulation();

        host.Add(simulation);

        using var cancellation =
            new CancellationTokenSource(
                TimeSpan.FromMilliseconds(100));

        await host.RunAsync(cancellation.Token);

        Assert.True(simulation.UpdateCount > 0);
        Assert.True(
            simulation.LastStep.SimulationTime >
            TimeSpan.Zero);
    }

    [Fact]
    public async Task RunAsync_CancellationStopsHost()
    {
        var host =
            new SimulationHost(
                TimeSpan.FromMilliseconds(10));

        using var cancellation =
            new CancellationTokenSource(
                TimeSpan.FromMilliseconds(50));

        await host.RunAsync(cancellation.Token);

        Assert.False(host.IsRunning);
    }

    [Fact]
    public async Task Add_WhileRunning_Throws()
    {
        var host =
            new SimulationHost(
                TimeSpan.FromMilliseconds(10));

        using var cancellation =
            new CancellationTokenSource();

        var runTask =
            host.RunAsync(cancellation.Token);

        await WaitUntilAsync(
            () => host.IsRunning,
            TimeSpan.FromSeconds(1));

        Assert.Throws<InvalidOperationException>(
            () => host.Add(new RecordingSimulation()));

        cancellation.Cancel();
        await runTask;
    }

    private static async Task WaitUntilAsync(
        Func<bool> condition,
        TimeSpan timeout)
    {
        var started = DateTime.UtcNow;

        while (!condition())
        {
            if (DateTime.UtcNow - started > timeout)
            {
                throw new TimeoutException();
            }

            await Task.Delay(1);
        }
    }

    private sealed class RecordingSimulation
        : ISimulation
    {
        public int UpdateCount { get; private set; }

        public SimulationStep LastStep { get; private set; }

        public void Update(SimulationStep step)
        {
            UpdateCount++;
            LastStep = step;
        }
    }
}
