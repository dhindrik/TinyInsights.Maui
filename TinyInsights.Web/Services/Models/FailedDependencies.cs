namespace TinyInsights.Web.Services.Models;

public class FailedDependencies
{
    public int Count => Items.Count;
    public int AffectedUsersCount { get; set; }
    public List<string> AffectedAppVersions { get; set; } = new();
    public List<string> AffectedOperatingSystems { get; set; } = new();

    public List<FailedDependencyItem> Items { get; set; } = new();

}
