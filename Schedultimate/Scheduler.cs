using Schedultimate.Enums;
using Schedultimate.Executions;
using System.Collections.Concurrent;

namespace Schedultimate;

public sealed class Scheduler
    : IAsyncDisposable
{
    private const byte DefaultTimerDelay = 100;

    private readonly PeriodicTimer _timer;
    private readonly CancellationTokenSource _cts;

    private ConcurrentQueue<Execution> _tasks = [];

    private bool _disposed;

    /// <summary>
    /// The cancellation token of this instance.
    /// </summary>
    public CancellationToken CancellationToken =>
        _cts.Token;

    /// <param name="timerTriggerDelay">Time between two excution loops.</param>
    /// <param name="toBeLinkedTokens">Cancellation tokens to link with.</param>
    public Scheduler(TimeSpan? timerTriggerDelay = null, params CancellationToken[] toBeLinkedTokens)
    {
        _timer = new PeriodicTimer(timerTriggerDelay is null ?
            TimeSpan.FromMilliseconds(DefaultTimerDelay)
            : timerTriggerDelay.Value
        );

        _cts = toBeLinkedTokens.Length is 0 ?
            new CancellationTokenSource()
            : CancellationTokenSource.CreateLinkedTokenSource(toBeLinkedTokens);
    }

    /// <summary>
    /// Start the <see cref="Scheduler"/>.
    /// </summary>
    /// <returns></returns>
    public async Task StartAsync()
    {
        try
        {
            while (await _timer.WaitForNextTickAsync(CancellationToken))
            {
                var now = DateTime.Now;

                if (!_tasks.IsEmpty)
                {
                    var nextCollection = new ConcurrentQueue<Execution>();

                    while (_tasks.TryDequeue(out var execution))
                    {
                        var available = execution.IsAvailable(now);

                        if (available)
                            _ = execution.ExecuteAsync(now, CancellationToken);

                        if (execution.State is not ExecutionState.Cancelled && (!execution.IsDelayed || !available))
                            nextCollection.Enqueue(execution);
                        else
                            execution.Dispose();
                    }

                    _tasks = nextCollection;
                }
            }
        }
        finally
        {
            await DisposeAsync();
        }
    }

    /// <summary>
    /// Execute a <see cref="Delegate"/> periodically.
    /// </summary>
    /// <param name="method">The method which will be executed.</param>
    /// <param name="interval">The interval between two calls.</param>
    /// <param name="delay">The delay before the first execution. Otherwise it will be run when it is registered.</param>
    /// <param name="predicate">The predicate to indicate if it can be executed or not.</param>
    /// <param name="days">The days which can allows the execution.</param>
    /// <returns></returns>
    public Execution ExecutePeriodically(Delegate method,
        TimeSpan interval,
        TimeSpan? delay = null,
        Func<bool>? predicate = null,
        params DayOfWeek[]? days)
    {
        var execution = new Execution(method, interval, delay, predicate, days);
        _tasks.Enqueue(execution);

        return execution;
    }

    /// <summary>
    /// Execute a <see cref="Delegate"/> periodically.
    /// </summary>
    /// <param name="method">The method which will be executed.</param>
    /// <param name="interval">The interval between two calls.</param>
    /// <param name="delay">The delay before the first execution. Otherwise it will be run when it is registered.</param>
    /// <param name="predicate">The predicate to indicate if it can be executed or not.</param>
    /// <returns></returns>
    public Execution ExecutePeriodically(Delegate method,
        TimeSpan interval,
        TimeSpan? delay = null,
        Func<bool>? predicate = null) =>
        ExecutePeriodically(method, interval, delay, predicate, null);

    /// <summary>
    /// Execute an async function periodically.
    /// </summary>
    /// <param name="method">The method which will be executed.</param>
    /// <param name="interval">The interval between two calls.</param>
    /// <param name="delay">The delay before the first execution. Otherwise it will be run when it is registered.</param>
    /// <param name="predicate">The predicate to indicate if it can be executed or not.</param>
    /// <param name="days">The days which can allows the execution.</param>
    /// <returns></returns>
    public Execution ExecutePeriodically(Func<Task> method,
        TimeSpan interval,
        TimeSpan? delay = null,
        Func<bool>? predicate = null,
        params DayOfWeek[]? days)
    {
        var execution = new Execution(method, interval, delay, predicate, days);
        _tasks.Enqueue(execution);

        return execution;
    }

    /// <summary>
    /// Execute an async function periodically.
    /// </summary>
    /// <param name="method">The method which will be executed.</param>
    /// <param name="interval">The interval between two calls.</param>
    /// <param name="delay">The delay before the first execution. Otherwise it will be run when it is registered.</param>
    /// <param name="predicate">The predicate to indicate if it can be executed or not.</param>
    /// <returns></returns>
    public Execution ExecutePeriodically(Func<Task> method,
        TimeSpan interval,
        TimeSpan? delay = null,
        Func<bool>? predicate = null) =>
        ExecutePeriodically(method, interval, delay, predicate, null);

    /// <summary>
    /// Execute a <see cref="Delegate"/> with a delay.
    /// </summary>
    /// <param name="method">The method which will be executed.</param>
    /// <param name="delay">The delay before the execution.</param>
    /// <param name="predicate">The predicate to indicate if it can be executed or not.</param>
    /// <returns></returns>
    public Execution ExecuteDelayed(Delegate method,
        TimeSpan delay,
        Func<bool>? predicate = null)
    {
        var execution = new Execution(method, TimeSpan.Zero, delay, predicate, null);
        _tasks.Enqueue(execution);

        return execution;
    }

    /// <summary>
    /// Execute an async function with a delay.
    /// </summary>
    /// <param name="method">The method which will be executed.</param>
    /// <param name="delay">The delay before the execution.</param>
    /// <param name="predicate">The predicate to indicate if it can be executed or not.</param>
    /// <returns></returns>
    public Execution ExecuteDelayed(Func<Task> method,
        TimeSpan delay,
        Func<bool>? predicate = null)
    {
        var execution = new Execution(method, TimeSpan.Zero, delay, predicate, null);
        _tasks.Enqueue(execution);

        return execution;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (!_cts.IsCancellationRequested)
            await _cts.CancelAsync().ConfigureAwait(false);

        foreach (var execution in _tasks)
            execution.Dispose();

        _tasks.Clear();

        _timer.Dispose();
        _cts.Dispose();
    }
}
