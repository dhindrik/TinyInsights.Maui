namespace TinyInsights.Web.Pages;
public partial class Query
{
    private Dictionary<string, string> predefinedQueries = new()
    {
        {"Latest errors", "exceptions" },
        {"Latest crashes", "exceptions | where customDimensions.IsCrash == true" },
        {"Exceptions per day", "exceptions | summarize count() by bin(timestamp, 1d)" },
        {"Crashes per day", "exceptions | where customDimensions.IsCrash == true | summarize count() by bin(timestamp, 1d)" },
        {"Users per day", "customEvents | summarize dcount(user_Id) by bin(timestamp, 1d)" }
    };

    private const string CustomTimeRangeValue = "custom";

    private Dictionary<string, string> timeRanges = new()
    {
        {"Last hours", "1h" },
        {"Last 4 hours", "4h" },
        {"Last 12 hours", "12h" },
        {"Last 24 hours", "24h" },
        {"Last 48 hours", "48h" },
        { "Last 3 days", "3d" },
        { "Last 7 days", "7d" },
        { "Last 30 days", "30d" },
        {"Custom", CustomTimeRangeValue }
    };
}
