namespace TinyInsights.Web.Services.Models;

public abstract class LogItem
{
    public Dictionary<string, string?> Data { get; protected init; }
}
