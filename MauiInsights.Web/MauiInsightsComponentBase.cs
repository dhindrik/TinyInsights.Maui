using Microsoft.AspNetCore.Components;

namespace MauiInsights.Web;

public abstract class MauiInsightsComponentBase : ComponentBase
{
    [CascadingParameter]
    public bool IsLoggedIn { get; set; }

    [Inject] 
    public NavigationManager NavigationManager { get; set; } = default!;
}