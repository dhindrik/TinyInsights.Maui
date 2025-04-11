namespace TinyInsights.Web.Services.Models;

public class CrashItem
{
    public required string Key { get; set; }
    public int Count { get; set; }
    public int UsersAffected { get; set; }
    public string? LatestAppVersion { get; set; }
    public DateTime? LastReport { get; set; }
}
