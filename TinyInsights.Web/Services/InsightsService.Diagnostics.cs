using System.Globalization;
using System.Text;
using System.Text.Json;

namespace TinyInsights.Web.Services;

public partial class InsightsService : IInsightsService
{
    public event EventHandler UnAuthorized;

    private readonly HttpClient httpClient;

    private string? appId;

    public InsightsService(IHttpClientFactory httpClientFactory)
    {
        httpClient = httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri("https://api.applicationinsights.io/");
    }

    public async Task<bool> AddAndValidateApiKey(string appId, string apiKey)
    {
        this.appId = appId;

        httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);

        var url = $"/v1/apps/{appId}/events/$all?$top=5";

        var response = await httpClient.GetAsync(url);

        return response.IsSuccessStatusCode;
    }

    public async Task<(bool Succeed, string? ErrorMessage)> AddAndValidateBearer(string appId, string token)
    {
        this.appId = appId;

        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var url = $"/v1/apps/{appId}/events/$all?$top=5";

        var response = await httpClient.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadAsStringAsync();
            return (false, responseContent);
        }

        return (true, null);
    }

    public async Task<List<CountPerDay>> GetErrorsPerDay(GlobalFilter filter)
    {
        var queryFilter = GetFilter(filter);

        var query =
            $"exceptions | where{queryFilter} customDimensions.IsCrash != 'true' and timestamp > ago({filter.NumberOfDays}d) | summarize count_sum = sum(itemCount) by bin(timestamp,1d)";
        var queryResult = await GetQueryResult<QueryResult>(query);
        var result = new List<CountPerDay>();

        foreach (var row in queryResult.Tables.First().Rows)
        {
            result.Add(new CountPerDay(DateOnly.FromDateTime(DateTime.Parse(row.First().ToString())),
                int.Parse(row.Last().ToString())));
        }

        return result;
    }

    public async Task<List<CountPerDay>> GetCrashesPerDay(GlobalFilter filter)
    {
        var queryFilter = GetFilter(filter);

        var query =
            $"exceptions | where{queryFilter} customDimensions.IsCrash == 'true' and timestamp > ago({filter.NumberOfDays}d) | summarize count_sum = sum(itemCount) by bin(timestamp,1d)";
        var queryResult = await GetQueryResult<QueryResult>(query);
        var result = new List<CountPerDay>();

        foreach (var row in queryResult.Tables.First().Rows)
        {
            result.Add(new CountPerDay(DateOnly.FromDateTime(DateTime.Parse(row.First().ToString())),
                int.Parse(row.Last().ToString())));
        }

        return result;
    }

    public async Task<List<CountPerKey>> GetErrorsGrouped(GlobalFilter filter)
    {
        var queryFilter = GetFilter(filter);

        var query =
            $"exceptions | where{queryFilter} customDimensions.IsCrash != 'true' and timestamp > ago({filter.NumberOfDays}d) | summarize count_sum = sum(itemCount) by problemId";

        var queryResult = await GetQueryResult<QueryResult>(query);

        var result = new List<CountPerKey>();

        foreach (var row in queryResult.Tables.First().Rows)
        {
            result.Add(new CountPerKey(row.First().ToString(), int.Parse(row.Last().ToString())));
        }

        return result;
    }

    public async Task<List<CountPerKey>> GetCrashesGrouped(GlobalFilter filter)
    {
        var queryFilter = GetFilter(filter);

        var query =
            $"exceptions | where{queryFilter} customDimensions.IsCrash == 'true' and timestamp > ago({filter.NumberOfDays}d) | summarize count_sum = sum(itemCount) by strcat(problemId, \" - \", outerMessage)";

        var queryResult = await GetQueryResult<QueryResult>(query);

        var result = new List<CountPerKey>();

        foreach (var row in queryResult.Tables.First().Rows)
        {
            result.Add(new CountPerKey(row.First().ToString(), int.Parse(row.Last().ToString())));
        }

        return result;
    }

    public Task<ErrorDetails> GetErrorDetails(string id, GlobalFilter filter)
    {
        var queryFilter = GetFilter(filter);

        var query =
            $"exceptions | where{queryFilter} customDimensions.IsCrash != 'true' and timestamp > ago({filter.NumberOfDays}d) and problemId == '{id}'";

        return GetErrorDetails(query);
    }

    public Task<ErrorDetails> GetCrashDetails(string id, GlobalFilter filter)
    {
        var queryFilter = GetFilter(filter);

        var query =
            $"exceptions | where{queryFilter} customDimensions.IsCrash == 'true' and timestamp > ago({filter.NumberOfDays}d) and strcat(problemId, \" - \", outerMessage) == '{id}'";

        return GetErrorDetails(query);
    }

    private async Task<ErrorDetails> GetErrorDetails(string query)
    {
        var queryResult = await GetQueryResult<QueryResult>(query);

        var result = new ErrorDetails();

        foreach (var row in queryResult.Tables.First().Rows)
        {
            var data = GetData(queryResult, row);

            var crash = new ErrorItem(data);
            result.Items.Add(crash);
        }

        result.AffectedUsersCount =
            result.Items.Where(x => x.UserId is not null).Select(x => x.UserId).Distinct().Count();
        result.AffectedAppVersions = result.Items.Where(x => x.AppVersion is not null).Select(x => x.AppVersion)
            .Distinct().ToList();
        result.AffectedOperatingSystems = result.Items.Where(x => x.ClientOs is not null).Select(x => x.ClientOs)
            .Distinct().ToList();

        return result;
    }

    public Task<List<EventItem>> GetEventsByUser(string userId, GlobalFilter filter)
    {
        return GetEventsByUser(userId, DateTime.Now, DateTime.Now.AddDays(-filter.NumberOfDays));
    }

    public async Task<List<EventItem>> GetEventsByUser(string userId, DateTime timestamp)
    {
        return await GetEventsByUser(userId, timestamp, timestamp.AddHours(-1));
    }

    private async Task<List<EventItem>> GetEventsByUser(string userId, DateTime to, DateTime from)
    {
        var toDate = to.ToUniversalTime().ToString("o");
        var fromDate = from.ToUniversalTime().ToString("o");

        var eventQuery =
            $"customEvents| where user_Id == '{userId}' and timestamp between (todatetime('{fromDate}') .. todatetime('{toDate}'))";
        var pageViewsQuery =
            $"pageViews| where user_Id == '{userId}' and timestamp between (todatetime('{fromDate}') .. todatetime('{toDate}'))";
        var errorsQuery =
            $"exceptions | where user_Id == '{userId}' and customDimensions.IsCrash == 'true' and timestamp between (todatetime('{fromDate}') .. todatetime('{toDate}'))";
        var crashQuery =
            $"exceptions | where user_Id == '{userId}' and customDimensions.IsCrash != 'true' and timestamp between (todatetime('{fromDate}') .. todatetime('{toDate}'))";

        var pageViewsTask = GetQueryResult<QueryResult>(pageViewsQuery);
        var eventsTask = GetQueryResult<QueryResult>(eventQuery);
        var errorsTask = GetQueryResult<QueryResult>(errorsQuery);
        var crashTask = GetQueryResult<QueryResult>(crashQuery);

        await Task.WhenAll(pageViewsTask, eventsTask, errorsTask, crashTask);

        var pageViewsResult = pageViewsTask.Result;
        var eventsResult = eventsTask.Result;
        var errorsResult = errorsTask.Result;
        var crashResult = crashTask.Result;

        var result = new List<EventItem>();

        foreach (var row in pageViewsResult.Tables.First().Rows)
        {
            var data = GetData(pageViewsResult, row);

            var eventItem = new EventItem(data, EventType.PageView);
            result.Add(eventItem);
        }

        foreach (var row in eventsResult.Tables.First().Rows)
        {
            var data = GetData(eventsResult, row);

            var eventItem = new EventItem(data, EventType.CustomEvent);
            result.Add(eventItem);
        }

        foreach (var row in errorsResult.Tables.First().Rows)
        {
            var data = GetData(errorsResult, row);

            var eventItem = new EventItem(data, EventType.Error);
            result.Add(eventItem);
        }

        foreach (var row in crashResult.Tables.First().Rows)
        {
            var data = GetData(crashResult, row);

            var eventItem = new EventItem(data, EventType.Crash);
            result.Add(eventItem);
        }

        return result.OrderByDescending(x => x.Timestamp).ToList();
    }



    public async Task<List<AvgPerKey>> GetDependencyAvgDurations(GlobalFilter filter)
    {
        var queryFilter = GetFilter(filter);

        var query =
            $"dependencies | where{queryFilter} timestamp > ago({filter.NumberOfDays}d) | summarize avg = avg(duration) by data | limit 20";

        var queryResult = await GetQueryResult<QueryResult>(query);

        var result = new List<AvgPerKey>();

        foreach (var row in queryResult.Tables.First().Rows)
        {
            result.Add(new AvgPerKey(row.First().ToString(), double.Parse(row.Last().ToString(), CultureInfo.InvariantCulture)));
        }

        return result;
    }

    public async Task<List<CountPerKey>> GetTopDependencies(GlobalFilter filter)
    {
        var queryFilter = GetFilter(filter);

        var query =
            $"dependencies | where{queryFilter} timestamp > ago({filter.NumberOfDays}d) | summarize count_sum = sum(itemCount) by data | limit 20";

        var queryResult = await GetQueryResult<QueryResult>(query);

        var result = new List<CountPerKey>();

        foreach (var row in queryResult.Tables.First().Rows)
        {
            result.Add(new CountPerKey(row.First().ToString(), int.Parse(row.Last().ToString())));
        }

        return result;
    }

    public async Task<List<CountPerKey>> GetFailedDependencies(GlobalFilter filter, List<string>? resultCodeFilter = null)
    {
        var queryFilter = GetFilter(filter);

        if (resultCodeFilter is { Count: > 0 })
        {
            var filterBuilder = new StringBuilder();
            filterBuilder.Append(queryFilter);

            filterBuilder.Append("resultCode in (");

            foreach (var number in resultCodeFilter)
            {
                filterBuilder.Append($"'{number}',");
            }

            filterBuilder.Remove(filterBuilder.Length - 1, 1);
            filterBuilder.Append(") and ");

            queryFilter = filterBuilder.ToString();
        }

        var query =
            $"dependencies | where{queryFilter} success == false and timestamp > ago({filter.NumberOfDays}d) | summarize count_sum = sum(itemCount) by data";

        var queryResult = await GetQueryResult<QueryResult>(query);

        var result = new List<CountPerKey>();

        foreach (var row in queryResult.Tables.First().Rows)
        {
            result.Add(new CountPerKey(row.First().ToString(), int.Parse(row.Last().ToString())));
        }

        return result;
    }

    public async Task<List<string>> GetFailedDependenciesStatusCodes(GlobalFilter filter)
    {
        var queryFilter = GetFilter(filter);

        var query = $"dependencies | where{queryFilter} success == false and resultCode != ''| distinct resultCode ";

        var queryResult = await GetQueryResult<QueryResult>(query);

        return queryResult.Tables.First().Rows.Select(r => r.First().ToString()).ToList();

    }

    public async Task<FailedDependencies> GetFailedDependencies(string key, GlobalFilter filter)
    {
        var queryFilter = GetFilter(filter);

        var query =
            $"dependencies | where{queryFilter} success == false and timestamp > ago({filter.NumberOfDays}d) and data == '{key}'";

        var queryResult = await GetQueryResult<QueryResult>(query);

        var result = new FailedDependencies();

        foreach (var row in queryResult.Tables.First().Rows)
        {
            var data = GetData(queryResult, row);

            var dependency = new FailedDependencyItem(data);
            result.Items.Add(dependency);
        }

        result.AffectedUsersCount =
            result.Items.Where(x => x.UserId is not null).Select(x => x.UserId).Distinct().Count();
        result.AffectedAppVersions = result.Items.Where(x => x.AppVersion is not null).Select(x => x.AppVersion)
            .Distinct().ToList();
        result.AffectedOperatingSystems = result.Items.Where(x => x.ClientOs is not null).Select(x => x.ClientOs)
            .Distinct().ToList();

        return result;
    }

    public async Task<List<CountPerDay>> GetFailedDependenciesPerDay(GlobalFilter filter)
    {
        var queryFilter = GetFilter(filter);

        var query =
            $"dependencies | where{queryFilter} success == false and timestamp > ago({filter.NumberOfDays}d) | summarize count_sum = sum(itemCount) by bin(timestamp,1d)";
        var queryResult = await GetQueryResult<QueryResult>(query);
        var result = new List<CountPerDay>();

        foreach (var row in queryResult.Tables.First().Rows)
        {
            result.Add(new CountPerDay(DateOnly.FromDateTime(DateTime.Parse(row.First().ToString())),
                int.Parse(row.Last().ToString())));
        }

        return result;
    }

    private async Task<T> GetQueryResult<T>(string query)
    {
        var url = $"/v1/apps/{appId}/query";

        var json = JsonSerializer.Serialize(new { query = query });
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync(url, content);

        if (response.IsSuccessStatusCode is false && (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || response.StatusCode == System.Net.HttpStatusCode.NotFound))
        {
            throw new UnauthorizedAccessException();
        }

        var responseContent = await response.Content.ReadAsStringAsync();

        var queryResult = JsonSerializer.Deserialize<T>(responseContent,
            new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });

        return queryResult;
    }

    private Dictionary<string, string?> GetData(QueryResult queryResult, List<object> row)
    {
        var data = new Dictionary<string, string?>();

        for (var i = 0; i < row.Count; i++)
        {

            var current = row[i];
            var column = queryResult.Tables.First().Columns[i];

            if (queryResult.Tables.First().Columns[i].Name == "customDimensions")
            {
                var json = current.ToString();

                var custom = JsonSerializer.Deserialize<CustomDimensions>(json);

                data.Add(nameof(CustomDimensions.AppBuildNumber), custom.AppBuildNumber);
                data.Add(nameof(CustomDimensions.AppVersion), custom.AppVersion);
                data.Add(nameof(CustomDimensions.Language), custom.Language);
                data.Add(nameof(CustomDimensions.StackTrace), custom.StackTrace);
                data.Add(nameof(CustomDimensions.Manufacturer), custom.Manufacturer);

                if (custom.FullUrl is not null)
                {
                    data.Add(nameof(CustomDimensions.FullUrl), custom.FullUrl);
                }
            }
            else
            {
                if (current is JsonElement element)
                {
                    data.Add(column.Name, element.ToString());
                }

            }
        }

        return data;
    }
}

public class QueryResult
{
    public List<Table> Tables { get; set; }
}

public class Column
{
    public string Name { get; set; }

    public string Type { get; set; }
}

public class Table
{
    public List<Column> Columns { get; set; }
    public List<List<object>> Rows { get; set; }
}

public class CustomDimensions
{
    public string? AppBuildNumber { get; set; }
    public string? AppVersion { get; set; }
    public string? Language { get; set; }
    public string? StackTrace { get; set; }
    public string? Manufacturer { get; set; }
    public string? FullUrl { get; set; }
}
