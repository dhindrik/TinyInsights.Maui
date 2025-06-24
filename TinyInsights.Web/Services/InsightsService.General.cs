using System.Text;

namespace TinyInsights.Web.Services;

public partial class InsightsService
{
    public async Task<List<string>> GetUniqueAppVersions()
    {
        var query = $"union pageViews, customEvents, exceptions | distinct strcat(tostring(customDimensions['AppVersion']), '(', tostring(customDimensions['AppBuildNumber']),')')";

        var queryResult = await GetQueryResult<QueryResult>(query);

        return queryResult.Tables.First().Rows.Select(r => r.First().ToString()).ToList();

    }

    private string GetFilter(GlobalFilter filter)
    {
        var filterBuilder = new StringBuilder();
        filterBuilder.Append(" ");

        if (!string.IsNullOrWhiteSpace(filter.TextFilter))
        {
            filterBuilder.Append($"{filter.TextFilter} and ");
        }

        if (filter.OperatingSystemFilterValue is not null)
        {
            filterBuilder.Append($"client_OS == '{filter.OperatingSystemFilterValue}' and ");
        }

        if (filter.AppVersions.Where(x => x != GlobalFilter.AppVersionsDefaultValue).Any())
        {
            filterBuilder.Append("customDimensions.AppBuildNumber in (");

            foreach (var number in filter.AppBuildNumbers)
            {
                filterBuilder.Append($"'{number}',");
            }

            filterBuilder.Remove(filterBuilder.Length - 1, 1);
            filterBuilder.Append(") and ");
        }

        return filterBuilder.ToString();
    }

    public Task<QueryResult> RunQuery(string query, CancellationToken cancellationToken = default)
    {
        return GetQueryResult<QueryResult>(query, cancellationToken);
    }
}
