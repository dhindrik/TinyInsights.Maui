namespace TinyInsights.Web.Services;

public interface IInsightsService
{
    Task<bool> AddAndValidateApiKey(string appId, string apiKey);
    Task<(bool Succeed, string? ErrorMessage)> AddAndValidateBearer(string appId, string token);

    Task<List<string>> GetUniqueAppVersions();

    #region Diagnostics
    Task<List<CountPerDay>> GetCrashesPerDay(GlobalFilter filter);
    Task<List<CountPerDay>> GetErrorsPerDay(GlobalFilter filter);
    Task<List<CountPerKey>> GetCrashesGrouped(GlobalFilter filter);
    Task<List<CountPerKey>> GetErrorsGrouped(GlobalFilter filter);
    Task<ErrorDetails> GetCrashDetails(string id, GlobalFilter filter);
    Task<ErrorDetails> GetErrorDetails(string id, GlobalFilter filter);
    Task<List<EventItem>> GetEventsByUser(string userId, DateTime timestamp);
    Task<List<EventItem>> GetEventsByUser(string userId, GlobalFilter filter);
    Task<List<AvgPerKey>> GetDependencyAvgDurations(GlobalFilter filter);
    Task<List<CountPerKey>> GetTopDependencies(GlobalFilter filter);
    Task<List<CountPerKey>> GetFailedDependencies(GlobalFilter filter, List<string>? resultCodeFilter = null);
    Task<FailedDependencies> GetFailedDependencies(string key, GlobalFilter filter);
    Task<List<string>> GetFailedDependenciesStatusCodes(GlobalFilter filter);
    Task<List<CountPerDay>> GetFailedDependenciesPerDay(GlobalFilter filter);
    #endregion

    #region Analytics

    Task<List<CountPerDay>> GetPageViewsPerDay(GlobalFilter filter);
    Task<List<CountPerKey>> GetPageViewsGrouped(GlobalFilter filter);
    Task<List<CountPerKey>> GetEventsGrouped(GlobalFilter filter);
    Task<List<CountPerDay>> GetEventsPerDay(string eventName, GlobalFilter filter);
    Task<List<CountPerDay>> GetUsersPerDay(GlobalFilter filter);
    Task<List<CountPerKey>> GetUserPerCountry(GlobalFilter filter);
    Task<List<CountPerKey>> GetUserPerLanguage(GlobalFilter filter);
    Task<List<CountPerKey>> GetUsersPerIdiom(GlobalFilter filter);
    Task<List<CountPerKey>> GetUsersPerOperatingSystem(GlobalFilter filter);
    Task<List<CountPerKey>> GetUserPerManufacturer(GlobalFilter filter);
    Task<List<CountPerDay>> GetSessionsPerDay(GlobalFilter filter);
    Task<List<CountPerKey>> GetUsersPerOperatingSystemVersion(GlobalFilter filter);
    #endregion

}