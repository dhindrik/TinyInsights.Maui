namespace TinyInsights.Web.Services;

public partial class InsightsService
{
    public async Task<List<CountPerDay>> GetPageViewsPerDay(GlobalFilter filter)
    {
        var queryFilter = GetFilter(filter);

        var query = $"pageViews | where{queryFilter} timestamp > ago({filter.NumberOfDays}d) | summarize count_sum = sum(itemCount) by bin(timestamp,1d)";

        var queryResult = await GetQueryResult<QueryResult>(query);
        var result = new List<CountPerDay>();

        foreach (var row in queryResult.Tables.First().Rows)
        {
            result.Add(new CountPerDay(DateOnly.FromDateTime(DateTime.Parse(row.First().ToString())), int.Parse(row.Last().ToString())));
        }

        return result;
    }

    public async Task<List<CountPerKey>> GetPageViewsGrouped(GlobalFilter filter)
    {
        var queryFilter = GetFilter(filter);

        var query = $"pageViews | where{queryFilter} timestamp > ago({filter.NumberOfDays}d) | summarize count_sum = sum(itemCount) by name";

        var queryResult = await GetQueryResult<QueryResult>(query);

        var result = new List<CountPerKey>();

        foreach (var row in queryResult.Tables.First().Rows)
        {
            result.Add(new CountPerKey(row.First().ToString(), int.Parse(row.Last().ToString())));
        }

        return result;
    }

    public async Task<List<CountPerKey>> GetEventsGrouped(GlobalFilter filter)
    {
        var queryFilter = GetFilter(filter);

        var query = $"customEvents | where{queryFilter} timestamp > ago({filter.NumberOfDays}d) | summarize count_sum = sum(itemCount) by name";

        var queryResult = await GetQueryResult<QueryResult>(query);

        var result = new List<CountPerKey>();

        foreach (var row in queryResult.Tables.First().Rows)
        {
            result.Add(new CountPerKey(row.First().ToString(), int.Parse(row.Last().ToString())));
        }

        return result;
    }

    public async Task<List<CountPerDay>> GetEventsPerDay(string eventName, GlobalFilter filter)
    {
        var queryFilter = GetFilter(filter);
        var query = $"customEvents | where{queryFilter} name == '{eventName}' and timestamp > ago({filter.NumberOfDays}d) | summarize count_sum = sum(itemCount) by bin(timestamp,1d)";
        var queryResult = await GetQueryResult<QueryResult>(query);
        var result = new List<CountPerDay>();

        foreach (var row in queryResult.Tables.First().Rows)
        {
            result.Add(new CountPerDay(DateOnly.FromDateTime(DateTime.Parse(row.First().ToString())), int.Parse(row.Last().ToString())));
        }

        return result;
    }

    public async Task<List<CountPerDay>> GetUsersPerDay(GlobalFilter filter)
    {
        var queryFilter = GetFilter(filter);
        var query = $"pageViews | where{queryFilter} timestamp > ago({filter.NumberOfDays}d) | summarize uniqueUsers = dcount(user_Id) by bin(timestamp, 1d)";
        var queryResult = await GetQueryResult<QueryResult>(query);
        var result = new List<CountPerDay>();

        foreach (var row in queryResult.Tables.First().Rows)
        {
            result.Add(new CountPerDay(DateOnly.FromDateTime(DateTime.Parse(row.First().ToString())), int.Parse(row.Last().ToString())));
        }

        return result;
    }

    public async Task<List<CountPerKey>> GetUserPerCountry(GlobalFilter filter)
    {
        var queryFilter = GetFilter(filter);
        var query = $"pageViews | where{queryFilter} timestamp > ago({filter.NumberOfDays}d) | summarize PageViewsCount = dcount(user_Id) by client_CountryOrRegion";

        var queryResult = await GetQueryResult<QueryResult>(query);

        var result = new List<CountPerKey>();

        foreach (var row in queryResult.Tables.First().Rows)
        {
            result.Add(new CountPerKey(row.First().ToString(), int.Parse(row.Last().ToString())));
        }

        return result;
    }

    public async Task<List<CountPerKey>> GetUsersPerIdiom(GlobalFilter filter)
    {

        var queryFilter = GetFilter(filter);
        var query = $"pageViews | where{queryFilter} timestamp > ago({filter.NumberOfDays}d) | summarize PageViewsCount = dcount(user_Id) by client_Type";

        var queryResult = await GetQueryResult<QueryResult>(query);

        var result = new List<CountPerKey>();

        foreach (var row in queryResult.Tables.First().Rows)
        {
            result.Add(new CountPerKey(row.First().ToString(), int.Parse(row.Last().ToString())));
        }

        return result;
    }

    public async Task<List<CountPerKey>> GetUsersPerOperatingSystem(GlobalFilter filter)
    {

        var queryFilter = GetFilter(filter);
        var query = $"pageViews | where{queryFilter} timestamp > ago({filter.NumberOfDays}d) | summarize PageViewsCount = dcount(user_Id) by client_OS";

        var queryResult = await GetQueryResult<QueryResult>(query);

        var result = new List<CountPerKey>();

        foreach (var row in queryResult.Tables.First().Rows)
        {
            result.Add(new CountPerKey(row.First().ToString(), int.Parse(row.Last().ToString())));
        }

        return result;
    }
}