using Microsoft.AspNetCore.Components;

namespace TinyInsights.Web;

public abstract class TinyInsightsComponentBase : ComponentBase
{
    [CascadingParameter]
    public bool IsLoggedIn { get; set; }

    [Inject] 
    public NavigationManager NavigationManager { get; set; } = default!;
}