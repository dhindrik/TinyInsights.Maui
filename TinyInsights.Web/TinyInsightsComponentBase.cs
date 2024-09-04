using Microsoft.AspNetCore.Components;

namespace TinyInsights.Web;

public abstract class TinyInsightsComponentBase : ComponentBase
{
    [CascadingParameter]
    public bool IsLoggedIn { get; set; }

    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;

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
}