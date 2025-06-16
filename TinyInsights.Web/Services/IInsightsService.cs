namespace TinyInsights.Web.Services;

public interface IInsightsService
{
    Task<bool> AddAndValidateApiKey(string appId, string apiKey, CancellationToken cancellationToken = default);
    Task<(bool Succeed, string? ErrorMessage)> AddAndValidateBearer(string appId, string token, CancellationToken cancellationToken = default);

    Task<List<string>> GetUniqueAppVersions();

    #region Diagnostics
    Task<List<CountPerDay>> GetCrashesPerDay(GlobalFilter filter, CancellationToken cancellationToken = default);
    Task<List<CountPerDay>> GetErrorsPerDay(GlobalFilter filter, List<string>? errorSeverities = null, CancellationToken cancellationToken = default);
    Task<List<CrashItem>> GetCrashesGrouped(GlobalFilter filter, CancellationToken cancellationToken = default);
    Task<List<ErrorCount>> GetErrorsGrouped(GlobalFilter filter, List<string>? errorSeverities = null, CancellationToken cancellationToken = default);
    Task<ErrorDetails> GetCrashDetails(string id, GlobalFilter filter, CancellationToken cancellationToken = default);
    Task<ErrorDetails> GetErrorDetails(string id, GlobalFilter filter, string? severity = null, CancellationToken cancellationToken = default);
    Task<List<EventItem>> GetEventsByUser(string userId, DateTime timestamp, CancellationToken cancellationToken = default);
    Task<List<EventItem>> GetEventsByUser(string userId, GlobalFilter filter, CancellationToken cancellationToken = default);
    Task<List<AvgPerKey>> GetDependencyAvgDurations(GlobalFilter filter, CancellationToken cancellationToken = default);
    Task<List<DependencyCount>> GetTopDependencies(GlobalFilter filter, CancellationToken cancellationToken = default);
    Task<List<DependencyCount>> GetFailedDependencies(GlobalFilter filter, List<string>? resultCodeFilter = null, CancellationToken cancellationToken = default);
    Task<FailedDependencies> GetFailedDependencies(string key, string method, GlobalFilter filter, CancellationToken cancellationToken = default);
    Task<List<string>> GetFailedDependenciesStatusCodes(GlobalFilter filter, CancellationToken cancellationToken = default);
    Task<List<CountPerDay>> GetFailedDependenciesPerDay(GlobalFilter filter, CancellationToken cancellationToken = default);
    Task<List<CountPerDay>> GetCrashDetailsPerDay(string problemId, GlobalFilter filter, CancellationToken cancellationToken = default);
    Task<List<CountPerDay>> GetErrorDetailsPerDay(string problemId, GlobalFilter filter, string? severity = null, CancellationToken cancellationToken = default);
    #endregion

    #region Analytics

    Task<List<CountPerDay>> GetPageViewsPerDay(GlobalFilter filter, CancellationToken cancellationToken = default);
    Task<List<CountPerKey>> GetPageViewsGrouped(GlobalFilter filter, CancellationToken cancellationToken = default);
    Task<List<CountPerKey>> GetEventsGrouped(GlobalFilter filter, CancellationToken cancellationToken = default);
    Task<List<CountPerDay>> GetEventsPerDay(string eventName, GlobalFilter filter, CancellationToken cancellationToken = default);
    Task<List<CountPerDay>> GetUsersPerDay(GlobalFilter filter, CancellationToken cancellationToken = default);
    Task<List<CountPerKey>> GetUserPerCountry(GlobalFilter filter, CancellationToken cancellationToken = default);
    Task<List<CountPerKey>> GetUserPerLanguage(GlobalFilter filter, CancellationToken cancellationToken = default);
    Task<List<CountPerKey>> GetUsersPerIdiom(GlobalFilter filter, CancellationToken cancellationToken = default);
    Task<List<CountPerKey>> GetUsersPerOperatingSystem(GlobalFilter filter, CancellationToken cancellationToken = default);
    Task<List<CountPerKey>> GetUserPerManufacturer(GlobalFilter filter, CancellationToken cancellationToken = default);
    Task<List<CountPerDay>> GetSessionsPerDay(GlobalFilter filter, CancellationToken cancellationToken = default);
    Task<List<CountPerKey>> GetUsersPerOperatingSystemVersion(GlobalFilter filter, CancellationToken cancellationToken = default);
    Task<List<CountPerKey>> GetUsersPerAppVersion(GlobalFilter filter, CancellationToken cancellationToken = default);
    #endregion

}