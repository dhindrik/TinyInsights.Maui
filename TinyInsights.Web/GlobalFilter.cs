using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TinyInsights.Web;
public class GlobalFilter : INotifyPropertyChanged
{
    public const string AppVersionsDefaultValue = "All app versions";

    private int numberOfDays = 30;
    public int NumberOfDays
    {
        get => numberOfDays;
        set => SetField(ref numberOfDays, value);
    }

    private string operatingSystem = "all";
    public string OperatingSystem
    {
        get => operatingSystem;
        set => SetField(ref operatingSystem, value);
    }

    public string? OperatingSystemFilterValue => OperatingSystem == "all" ? null : OperatingSystem;

    private List<string> appVersions = new() {
        AppVersionsDefaultValue
    };

    public List<string> AllAppVersions {get;set;} = new();

    public List<string> AppVersions
    {
        get => appVersions;
        set => SetField(ref appVersions, value);
    }

    public List<string> AppBuildNumbers{
        get
        {
            return AppVersions.Select(v => v.Split('(').Last().Trim(')')).ToList();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        if(propertyName == nameof(AppVersions))
        {
            if(AppVersions.Count == 0)
            {
                AppVersions.Add(AppVersionsDefaultValue);
            }
            else if(AppVersions.Count > 1 && AppVersions.Contains(AppVersionsDefaultValue))
            {
                AppVersions.Remove(AppVersionsDefaultValue);
            }
            else if(AppVersions.Count == AllAppVersions.Count)
            {
                AppVersions.Clear();
                AppVersions.Add(AppVersionsDefaultValue);
            }
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}