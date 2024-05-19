using System.Runtime.InteropServices.JavaScript;
using Microsoft.AspNetCore.Components.Web.Virtualization;

namespace MauiInsights.Web.Services;

public interface IInsightsService
{
    Task<bool> ValidateToken(string appId, string token);
    
    #region Diagnistics
    Task<List<CountPerDay>> GetCrashesPerDay(GlobalFilter filter);
    Task<List<CountPerDay>> GetErrorsPerDay(GlobalFilter filter);
    Task<List<CountPerKey>>  GetCrashesGrouped(GlobalFilter filter);
    Task<List<CountPerKey>> GetErrorsGrouped(GlobalFilter filter);
    Task<ErrorDetails> GetCrashDetails(string id, GlobalFilter filter);
    Task<ErrorDetails> GetErrorDetails(string id, GlobalFilter filter);
    Task<List<EventItem>> GetEventsByUser(string userId, DateTime timestamp);
    Task<List<AvgPerKey>> GetDependencyAvgDurations(GlobalFilter filter);
    Task<List<CountPerKey>> GetTopDependencies(GlobalFilter filter);
    #endregion

    #region Analytics

    Task<List<CountPerDay>> GetPageViewsPerDay(GlobalFilter filter);
    Task<List<CountPerKey>> GetPageViewsGrouped(GlobalFilter filter);
    Task<List<CountPerKey>> GetEventsGrouped(GlobalFilter filter);
    Task<List<CountPerDay>> GetUsersPerDay(GlobalFilter filter);
    Task<List<CountPerKey>> GetUserPerCountry(GlobalFilter filter);

    #endregion

}

public abstract class CountPer(int count)
{
    public int Count { get; } = count;
}

public class CountPerDay(DateOnly date, int count) : CountPer(count)
{
    public DateOnly Date => date;
}

public class CountPerKey(string key, int count) : CountPer(count)
{
    public string Key => key;
}

public class AvgPerKey(string key, double avg)
{
    public string Key => key;
    public double Avg => avg;
}



public class ErrorDetails
{
    public int Count => Items.Count;
    public int AffectedUsersCount { get; set; }
    public List<string> AffectedAppVersions { get; set; } = new();
    public List<string> AffectedOperatingSystems{ get; set; } = new();

    public List<ErrorItem> Items { get; set; } = new();

}

public abstract class LogItem
{
    public Dictionary<string, string?> Data { get; protected init; }
}

public class ErrorItem : LogItem
{
    public ErrorItem(Dictionary<string, string?> data)
    {
        Data = data;
    }

    public string? UserId => GetData("user_Id");
    public string? AppVersion => GetData("AppVersion");
    public string? ClientType => GetData("client_Type");
    public string? ClientModel => GetData("client_Model");
    public string? ClientOs => GetData("client_OS");
    public string? ClientOsVersion => GetData("OperatingSystemVersion");
    public string? ClientCity => GetData("client_City");
    public string? ClientState => GetData("client_StateOrProvince");
    public string? ClientCountry => GetData("client_CountryOrRegion");
    public DateTime Timestamp => DateTime.Parse(GetData("timestamp")!);

    public string? StackTrace => GetData("StackTrace");

    private string? GetData(string key)
    {
        return Data.GetValueOrDefault(key);
    }
}

public class EventItem : LogItem
{
    public EventType EventType { get; }

    public EventItem(Dictionary<string, string?> data, EventType eventType)
    {
        EventType = eventType;
        Data = data;
    }

    public string? Name => GetName();
    public DateTime Timestamp => DateTime.Parse(GetData("timestamp")!);

    private string? GetName()
    {
        var name = GetData("name");

        if (name is null && EventType == EventType.Error)
        {
            name = GetData("problemId");
        }
        else if (name is null && EventType == EventType.Crash)
        {
            name = $"{GetData("problemId")} - {GetData("outerMessage")}";
        }

        return name;
    }
    
    
    private string? GetData(string key)
    {
        return Data.GetValueOrDefault(key);
    }
}

public enum EventType
{
    CustomEvent,
    PageView,
    Error,
    Crash
}