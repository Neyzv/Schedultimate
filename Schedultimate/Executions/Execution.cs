using Schedultimate.Enums;

namespace Schedultimate.Executions;

public sealed class Execution
    : IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1);
    private readonly Func<Task>? _asyncExecution;
    private readonly Delegate? _syncExecution;

    private TimeSpan _interval;
    private Func<bool>? _predicate;
    private HashSet<DayOfWeek> _days = [];
    private DateTime _lastTickDate;

    private bool _disposed;

    /// <summary>
    /// The current state of the execution.
    /// </summary>
    public ExecutionState State { get; private set; }

    /// <summary>
    /// Determins if it's a delayed or a periodically execution.
    /// </summary>
    internal bool IsDelayed =>
        _interval.TotalMilliseconds is 0;

    private Execution(TimeSpan interval, TimeSpan? delay, Func<bool>? predicate, DayOfWeek[]? days)
    {
        _interval = interval;
        _lastTickDate = DateTime.Now - interval + (delay is null ? TimeSpan.Zero : delay.Value);
        _predicate = predicate;
        _days = days is null ? [] : [.. days];
    }

    internal Execution(Delegate syncExecution,
        TimeSpan interval,
        TimeSpan? delay,
        Func<bool>? predicate,
        DayOfWeek[]? days)
        : this(interval, delay, predicate, days) =>
        _syncExecution = syncExecution;

    internal Execution(Func<Task> asyncExecution,
        TimeSpan interval,
        TimeSpan? delay,
        Func<bool>? predicate,
        DayOfWeek[]? days)
        : this(interval, delay, predicate, days) =>
        _asyncExecution = asyncExecution;

    /// <summary>
    /// Determins if the curent date and time allows the execution.
    /// </summary>
    /// <param name="startLoopDate">The date and time of the beginning of the execution loop.</param>
    /// <returns></returns>
    internal bool IsAvailable(DateTime startLoopDate)
    {
        _semaphore.Wait();

        try
        {
            return _lastTickDate + _interval <= startLoopDate
                && (_days.Count is 0 || _days.Contains(startLoopDate.DayOfWeek));
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Execute the inner function if possible.
    /// </summary>
    /// <param name="startLoopDate">The date and time of the beginning of the execution loop.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    internal async Task ExecuteAsync(DateTime startLoopDate, CancellationToken ct)
    {
        if (ct.IsCancellationRequested || State is not ExecutionState.Running)
            return;

        await _semaphore.WaitAsync(ct);

        try
        {
            if (_predicate is not null && !_predicate())
                return;

            _lastTickDate = startLoopDate;

            if (_asyncExecution is not null)
                await _asyncExecution();
            else if (_syncExecution is not null)
                _syncExecution.DynamicInvoke();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Change the execution interval.
    /// </summary>
    /// <param name="interval">The new interval.</param>
    /// <returns></returns>
    public Execution ChangeInterval(TimeSpan interval)
    {
        _semaphore.Wait();
        _interval = interval;
        _semaphore.Release();

        return this;
    }

    /// <summary>
    /// Change the predicate to allows the execution.
    /// </summary>
    /// <param name="predicate">The new condition.</param>
    /// <returns></returns>
    public Execution ChangePredicate(Func<bool> predicate)
    {
        _semaphore.Wait();
        _predicate = predicate;
        _semaphore.Release();

        return this;
    }

    /// <summary>
    /// Change the execution days of the execution. 
    /// </summary>
    /// <param name="days">The days which can allows the execution.</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">Thrown if it's a delayed, not a periodically executed execution.</exception>
    public Execution ChangeDays(params DayOfWeek[] days)
    {
        if (IsDelayed)
            throw new InvalidOperationException();

        _semaphore.Wait();
        _days = [.. days];
        _semaphore.Release();

        return this;
    }

    /// <summary>
    /// Change the state of the execution.
    /// </summary>
    /// <param name="state">The new state to apply.</param>
    /// <returns></returns>
    public Execution ChangeState(ExecutionState state)
    {
        _semaphore.Wait();
        State = state;
        _semaphore.Release();

        return this;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        ChangeState(ExecutionState.Cancelled);
        _semaphore.Dispose();
    }
}
