using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using TinyInsights.Web;
using TinyInsights.Web.Services;
using Microsoft.AspNetCore.Components;
using Radzen;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddRadzenComponents();

builder.Services.AddHttpClient();

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("https://api.applicationinsights.io/") });

builder.Services.AddSingleton<IInsightsService, InsightsService>();

builder.Services.AddCascadingValue( x =>
{
    var filter = new GlobalFilter();
    var source = new CascadingValueSource<GlobalFilter>(filter, isFixed: false);

    filter.PropertyChanged += (sender, eventArgs) => source.NotifyChangedAsync();

    return source;
});

await builder.Build().RunAsync();