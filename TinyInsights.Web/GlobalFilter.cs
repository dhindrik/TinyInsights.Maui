using Blazored.LocalStorage;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TinyInsights.Web;

public class GlobalFilter : INotifyPropertyChanged
{
    public const string AppVersionsDefaultValue = "All app versions";

    private readonly ILocalStorageService localStorageService;

    public GlobalFilter(ILocalStorageService localStorageService)
    {
        this.localStorageService = localStorageService;

        _ = Initialize();
    }

    private async Task Initialize()
    {
        if (await localStorageService.ContainKeyAsync(nameof(GlobalFilter)))
        {
            var filter = await localStorageService.GetItemAsync<FilterModel>(nameof(GlobalFilter));

            if (filter is not null)
            {
                NumberOfDays = filter.NumberOfDays;
                TextFilter = filter.TextFilter;
            }
        }
    }

    public async Task RestoreDayFilter()
    {

        if (await localStorageService.ContainKeyAsync(nameof(GlobalFilter)))
        {
            var filter = await localStorageService.GetItemAsync<FilterModel>(nameof(GlobalFilter));

            if (filter is not null)
            {
                NumberOfDays = filter.NumberOfDays;
            }
        }
    }

    private int numberOfDays = 30;
    public int NumberOfDays
    {
        get => numberOfDays;
        set => SetField(ref numberOfDays, value);
    }

    private DateFilter? dateFilter;
    public DateFilter? DateFilter
    {
        get => dateFilter;
        set => SetField(ref dateFilter, value);
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

    public List<string> AllAppVersions { get; set; } = new();

    public List<string> AppVersions
    {
        get => appVersions;
        set => SetField(ref appVersions, value);
    }

    public string? TextFilter { get; set; }

    public void ApplyTextFilter()
    {
        OnPropertyChanged(nameof(TextFilter));
    }

    public List<string> AppBuildNumbers
    {
        get
        {
            return AppVersions.Select(v => v.Split('(').Last().Trim(')')).ToList();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual async void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        if (propertyName == nameof(NumberOfDays) && NumberOfDays == -1)
        {
            return;
        }
        else if (propertyName == nameof(NumberOfDays))
        {
            dateFilter = null;
        }

        if (propertyName == nameof(AppVersions))
        {
            if (AppVersions.Count == 0)
            {
                AppVersions.Add(AppVersionsDefaultValue);
            }
            else if (AppVersions.Count > 1 && AppVersions.Contains(AppVersionsDefaultValue))
            {
                AppVersions.Remove(AppVersionsDefaultValue);
            }
            else if (AppVersions.Count == AllAppVersions.Count)
            {
                AppVersions.Clear();
                AppVersions.Add(AppVersionsDefaultValue);
            }
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        if (propertyName == nameof(NumberOfDays) || propertyName == nameof(TextFilter))
        {
            var daysToSave = NumberOfDays;

            //Use last saved value if custom filter
            if (daysToSave == -1)
            {
                var filter = await localStorageService.GetItemAsync<FilterModel>(nameof(GlobalFilter));

                if (filter is not null)
                {
                    daysToSave = filter.NumberOfDays;
                }
            }

            _ = localStorageService.SetItemAsync(nameof(GlobalFilter), new FilterModel()
            {
                NumberOfDays = daysToSave,
                TextFilter = TextFilter
            });
        }
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

public record DateFilter(DateOnly StartDate, DateOnly EndDate);

public class FilterModel
{
    public string? TextFilter { get; set; }

    public int NumberOfDays { get; set; }
}