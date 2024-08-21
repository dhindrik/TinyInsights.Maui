namespace TinyInsights;

public interface IInsights
{
    void AddProvider(IInsightsProvider provider);

    void AddGlobalProperty(string key, string value);

    Task TrackErrorAsync(Exception ex, Dictionary<string, string>? properties = null);

    Task TrackPageViewAsync(string viewName, Dictionary<string, string>? properties = null);

    Task TrackEventAsync(string eventName, Dictionary<string, string>? properties = null);

    Task TrackDependencyAsync(string dependencyType, string dependencyName, string data, DateTimeOffset startTime, TimeSpan duration, bool success, int resultCode = 0, Exception? exception = null);
    Task TrackDependencyAsync(string dependencyType, string dependencyName, string data, HttpMethod? httpMethod, DateTimeOffset startTime, TimeSpan duration,
       bool success, int resultCode = 0, Exception? exception = null);
    Dependency CreateDependencyTracker(string dependencyType, string dependencyName, string data);
    void OverrideAnonymousUserId(string userId);
    void GenerateNewAnonymousUserId();
}