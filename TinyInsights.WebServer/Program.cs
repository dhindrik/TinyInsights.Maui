using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Components;
using Microsoft.Identity.Web;
using Radzen;
using TinyInsights.Web;
using TinyInsights.Web.Services;

var builder = WebApplication.CreateBuilder(args);

#if DEBUG
builder.Configuration.AddJsonFile("appsettings.local.json");
#endif

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
         .AddMicrosoftIdentityWebApp(builder.Configuration, "AzureAd")
           .EnableTokenAcquisitionToCallDownstreamApi(["https://api.applicationinsights.io/.default"])
           .AddInMemoryTokenCaches();

builder.Services.AddControllers();

builder.Services.AddRadzenComponents();

builder.Services.AddHttpClient();

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("https://api.applicationinsights.io/") });

builder.Services.AddScoped<IInsightsService, InsightsService>();
builder.Services.AddScoped<GlobalFilter>();

builder.Services.AddCascadingValue(provider =>
{
    var filter = provider.GetRequiredService<GlobalFilter>();
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

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapRazorComponents<TinyInsights.WebServer.Components.App>()
    .AddInteractiveServerRenderMode().AddAdditionalAssemblies(typeof(TinyInsights.Web._Imports).Assembly); ;


app.Run();