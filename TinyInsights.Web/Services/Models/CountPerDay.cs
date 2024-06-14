namespace TinyInsights.Web.Services.Models;

public class CountPerDay(DateOnly date, int count) : CountPer(count)
{
    public DateOnly Date => date;
}
