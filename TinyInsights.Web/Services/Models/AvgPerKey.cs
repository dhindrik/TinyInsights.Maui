namespace TinyInsights.Web.Services.Models;

public class AvgPerKey(string key, double avg)
{
    public string Key => key;
    public double Avg => avg;
}
