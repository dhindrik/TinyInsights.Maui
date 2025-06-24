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
}
