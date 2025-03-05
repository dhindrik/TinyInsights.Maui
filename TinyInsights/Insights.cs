namespace TinyInsights;

public class Insights : IInsights
{
    private readonly List<IInsightsProvider> insightsProviders = [];

    public void UpsertGlobalProperty(string key, string value)
    {
        foreach (var provider in insightsProviders)
        {
            provider.UpsertGlobalProperty(key, value);
        }
    }

    public void RemoveGlobalProperty(string key)
    {
        foreach (var provider in insightsProviders)
        {
            provider.RemoveGlobalProperty(key);
        }
    }

    public void AddProvider(IInsightsProvider provider)
    {
        insightsProviders.Add(provider);
    }

    public IReadOnlyList<IInsightsProvider> GetProviders()
    {
        return insightsProviders;
    }

    public Task TrackErrorAsync(Exception ex, Dictionary<string, string>? properties = null)
    {
        return TrackErrorAsync(ex, ErrorSeverity.Default, properties);
    }

    public Task TrackErrorAsync(Exception ex, ErrorSeverity severity, Dictionary<string, string>? properties = null)
    {
        var tasks = new List<Task>();

        if (properties == null)
        {
            properties = new Dictionary<string, string>();
        }

        properties.TryAdd(nameof(ErrorSeverity), severity.ToString());

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

    public Task TrackPageVisitTime(string pageFullName, string pageDisplayName, double pageVisitTime)
    {
        var tasks = new List<Task>();

        foreach (var provider in insightsProviders.Where(x => x.IsTrackPageViewsEnabled))
        {
            var task = provider.TrackPageVisitTime(pageFullName, pageDisplayName, pageVisitTime);
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
        return TrackDependencyAsync(dependencyType, dependencyName, data, null, startTime, duration, success, resultCode, exception);
    }

    public Task TrackDependencyAsync(string dependencyType, string dependencyName, string data, HttpMethod? httpMethod, DateTimeOffset startTime, TimeSpan duration,
       bool success, int resultCode = 0, Exception? exception = null)
    {
        var tasks = new List<Task>();

        foreach (var provider in insightsProviders.Where(x => x.IsTrackDependencyEnabled))
        {
            if (provider.TrackDependencyFilter is not null && !provider.TrackDependencyFilter.Invoke((dependencyType, dependencyName, data, startTime, duration, success, resultCode, exception)))
            {
                continue;
            }

            var task = provider.TrackDependencyAsync(dependencyType, dependencyName, data, httpMethod, startTime, duration, success, resultCode, exception);
            tasks.Add(task);
        }

        _ = Task.WhenAll(tasks);

        return Task.CompletedTask;
    }

    public Dependency CreateDependencyTracker(string dependencyType, string dependencyName, string data)
    {
        return new Dependency(this)
        {
            DependencyType = dependencyType,
            DependencyName = dependencyName,
            Data = data
        };
    }

    public Dependency CreateDependencyTracker(string dependencyType, string dependencyName, string data, HttpMethod httpMethod)
    {
        return new Dependency(this)
        {
            DependencyType = dependencyType,
            DependencyName = dependencyName,
            Data = data,
            HttpMethod = httpMethod
        };
    }

    public void OverrideAnonymousUserId(string userId)
    {
        foreach (var provider in insightsProviders)
        {
            provider.OverrideAnonymousUserId(userId);
        }
    }

    public void GenerateNewAnonymousUserId()
    {
        foreach (var provider in insightsProviders)
        {
            provider.GenerateNewAnonymousUserId();
        }
    }

    public void CreateNewSession()
    {
        foreach (var provider in insightsProviders)
        {
            provider.CreateNewSession();
        }
    }

    public bool HasCrashed()
    {
        return insightsProviders.Any(provider => provider.HasCrashed());
    }

    public async Task SendCrashes()
    {
        foreach (var provider in insightsProviders)
        {
            await provider.SendCrashes();
        }
    }

    public void ResetCrashes()
    {
        foreach (var provider in insightsProviders)
        {
            provider.ResetCrashes();
        }
    }

    public async Task FlushAsync()
    {
        foreach (var provider in insightsProviders)
        {
            await provider.FlushAsync();
        }
    }
}