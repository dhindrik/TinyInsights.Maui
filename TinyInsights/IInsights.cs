namespace TinyInsights;

public interface IInsights
{
    void AddProvider(IInsightsProvider provider);

    IReadOnlyList<IInsightsProvider> GetProviders();

    void UpsertGlobalProperty(string key, string value);

    void RemoveGlobalProperty(string key);

    Task TrackErrorAsync(Exception ex, Dictionary<string, string>? properties = null);

    Task TrackPageViewAsync(string viewName, Dictionary<string, string>? properties = null);

    Task TrackPageVisitTime(string pageFullName, string pageDisplayName, double pageVisitTime);

    Task TrackEventAsync(string eventName, Dictionary<string, string>? properties = null);

    Task TrackErrorAsync(Exception ex, ErrorSeverity severity, Dictionary<string, string>? properties = null);

    Task TrackDependencyAsync(string dependencyType, string dependencyName, string data, DateTimeOffset startTime, TimeSpan duration, bool success, int resultCode = 0, Exception? exception = null);

    Task TrackDependencyAsync(string dependencyType, string dependencyName, string data, HttpMethod? httpMethod, DateTimeOffset startTime, TimeSpan duration,
       bool success, int resultCode = 0, Exception? exception = null);

    Dependency CreateDependencyTracker(string dependencyType, string dependencyName, string data);

    void OverrideAnonymousUserId(string userId);

    void GenerateNewAnonymousUserId();

    void CreateNewSession();

    bool HasCrashed();

    Task SendCrashes();

    void ResetCrashes();

    Task FlushAsync();

    /// <summary>
    /// Indicates whether all providers have been successfully initialized.
    /// </summary>
    bool AreAllProvidersInitialized { get; }

    /// <summary>
    /// Connects to Application Insights using the provided connection string.
    /// Applied only to providers not yet initialized.
    /// </summary>
    /// <param name="applicationInsightsConnectionString">Connection string to initialize providers with</param>
    /// <returns>True if all providers are initialized once operation is executed</returns>
    bool Connect(string applicationInsightsConnectionString);
}