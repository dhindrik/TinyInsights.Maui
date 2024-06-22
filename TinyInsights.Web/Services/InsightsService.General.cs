namespace TinyInsights.Web.Services;

public partial class InsightsService
{
    public async Task<List<string>> GetUniqueAppVersions()
    {
        var query = $"union pageViews, customEvents, exceptions | distinct strcat(tostring(customDimensions['AppVersion']), '(', tostring(customDimensions['AppBuildNumber']),')')";

        var queryResult = await GetQueryResult<QueryResult>(query);

        return queryResult.Tables.First().Rows.Select(r => r.First().ToString()).ToList();

    }
}
