namespace TinyInsights.Web.Services.Models;

public class DependencyCount(string key, string method, int count) : CountPer(count)
{
    public string Key => key;
    public string Method => method;
}