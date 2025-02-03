using Schedultimate;
using Schedultimate.Enums;

async Task WaitAndPrint(string content)
{
    await Task.Delay(1000).ConfigureAwait(false);
    Console.WriteLine(content);
}

await using var scheduler = new Scheduler();
scheduler.ExecutePeriodically(static () => Console.WriteLine("Hello"),
    TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2));

Console.WriteLine("Scheduler starting...");
_ = scheduler.StartAsync();

var execution = scheduler.ExecutePeriodically(static () => Console.WriteLine("Hi"),
    TimeSpan.FromSeconds(3));

var delayedExecution = scheduler.ExecuteDelayed(static () => Console.WriteLine("I will be executed once but not now..."),
    TimeSpan.FromSeconds(4));

try
{
    delayedExecution.ChangeDays(DayOfWeek.Monday);
}
catch (InvalidOperationException)
{
    Console.WriteLine("You can't specify days for a delayed execution.");
}

await Task.Delay(4000);
execution.ChangeInterval(TimeSpan.FromSeconds(5));

scheduler.ExecutePeriodically(() => WaitAndPrint("Hi from async context"),
    TimeSpan.FromSeconds(3));

scheduler.ExecuteDelayed(() => WaitAndPrint("I will be executed once but not now from async context..."),
    TimeSpan.FromSeconds(4));

execution.ChangeState(ExecutionState.Paused);
await Task.Delay(5000);
execution.ChangeState(ExecutionState.Running);
await Task.Delay(3000);
execution.ChangeState(ExecutionState.Cancelled); // Will be disposed
// or
execution.Dispose();

Console.ReadLine();