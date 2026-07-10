using System.Diagnostics;

namespace Hase.Simulation;

/// <summary>
/// Advances registered simulations using a periodic update loop.
/// </summary>
public sealed class SimulationHost
{
    private readonly List<ISimulation> _simulations = [];
    private bool _isRunning;

    public SimulationHost(TimeSpan updateInterval)
    {
        if (updateInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(updateInterval),
                "The update interval must be greater than zero.");
        }

        UpdateInterval = updateInterval;
    }

    public TimeSpan UpdateInterval { get; }

    public bool IsRunning => _isRunning;

    public void Add(ISimulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);

        if (_isRunning)
        {
            throw new InvalidOperationException(
                "Simulations cannot be added while the host is running.");
        }

        _simulations.Add(simulation);
    }

    public async Task RunAsync(
        CancellationToken cancellationToken = default)
    {
        if (_isRunning)
        {
            throw new InvalidOperationException(
                "The simulation host is already running.");
        }

        _isRunning = true;

        try
        {
            using var timer =
                new PeriodicTimer(UpdateInterval);

            var stopwatch = Stopwatch.StartNew();
            var previousTime = stopwatch.Elapsed;
            var simulationTime = TimeSpan.Zero;

            while (await timer.WaitForNextTickAsync(
                       cancellationToken))
            {
                var currentTime = stopwatch.Elapsed;
                var elapsed = currentTime - previousTime;

                previousTime = currentTime;
                simulationTime += elapsed;

                var step = new SimulationStep(
                    elapsed,
                    simulationTime);

                foreach (var simulation in _simulations)
                {
                    simulation.Update(step);
                }
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // Normal host shutdown.
        }
        finally
        {
            _isRunning = false;
        }
    }
}