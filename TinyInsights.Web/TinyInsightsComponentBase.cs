using Microsoft.AspNetCore.Components;

namespace TinyInsights.Web;

public abstract class TinyInsightsComponentBase : ComponentBase
{
    private CancellationTokenSource cancellationTokenSource = new();

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