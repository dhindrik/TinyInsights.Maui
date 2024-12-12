namespace TinyInsights;

public interface IInsights
{
    void AddProvider(IInsightsProvider provider);
    IReadOnlyList<IInsightsProvider> GetProviders();

    void UpsertGlobalProperty(string key, string value);
    void RemoveGlobalProperty(string key);

    Task TrackErrorAsync(Exception ex, Dictionary<string, string>? properties = null);

    Task TrackPageViewAsync(string viewName, Dictionary<string, string>? properties = null, TimeSpan? duration = null);

    Task TrackEventAsync(string eventName, Dictionary<string, string>? properties = null);
    Task TrackErrorAsync(Exception ex, ErrorSeverity severity, Dictionary<string, string>? properties = null);

    Task TrackDependencyAsync(string dependencyType, string dependencyName, string data, DateTimeOffset startTime, TimeSpan duration, bool success, int resultCode = 0, Exception? exception = null);

    Task TrackDependencyAsync(string dependencyType, string dependencyName, string data, HttpMethod? httpMethod, DateTimeOffset startTime, TimeSpan duration,
       bool success, int resultCode = 0, Exception? exception = null);

    Dependency CreateDependencyTracker(string dependencyType, string dependencyName, string data);

    void OverrideAnonymousUserId(string userId);

    void GenerateNewAnonymousUserId();
    void CreateNewSession();
}