using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MauiInsights.Web;
using MauiInsights.Web.Services;
using Radzen;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddRadzenComponents();

builder.Services.AddHttpClient();

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("https://api.applicationinsights.io/") });

builder.Services.AddSingleton<IInsightsService, InsightsService>();

await builder.Build().RunAsync();