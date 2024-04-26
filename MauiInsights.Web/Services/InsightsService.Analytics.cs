namespace MauiInsights.Web.Services;

public partial class InsightsService
{
    public async Task<List<CountPerDay>> GetPageViewsPerDay(int days)
    {
        var query = $"pageViews | where timestamp > ago({days}d) | summarize count_sum = sum(itemCount) by bin(timestamp,1d)";
        var queryResult = await GetQueryResult<QueryResult>(query);
        var result = new List<CountPerDay>();

        foreach (var row in queryResult.Tables.First().Rows)
        {
            result.Add(new CountPerDay(DateOnly.FromDateTime( DateTime.Parse(row.First().ToString())), int.Parse(row.Last().ToString())));
        }

        return result;
    }
    
    public async Task<List<CountPerKey>>  GetPageViewsGrouped(int days)
    {
        var query = $"pageViews | where timestamp > ago({days}d) | summarize count_sum = sum(itemCount) by name";

        var queryResult = await GetQueryResult<QueryResult>(query);

        var result = new List<CountPerKey>();

        foreach (var row in queryResult.Tables.First().Rows)
        {
            result.Add(new CountPerKey(row.First().ToString(), int.Parse(row.Last().ToString())));
        }

        return result;
    }
    
    public async Task<List<CountPerKey>>  GetEventsGrouped(int days)
    {
        var query = $"customEvents | where timestamp > ago({days}d) | summarize count_sum = sum(itemCount) by name";

        var queryResult = await GetQueryResult<QueryResult>(query);

        var result = new List<CountPerKey>();

        foreach (var row in queryResult.Tables.First().Rows)
        {
            result.Add(new CountPerKey(row.First().ToString(), int.Parse(row.Last().ToString())));
        }

        return result;
    }
    
    public async Task<List<CountPerDay>> GetUsersPerDay(int days)
    {
        var query = $"pageViews | where timestamp > ago({days}d) | summarize uniqueUsers = dcount(user_Id) by bin(timestamp, 1d)";
        var queryResult = await GetQueryResult<QueryResult>(query);
        var result = new List<CountPerDay>();

        foreach (var row in queryResult.Tables.First().Rows)
        {
            result.Add(new CountPerDay(DateOnly.FromDateTime( DateTime.Parse(row.First().ToString())), int.Parse(row.Last().ToString())));
        }

        return result;
    }
    
    public async Task<List<CountPerKey>>  GetUserPerCountry(int days)
    {
        var query = $"pageViews | where timestamp > ago({days}d) | summarize PageViewsCount = dcount(user_Id) by client_CountryOrRegion";

        var queryResult = await GetQueryResult<QueryResult>(query);

        var result = new List<CountPerKey>();

        foreach (var row in queryResult.Tables.First().Rows)
        {
            result.Add(new CountPerKey(row.First().ToString(), int.Parse(row.Last().ToString())));
        }

        return result;
    }
}