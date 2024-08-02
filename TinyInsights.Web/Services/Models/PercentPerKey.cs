namespace TinyInsights.Web.Services.Models
{
    public class PercentPerKey(string key, double percent)
    {
        public string Key => key;
        public double Percent => percent;
    }
}
