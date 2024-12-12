using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Radzen;
using TinyInsights.Web;
using TinyInsights.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddRadzenComponents();

builder.Services.AddHttpClient();

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("https://api.applicationinsights.io/") });

builder.Services.AddSingleton<IInsightsService, InsightsService>();
builder.Services.AddSingleton<GlobalFilter>();

builder.Services.AddCascadingValue(provider =>
{
    var filter = provider.GetRequiredService<GlobalFilter>();
    var source = new CascadingValueSource<GlobalFilter>(filter, isFixed: false);

    filter.PropertyChanged += (sender, eventArgs) => source.NotifyChangedAsync();

    return source;
});

builder.Services.AddBlazoredLocalStorageAsSingleton();

await builder.Build().RunAsync();