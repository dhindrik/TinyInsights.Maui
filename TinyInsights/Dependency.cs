namespace TinyInsights;

public class Dependency : IDisposable
{
    private readonly IInsights insights;

    internal Dependency(IInsights insights)
    {
        this.insights = insights;
        StartTime = DateTimeOffset.Now;
    }

    public string DependencyType { get; internal set; } = string.Empty;
    public string DependencyName { get; internal set; } = string.Empty;
    public string Data { get; internal set; } = string.Empty;
    public DateTimeOffset StartTime { get; private set; }
    public TimeSpan Duration { get; private set; }
    public HttpMethod? HttpMethod { get; internal set; }

    private readonly SemaphoreSlim semaphore = new(1, 1);

    private bool isFinished;

    public void Dispose()
    {
        Task.Run(() => Finish(true));
    }

    public Task Finish(bool success, int resultCode = 0)
    {
        return Finish(success, resultCode, null);
    }

    public Task Finish(bool success, Exception exception)
    {
        return Finish(success, 0, exception);
    }

    public async Task Finish(bool success, int resultCode, Exception? exception = null)
    {
        await semaphore.WaitAsync();

        if (!isFinished)
        {
            Duration = DateTimeOffset.Now - StartTime;

            await insights.TrackDependencyAsync(DependencyType, DependencyName, Data, HttpMethod, StartTime, Duration, success, resultCode, exception);

            isFinished = true;

            semaphore.Release();
        }
    }
}