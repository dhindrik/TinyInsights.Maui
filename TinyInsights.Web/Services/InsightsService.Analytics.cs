namespace TinyInsights.Web.Services;

public partial class InsightsService
{
    public Task<List<CountPerDay>> GetPageViewsPerDay(GlobalFilter filter, CancellationToken cancellationToken = default)
    {
        var queryFilter = GetFilter(filter);

        var query = $"pageViews | where{queryFilter} timestamp > ago({filter.NumberOfDays}d) | summarize count_sum = sum(itemCount) by bin(timestamp,1d)";

        return GetPerDayResult(query, cancellationToken);
    }

    public Task<List<CountPerKey>> GetPageViewsGrouped(GlobalFilter filter, CancellationToken cancellationToken = default)
    {
        var queryFilter = GetFilter(filter);

        var query = $"pageViews | where{queryFilter} timestamp > ago({filter.NumberOfDays}d) | summarize count_sum = sum(itemCount) by name";

        return GetPerKeyResult(query, cancellationToken);
    }

    public Task<List<CountPerKey>> GetEventsGrouped(GlobalFilter filter, CancellationToken cancellationToken = default)
    {
        var queryFilter = GetFilter(filter);

        var query = $"customEvents | where{queryFilter} timestamp > ago({filter.NumberOfDays}d) | summarize count_sum = sum(itemCount) by name";

        return GetPerKeyResult(query, cancellationToken);
    }

    public Task<List<CountPerDay>> GetEventsPerDay(string eventName, GlobalFilter filter, CancellationToken cancellationToken = default)
    {
        var queryFilter = GetFilter(filter);
        var query = $"customEvents | where{queryFilter} name == '{eventName}' and timestamp > ago({filter.NumberOfDays}d) | summarize count_sum = sum(itemCount) by bin(timestamp,1d)";

        return GetPerDayResult(query, cancellationToken);
    }

    public Task<List<CountPerDay>> GetUsersPerDay(GlobalFilter filter, CancellationToken cancellationToken = default)
    {
        var queryFilter = GetFilter(filter);
        var query = $"pageViews | where{queryFilter} timestamp > ago({filter.NumberOfDays}d) | summarize uniqueUsers = dcount(user_Id) by bin(timestamp, 1d)";

        return GetPerDayResult(query, cancellationToken);
    }

    public Task<List<CountPerDay>> GetSessionsPerDay(GlobalFilter filter, CancellationToken cancellationToken = default)
    {
        var queryFilter = GetFilter(filter);
        var query = $"pageViews | where{queryFilter} isnotnull(session_Id) and timestamp > ago({filter.NumberOfDays}d) | summarize uniqueSessions = dcount(session_Id) by bin(timestamp, 1d)";

        return GetPerDayResult(query, cancellationToken);
    }

    public Task<List<CountPerKey>> GetUserPerCountry(GlobalFilter filter, CancellationToken cancellationToken = default)
    {
        var queryFilter = GetFilter(filter);
        var query = $"pageViews | where{queryFilter} timestamp > ago({filter.NumberOfDays}d) | summarize PageViewsCount = dcount(user_Id) by client_CountryOrRegion";

        return GetPerKeyResult(query, cancellationToken);
    }

    public Task<List<CountPerKey>> GetUserPerLanguage(GlobalFilter filter, CancellationToken cancellationToken = default)
    {
        var queryFilter = GetFilter(filter);
        var query = $"pageViews | extend language = tostring(customDimensions.Language) | where{queryFilter} timestamp > ago({filter.NumberOfDays}d) | summarize arg_max(timestamp, *) by user_Id | summarize PageViewsCount = dcount(user_Id) by language";

        return GetPerKeyResult(query, cancellationToken);
    }

    public Task<List<CountPerKey>> GetUsersPerAppVersion(GlobalFilter filter, CancellationToken cancellationToken = default)
    {
        var queryFilter = GetFilter(filter);
        var query = $"pageViews | extend appVersion = tostring(customDimensions.AppVersion) | where{queryFilter} timestamp > ago({filter.NumberOfDays}d) | summarize arg_max(timestamp, *) by user_Id | summarize PageViewsCount = dcount(user_Id) by appVersion";

        return GetPerKeyResult(query, cancellationToken);
    }

    public Task<List<CountPerKey>> GetUsersPerIdiom(GlobalFilter filter, CancellationToken cancellationToken = default)
    {

        var queryFilter = GetFilter(filter);
        var query = $"pageViews | where{queryFilter} timestamp > ago({filter.NumberOfDays}d) | summarize PageViewsCount = dcount(user_Id) by client_Type";

        return GetPerKeyResult(query, cancellationToken);
    }

    public Task<List<CountPerKey>> GetUsersPerOperatingSystem(GlobalFilter filter, CancellationToken cancellationToken = default)
    {

        var queryFilter = GetFilter(filter);
        var query = $"pageViews | where{queryFilter} timestamp > ago({filter.NumberOfDays}d) | summarize PageViewsCount = dcount(user_Id) by client_OS";

        return GetPerKeyResult(query, cancellationToken);
    }

    public Task<List<CountPerKey>> GetUserPerManufacturer(GlobalFilter filter, CancellationToken cancellationToken = default)
    {
        var queryFilter = GetFilter(filter);
        var query = $"pageViews | extend manufacturer = tostring(customDimensions.Manufacturer) | where{queryFilter} timestamp > ago({filter.NumberOfDays}d) | summarize PageViewsCount = dcount(user_Id) by manufacturer";

        return GetPerKeyResult(query, cancellationToken);
    }

    public Task<List<CountPerKey>> GetUsersPerOperatingSystemVersion(GlobalFilter filter, CancellationToken cancellationToken = default)
    {
        var queryFilter = GetFilter(filter);
        var query = $"pageViews | extend os_Version = strcat(client_OS, \" \", tostring(customDimensions.OperatingSystemVersion)) | where{queryFilter} timestamp > ago({filter.NumberOfDays}d) | summarize  arg_max(timestamp, *) by user_Id | summarize viewCount = dcount(user_Id) by os_Version";

        return GetPerKeyResult(query, cancellationToken);
    }

    private async Task<List<CountPerKey>> GetPerKeyResult(string query, CancellationToken cancellationToken)
    {
        var queryResult = await GetQueryResult<QueryResult>(query);

        var result = new List<CountPerKey>();

        foreach (var row in queryResult.Tables.First().Rows)
        {
            result.Add(new CountPerKey(row.First().ToString(), int.Parse(row.Last().ToString())));
        }

        return result;
    }

    private async Task<List<CountPerDay>> GetPerDayResult(string query, CancellationToken cancellationToken)
    {
        var queryResult = await GetQueryResult<QueryResult>(query);

        var result = new List<CountPerDay>();

        foreach (var row in queryResult.Tables.First().Rows)
        {
            result.Add(new CountPerDay(DateOnly.FromDateTime(DateTime.Parse(row.First().ToString())), int.Parse(row.Last().ToString())));
        }

        return result;
    }
}