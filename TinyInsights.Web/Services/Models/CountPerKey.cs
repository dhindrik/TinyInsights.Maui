namespace TinyInsights.Web.Services.Models;

public class CountPerKey(string key, int count) : CountPer(count)
{
    public string Key => key;
}
