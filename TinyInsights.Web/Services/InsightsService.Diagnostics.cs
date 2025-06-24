using System.Globalization;
using System.Text;
using System.Text.Json;

namespace TinyInsights.Web.Services;

public partial class InsightsService : IInsightsService
{
    private readonly HttpClient httpClient;

    private string? appId;

    public InsightsService(IHttpClientFactory httpClientFactory)
    {
        httpClient = httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri("https://api.applicationinsights.io/");
    }

    public async Task<bool> AddAndValidateApiKey(string appId, string apiKey, CancellationToken cancellationToken = default)
    {
        this.appId = appId;

        httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);

        var url = $"/v1/apps/{appId}/events/$all?$top=5";

        var response = await httpClient.GetAsync(url, cancellationToken);

        return response.IsSuccessStatusCode;
    }

    public async Task<(bool Succeed, string? ErrorMessage)> AddAndValidateBearer(string appId, string token, CancellationToken cancellationToken = default)
    {
        this.appId = appId;

        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var url = $"/v1/apps/{appId}/events/$all?$top=5";

        var response = await httpClient.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            return (false, responseContent);
        }

        return (true, null);
    }

    public async Task<List<CountPerDay>> GetErrorsPerDay(GlobalFilter filter, List<string>? errorSeverities = null, CancellationToken cancellationToken = default)
    {
        var queryFilter = GetFilter(filter);

        if (errorSeverities is { Count: > 0 })
        {
            var filterBuilder = new StringBuilder();
            filterBuilder.Append(queryFilter);
            filterBuilder.Append("customDimensions.ErrorSeverity in (");
            filterBuilder.AppendJoin(',', errorSeverities.Select(x => $"'{x}'"));
            filterBuilder.Append(") and ");
            queryFilter = filterBuilder.ToString();
        }

        var query =
            $"exceptions | where{queryFilter} customDimensions.IsCrash != 'true' and timestamp > ago({filter.NumberOfDays}d) | summarize count_sum = sum(itemCount) by bin(timestamp,1d)";
        var queryResult = await GetQueryResult<QueryResult>(query, cancellationToken);
        var result = new List<CountPerDay>();

        foreach (var row in queryResult.Tables.First().Rows)
        {
            result.Add(new CountPerDay(DateOnly.FromDateTime(DateTime.Parse(row.First().ToString())),
                int.Parse(row.Last().ToString())));
        }

        return result;
    }

    public async Task<List<CountPerDay>> GetCrashesPerDay(GlobalFilter filter, CancellationToken cancellationToken = default)
    {
        var queryFilter = GetFilter(filter);

        var query =
            $"exceptions | where{queryFilter} customDimensions.IsCrash == 'true' and timestamp > ago({filter.NumberOfDays}d) | summarize count_sum = sum(itemCount) by bin(timestamp,1d)";
        var queryResult = await GetQueryResult<QueryResult>(query, cancellationToken);
        var result = new List<CountPerDay>();

        foreach (var row in queryResult.Tables.First().Rows)
        {
            result.Add(new CountPerDay(DateOnly.FromDateTime(DateTime.Parse(row.First().ToString())),
                int.Parse(row.Last().ToString())));
        }

        return result;
    }

    public async Task<List<ErrorCount>> GetErrorsGrouped(GlobalFilter filter, List<string>? errorSeverities = null, CancellationToken cancellationToken = default)
    {
        var queryFilter = GetFilter(filter);

        if (errorSeverities is { Count: > 0 })
        {
            var filterBuilder = new StringBuilder();
            filterBuilder.Append(queryFilter);
            filterBuilder.Append("customDimensions.ErrorSeverity in (");
            filterBuilder.AppendJoin(',', errorSeverities.Select(x => $"'{x}'"));
            filterBuilder.Append(") and ");
            queryFilter = filterBuilder.ToString();
        }

        var query =
            $"exceptions | where{queryFilter} customDimensions.IsCrash != 'true' and timestamp > ago({filter.NumberOfDays}d) | summarize count_sum = sum(itemCount) by problemId, tostring(customDimensions.ErrorSeverity)";

        var queryResult = await GetQueryResult<QueryResult>(query, cancellationToken);

        var result = new List<ErrorCount>();

        foreach (var row in queryResult.Tables.First().Rows)
        {
            result.Add(new ErrorCount(row[0].ToString(), row[1].ToString(), int.Parse(row[2].ToString())));
        }

        return result;
    }

    public async Task<List<CrashItem>> GetCrashesGrouped(GlobalFilter filter, CancellationToken cancellationToken = default)
    {
        var queryFilter = GetFilter(filter);

        var query =
            $"exceptions | where{queryFilter} customDimensions.IsCrash == 'true' and timestamp > ago({filter.NumberOfDays}d) | extend AppVersion = parse_version(tostring(customDimensions.AppVersion)) | summarize count_sum = sum(itemCount), users_affected = dcount(user_Id), latest_app_version = arg_max(AppVersion, customDimensions.AppVersion), last_reported = max(timestamp) by strcat(problemId, ' - ', outerMessage)";

        var queryResult = await GetQueryResult<QueryResult>(query, cancellationToken);

        var result = new List<CrashItem>();

        foreach (var row in queryResult.Tables.First().Rows)
        {
            result.Add(new CrashItem()
            {
                Key = row[0].ToString(),
                Count = int.Parse(row[1].ToString()),
                UsersAffected = int.Parse(row[2].ToString()),
                LatestAppVersion = row[4].ToString(),
                LastReport = DateTime.Parse(row[5].ToString())
            });
        }

        return result;
    }

    public Task<ErrorDetails> GetErrorDetails(string id, GlobalFilter filter, string? severity = null, CancellationToken cancellationToken = default)
    {
        var queryFilter = GetFilter(filter);

        if (severity is not null)
        {
            queryFilter += $"customDimensions.ErrorSeverity == '{severity}' and ";
        }

        var query =
            $"exceptions | where{queryFilter} customDimensions.IsCrash != 'true' and timestamp > ago({filter.NumberOfDays}d) and problemId == '{id}' | top 100 by timestamp desc";

        return GetErrorDetails(query, cancellationToken);
    }

    public Task<ErrorDetails> GetCrashDetails(string id, GlobalFilter filter, CancellationToken cancellationToken = default)
    {
        var queryFilter = GetFilter(filter);

        var query =
            $"exceptions | where{queryFilter} customDimensions.IsCrash == 'true' and timestamp > ago({filter.NumberOfDays}d) and strcat(problemId, \" - \", outerMessage) == '{id}' | top 100 by timestamp desc";

        return GetErrorDetails(query, cancellationToken);
    }

    private async Task<ErrorDetails> GetErrorDetails(string query, CancellationToken cancellationToken = default)
    {
        var queryResult = await GetQueryResult<QueryResult>(query, cancellationToken);

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

    public Task<List<EventItem>> GetEventsByUser(string userId, GlobalFilter filter, CancellationToken cancellationToken = default)
    {
        return GetEventsByUser(userId, DateTime.Now, DateTime.Now.AddDays(-filter.NumberOfDays), cancellationToken);
    }

    public async Task<List<EventItem>> GetEventsByUser(string userId, DateTime timestamp, CancellationToken cancellationToken = default)
    {
        return await GetEventsByUser(userId, timestamp, timestamp.AddHours(-1), cancellationToken);
    }

    private async Task<List<EventItem>> GetEventsByUser(string userId, DateTime to, DateTime from, CancellationToken cancellationToken = default)
    {
        var toDate = to.ToUniversalTime().ToString("o");
        var fromDate = from.ToUniversalTime().ToString("o");

        userId = userId.ToLower();

        var eventQuery =
            $"customEvents| where tolower(user_Id) == '{userId}' and timestamp between (todatetime('{fromDate}') .. todatetime('{toDate}'))";
        var pageViewsQuery =
            $"pageViews| where tolower(user_Id) == '{userId}' and timestamp between (todatetime('{fromDate}') .. todatetime('{toDate}'))";
        var errorsQuery =
            $"exceptions | where tolower(user_Id) == '{userId}' and customDimensions.IsCrash != 'true' and timestamp between (todatetime('{fromDate}') .. todatetime('{toDate}'))";
        var crashQuery =
            $"exceptions | where tolower(user_Id) == '{userId}' and customDimensions.IsCrash == 'true' and timestamp between (todatetime('{fromDate}') .. todatetime('{toDate}'))";
        var dependencyQuery =
            $"dependencies | where tolower(user_Id) == '{userId}' and timestamp between (todatetime('{fromDate}') .. todatetime('{toDate}'))";

        var pageViewsTask = GetQueryResult<QueryResult>(pageViewsQuery, cancellationToken);
        var eventsTask = GetQueryResult<QueryResult>(eventQuery, cancellationToken);
        var errorsTask = GetQueryResult<QueryResult>(errorsQuery, cancellationToken);
        var crashTask = GetQueryResult<QueryResult>(crashQuery, cancellationToken);
        var dependencyTask = GetQueryResult<QueryResult>(dependencyQuery, cancellationToken);

        await Task.WhenAll(pageViewsTask, eventsTask, errorsTask, crashTask, dependencyTask);

        var pageViewsResult = pageViewsTask.Result;
        var eventsResult = eventsTask.Result;
        var errorsResult = errorsTask.Result;
        var crashResult = crashTask.Result;
        var dependencyResult = dependencyTask.Result;

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

        foreach (var row in dependencyResult.Tables.First().Rows)
        {
            var data = GetData(dependencyResult, row);
            var eventItem = new EventItem(data, EventType.Dependency);
            result.Add(eventItem);
        }

        return result.OrderByDescending(x => x.Timestamp).ToList();
    }

    public async Task<List<AvgPerKey>> GetDependencyAvgDurations(GlobalFilter filter, CancellationToken cancellationToken = default)
    {
        var queryFilter = GetFilter(filter);

        var query =
            $"dependencies | where{queryFilter} timestamp > ago({filter.NumberOfDays}d) | summarize avg = avg(duration) by data | limit 20";

        var queryResult = await GetQueryResult<QueryResult>(query, cancellationToken);

        var result = new List<AvgPerKey>();

        foreach (var row in queryResult.Tables.First().Rows)
        {
            result.Add(new AvgPerKey(row.First().ToString(), double.Parse(row.Last().ToString(), CultureInfo.InvariantCulture)));
        }

        return result;
    }

    public async Task<List<DependencyCount>> GetTopDependencies(GlobalFilter filter, CancellationToken cancellationToken = default)
    {
        var queryFilter = GetFilter(filter);

        var query =
            $"dependencies | where{queryFilter} timestamp > ago({filter.NumberOfDays}d) | summarize count_sum = sum(itemCount) by data, tostring(customDimensions.HttpMethod) | limit 20";

        var queryResult = await GetQueryResult<QueryResult>(query, cancellationToken);

        var result = new List<DependencyCount>();

        foreach (var row in queryResult.Tables.First().Rows)
        {
            result.Add(new DependencyCount(row[0].ToString(), row[1].ToString(), int.Parse(row[2].ToString())));
        }

        return result;
    }

    public async Task<List<DependencyCount>> GetFailedDependencies(GlobalFilter filter, List<string>? resultCodeFilter = null, CancellationToken cancellationToken = default)
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
            $"dependencies | where{queryFilter} success == false and timestamp > ago({filter.NumberOfDays}d) | summarize count_sum = sum(itemCount) by data, tostring(customDimensions.HttpMethod)";

        var queryResult = await GetQueryResult<QueryResult>(query, cancellationToken);

        var result = new List<DependencyCount>();

        foreach (var row in queryResult.Tables.First().Rows)
        {
            result.Add(new DependencyCount(row[0].ToString(), row[1].ToString(), int.Parse(row[2].ToString())));
        }

        return result;
    }

    public async Task<List<string>> GetFailedDependenciesStatusCodes(GlobalFilter filter, CancellationToken cancellationToken = default)
    {
        var queryFilter = GetFilter(filter);

        var query = $"dependencies | where{queryFilter} success == false and resultCode != ''| distinct resultCode";

        var queryResult = await GetQueryResult<QueryResult>(query, cancellationToken);

        return queryResult.Tables.First().Rows.Select(r => r.First().ToString()).ToList();
    }

    public async Task<FailedDependencies> GetFailedDependencies(string key, string method, GlobalFilter filter, CancellationToken cancellationToken = default)
    {
        var queryFilter = GetFilter(filter);

        var query =
            $"dependencies | where{queryFilter} success == false and timestamp > ago({filter.NumberOfDays}d) and data == '{key}' and customDimensions.HttpMethod == '{method}'";

        var queryResult = await GetQueryResult<QueryResult>(query, cancellationToken);

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

    public async Task<List<CountPerDay>> GetFailedDependenciesPerDay(GlobalFilter filter, CancellationToken cancellationToken = default)
    {
        var queryFilter = GetFilter(filter);

        var query =
            $"dependencies | where{queryFilter} success == false and timestamp > ago({filter.NumberOfDays}d) | summarize count_sum = sum(itemCount) by bin(timestamp,1d)";
        var queryResult = await GetQueryResult<QueryResult>(query, cancellationToken);
        var result = new List<CountPerDay>();

        foreach (var row in queryResult.Tables.First().Rows)
        {
            result.Add(new CountPerDay(DateOnly.FromDateTime(DateTime.Parse(row.First().ToString())),
                int.Parse(row.Last().ToString())));
        }

        return result;
    }

    private async Task<T> GetQueryResult<T>(string query, CancellationToken cancellationToken = default)
    {
        var url = $"/v1/apps/{appId}/query";

        var json = JsonSerializer.Serialize(new { query = query });
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync(url, content, cancellationToken);

        if (response.IsSuccessStatusCode is false && (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || response.StatusCode == System.Net.HttpStatusCode.NotFound))
        {
            throw new UnauthorizedAccessException();
        }

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

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

                if (!string.IsNullOrWhiteSpace(json))
                {
                    using var doc = JsonDocument.Parse(json);
                    foreach (var property in doc.RootElement.EnumerateObject())
                    {
                        data[property.Name] = property.Value.ValueKind == JsonValueKind.Null ? null : property.Value.ToString();
                    }
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

    public async Task<List<CountPerDay>> GetErrorDetailsPerDay(string problemId, GlobalFilter filter, string? severity = null, CancellationToken cancellationToken = default)
    {
        var queryFilter = GetFilter(filter);

        if (severity is not null)
        {
            queryFilter += $"customDimensions.ErrorSeverity == '{severity}' and ";
        }

        var query =
            $"exceptions | where{queryFilter} customDimensions.IsCrash != 'true' and problemId == '{problemId}' and timestamp > ago({filter.NumberOfDays}d) | summarize count_sum = sum(itemCount) by bin(timestamp,1d)";

        var queryResult = await GetQueryResult<QueryResult>(query, cancellationToken);
        var result = new List<CountPerDay>();

        foreach (var row in queryResult.Tables.First().Rows)
        {
            result.Add(new CountPerDay(DateOnly.FromDateTime(DateTime.Parse(row.First().ToString())),
                int.Parse(row.Last().ToString())));
        }

        return result.OrderBy(x => x.Date).ToList();
    }

    public async Task<List<CountPerDay>> GetCrashDetailsPerDay(string problemId, GlobalFilter filter, CancellationToken cancellationToken = default)
    {
        var queryFilter = GetFilter(filter);

        var query =
            $"exceptions | where{queryFilter} customDimensions.IsCrash == 'true' and strcat(problemId, \" - \", outerMessage) == '{problemId}' and timestamp > ago({filter.NumberOfDays}d) | summarize count_sum = sum(itemCount) by bin(timestamp,1d)";

        var queryResult = await GetQueryResult<QueryResult>(query, cancellationToken);
        var result = new List<CountPerDay>();

        foreach (var row in queryResult.Tables.First().Rows)
        {
            result.Add(new CountPerDay(DateOnly.FromDateTime(DateTime.Parse(row.First().ToString())),
                int.Parse(row.Last().ToString())));
        }

        return result.OrderBy(x => x.Date).ToList();
    }
}

public class QueryResult
{
    public List<Table> Tables { get; set; } = [];
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
    public string? HttpMethod { get; set; }
    public string? OperatingSystemVersion { get; set; }
}