# Schedultimate
Schedultimate is a Task Scheduler, that allows to create periodical tasks and also delayed tasks. Everything is manually configurable. \
You can also delay a task or execute it only if a predicate is verified. You can also change task parameters while it is running.

# How to use
To begin you'll need to create an instance of the `Scheduler`
```cs
using Schedultimate;

await using var scheduler = new Scheduler();
```
From now you can use scheduler's methods to create tasks.
