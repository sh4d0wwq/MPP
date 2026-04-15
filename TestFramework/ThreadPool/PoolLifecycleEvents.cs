namespace TestFramework.ThreadPool;

public sealed class PoolCreatedEventArgs : EventArgs
{
    public int MinThreads { get; init; }
    public int MaxThreads { get; init; }
}

public sealed class WorkerLifecycleEventArgs : EventArgs
{
    public string WorkerName { get; init; } = "";
    public int ActiveWorkerCount { get; init; }
}

public sealed class TaskEnqueuedEventArgs : EventArgs
{
    public string TaskName { get; init; } = "";
    public int QueueLength { get; init; }
}

public sealed class TaskLifecycleEventArgs : EventArgs
{
    public string TaskName { get; init; } = "";
    public string WorkerName { get; init; } = "";
    public bool Success { get; init; }
    public Exception? Error { get; init; }
}

public sealed class PoolScalingEventArgs : EventArgs
{
    public string Reason { get; init; } = "";
    public int NewWorkerCount { get; init; }
}

public sealed class PoolDisposingEventArgs : EventArgs
{
    public int CompletedTasks { get; init; }
    public int FailedTasks { get; init; }
}
