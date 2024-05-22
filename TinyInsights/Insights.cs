namespace TinyInsights;

public class Insights : IInsights
{
    private readonly List<IInsightsProvider> insightsProviders = new();

    public void AddProvider(IInsightsProvider provider)
    {
        insightsProviders.Add(provider);
    }
    
    public Task TrackErrorAsync(Exception ex, Dictionary<string, string>? properties = null)
    {
        var tasks = new List<Task>();

        foreach (var provider in insightsProviders.Where(x => x.IsTrackErrorsEnabled))
        {
            var task = provider.TrackErrorAsync(ex, properties);
            tasks.Add(task);
        }

        _ = Task.WhenAll(tasks);

        return Task.CompletedTask;
    }

    public Task TrackPageViewAsync(string viewName, Dictionary<string, string>? properties = null)
    {
        var tasks = new List<Task>();

        foreach (var provider in insightsProviders.Where(x => x.IsTrackPageViewsEnabled))
        {
            var task = provider.TrackPageViewAsync(viewName, properties);
            tasks.Add(task);
        }

        _ = Task.WhenAll(tasks);
        return Task.CompletedTask;
    }

    public Task TrackEventAsync(string eventName, Dictionary<string, string>? properties = null)
    {
        var tasks = new List<Task>();

        foreach (var provider in insightsProviders.Where(x => x.IsTrackEventsEnabled))
        {
            var task = provider.TrackEventAsync(eventName, properties);
            tasks.Add(task);
        }

        _ = Task.WhenAll(tasks);
        
        return Task.CompletedTask;
    }

    public Task TrackDependencyAsync(string dependencyType, string dependencyName, string data, DateTimeOffset startTime, TimeSpan duration,
        bool success, int resultCode = 0, Exception? exception = null)
    {
        var tasks = new List<Task>();

        foreach (var provider in insightsProviders.Where(x => x.IsTrackDependencyEnabled))
        {
            var task = provider.TrackDependencyAsync(dependencyType, dependencyName, data,startTime, duration, success, resultCode, exception);
            tasks.Add(task);
        }

        _ =  Task.WhenAll(tasks);
        
        return Task.CompletedTask;
    }
    
    public Dependency CreateDependencyTracker(string dependencyType, string dependencyName, string data)
    {
        var dependency = new Dependency(this)
        {
            DependencyType = dependencyType,
            DependencyName = dependencyName,
            Data = data
        };

        return dependency;
    }
}