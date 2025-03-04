using TinyInsights.CrashHandlers;

namespace TinyInsights;

public interface IInsightsProvider
{
    bool IsTrackErrorsEnabled { get; set; }
    bool IsTrackCrashesEnabled { get; set; }
    bool WriteCrashes { get; set; }
    bool IsTrackPageViewsEnabled { get; set; }
    bool IsAutoTrackPageViewsEnabled { get; set; }
    bool IsTrackEventsEnabled { get; set; }
    bool IsTrackDependencyEnabled { get; set; }

    Func<(string DependencyType, string DependencyName, string Data, DateTimeOffset StartTime, TimeSpan Duration, bool Success, int ResultCode, Exception? Exception), bool>? TrackDependencyFilter { get; set; }

    void Initialize();

    void UpsertGlobalProperty(string key, string value);

    void RemoveGlobalProperty(string key);

    Task TrackErrorAsync(Exception ex, Dictionary<string, string>? properties = null);

    Task TrackPageViewAsync(string viewName, Dictionary<string, string>? properties = null);

    /// <summary>
    /// Track the duration a user spent on a page
    /// </summary>
    /// <param name="pageVisitTime">Duration in milliseconds</param>
    Task TrackPageVisitTime(string pageFullName, string pageDisplayName, double pageVisitTime);

    Task TrackEventAsync(string eventName, Dictionary<string, string>? properties = null);

    Task TrackDependencyAsync(string dependencyType, string dependencyName, string data, HttpMethod? httpMethod, DateTimeOffset startTime, TimeSpan duration, bool success, int resultCode = 0, Exception? exception = null);

    Task TrackDependencyAsync(string dependencyType, string dependencyName, string data, DateTimeOffset startTime, TimeSpan duration, bool success, int resultCode = 0, Exception? exception = null);

    void OverrideAnonymousUserId(string userId);

    string GenerateNewAnonymousUserId();

    void CreateNewSession();

    bool HasCrashed();

    Task SendCrashes();

    void ResetCrashes();
    Task FlushAsync();

    void SetCrashHandler(ICrashHandler customCrashHandler);
}