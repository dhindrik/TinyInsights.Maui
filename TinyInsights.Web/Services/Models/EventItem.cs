namespace TinyInsights.Web.Services.Models;

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
        if (EventType == EventType.Dependency)
        {
            return GetData("data");
        }

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
