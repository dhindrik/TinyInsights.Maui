using Microsoft.AspNetCore.Components;
using Radzen;
using TinyInsights.Web;
using TinyInsights.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddRadzenComponents();

builder.Services.AddHttpClient();

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("https://api.applicationinsights.io/") });

builder.Services.AddSingleton<IInsightsService, InsightsService>();

builder.Services.AddCascadingValue(x =>
{
    var filter = new GlobalFilter();
    var source = new CascadingValueSource<GlobalFilter>(filter, isFixed: false);

    filter.PropertyChanged += (sender, eventArgs) => source.NotifyChangedAsync();

    return source;
});


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<TinyInsights.WebServer.Components.App>()
    .AddInteractiveServerRenderMode().AddAdditionalAssemblies(typeof(TinyInsights.Web._Imports).Assembly); ;

app.Run();
