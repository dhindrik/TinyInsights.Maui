namespace TinyInsights.Web.Services.Models;

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
    public string? ClientOsVersion
    {
        get
        {
            var os = GetData("OperatingSystemVersion");

            if (os != null)
            {
                return os;
            }

            // For android it can be forexample Android 14.
            var browser = GetData("client_Browser");

            if (browser != null)
            {
                var split = browser.Split(" ");
                if (split.Length > 1 && double.TryParse(split[1], out var version))
                {
                    return version.ToString();
                }
            }

            return null;
        }
    }
    public string? ClientCity => GetData("client_City");
    public string? ClientState => GetData("client_StateOrProvince");
    public string? ClientCountry => GetData("client_CountryOrRegion");
    public DateTime Timestamp => DateTime.Parse(GetData("timestamp")!);

    public string? StackTrace => GetData("StackTrace");
    public string? Message => GetData("outerMessage");

    protected string? GetData(string key)
    {
        return Data.GetValueOrDefault(key);
    }

    public bool IsSelected { get; set; }
}
