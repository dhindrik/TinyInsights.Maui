namespace TinyInsights.Web.Services.Models;

public class FailedDependencyItem : ErrorItem
{
    public FailedDependencyItem(Dictionary<string, string?> data) : base(data)
    {
        Data = data;
    }

    public int ResultCode => int.Parse(GetData("resultCode") ?? "0");
    public string? Method => GetData("HttpMethod");
    public string? FullUrl => GetData("FullUrl");
}