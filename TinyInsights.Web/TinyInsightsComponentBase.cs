using Microsoft.AspNetCore.Components;

namespace TinyInsights.Web;

public abstract class TinyInsightsComponentBase : ComponentBase
{
    protected CancellationTokenSource cancellationTokenSource = new();

    [CascadingParameter]
    public bool IsLoggedIn { get; set; }

    [CascadingParameter]
    public required GlobalFilter GlobalFilter { get; set; }

    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;

    public CancellationToken CancellationToken { get; set; } = default!;

    protected void HandleException(Exception ex)
    {
        if (ex is UnauthorizedAccessException)
        {
            if (OperatingSystem.IsBrowser())
            {
                NavigationManager.NavigateTo("/");
            }
            else
            {
                NavigationManager.NavigateTo("/login");
            }

        }
    }

    protected (DateOnly StartDate, int NumberOfDays) GetDateRangeFromFilter()
    {
        var date = DateOnly.MinValue;
        var numberOfDays = GlobalFilter.NumberOfDays;

        if (GlobalFilter.DateFilter is not null)
        {
            date = GlobalFilter.DateFilter.StartDate;
            numberOfDays = GlobalFilter.DateFilter.EndDate.DayNumber - GlobalFilter.DateFilter.StartDate.DayNumber;
            numberOfDays++;  //to also include the end date in the range
            return (date, numberOfDays);
        }

        //(GlobalFilter.NumberOfDays-1) so we full days
        date = DateOnly.FromDateTime(DateTime.Now).AddDays(-(GlobalFilter.NumberOfDays - 1));
        return (date, numberOfDays);
    }
}

public abstract class TinyInsightsPageComponentBase : TinyInsightsComponentBase
{


    protected override void OnInitialized()
    {
        base.OnInitialized();

        NavigationManager.LocationChanged += (sender, args) =>
        {
            CancelCurrentOperation();
        };
    }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        GlobalFilter.PropertyChanged += (sender, args) =>
        {
            CancelCurrentOperation();
        };
    }

    protected void CancelCurrentOperation()
    {
        if (cancellationTokenSource != null && !cancellationTokenSource.IsCancellationRequested)
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource = new CancellationTokenSource();
            CancellationToken = cancellationTokenSource.Token;
        }
    }
}