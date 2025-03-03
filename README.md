# TinyInsights

TinyInsights is a library for tracking and a website for showing insights for your .NET MAUI app. Data will be saved in Application Insights. The website will help you to show the data in a more mobile app-friendly user interface.

You can try out the website here, https://mauiinsights.z6.web.core.windows.net/. No data is saved in the application. This web assembly page can be used until March 2026, when access with API-key will stop working. After that you have to set up a application in Entra. For about how to setup the website for your application, read this, **[How to set up and configure the web site](docs/SetupServerSite.md)**.

## Add TinyInsights to your app
Install the latest version of the package ***TinyInsights.Maui.AppInsights*** to your app project.

```
dotnet add package TinyInsights.Maui.AppInsights
```

### Installation
In ***MauiProgram.cs***, you should call ***UseTinyInsights*** like below.
```csharp
var builder = MauiApp.CreateBuilder();
builder
  .UseMauiApp<App>()
  .ConfigureFonts(fonts =>
    {
      fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
      fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
     })
  .UseTinyInsights("{YOUR_CONNECTION_STRING}")
```

If you want, you can configure what type of events you want to track.
```csharp
builder.UseTinyInsights("{YOUR_CONNECTION_STRING}",
        (provider) =>
        {
            provider.IsTrackDependencyEnabled = true;
            provider.IsTrackEventsEnabled = true;
            provider.IsTrackErrorsEnabled = true;
            provider.IsTrackPageViewsEnabled = true;
            provider.IsTrackCrashesEnabled = true;           
        });
```

The package comes with a default implementation of a crash handler that stores crash data in a json file before application is terminated.
On the next application run the data will be deserialized from the json file and submitted to Azure Insights.
You can also replace it by your own implementation of `ICrashHandler` interface:
```csharp
.UseTinyInsights("{YOUR_CONNECTION_STRING}", (provider) =>
	{            
		provider.SetCrashHandler(MyOwnCrashHandler);
	});
```

### Track events
Crashes will be tracked automatically if it is enabled, other events you need to track manually. 

To get access to the insights provider that you should use to track events, you should inject the ***IInsights*** interface.
```csharp
private readonly IInsights insights;

public MainViewModel(IInsights insights)
{
  this.insights = insights;
}
```

#### Track sessions
When the app starts, a new session will be created. To create a new session, you should call the method below in the ***OnResume*** method in the ***App.xaml.cs*** file. This will create a new session every time the user is returning to the app.

```csharp
protected override void OnResume()
{
    base.OnResume();

    var insights = serviceProvider.GetRequiredService<IInsights>();
    insights.CreateNewSession();
}
```

#### Track page views
```csharp
await insights.TrackPageViewAsync("MainView");
```

#### Track custom events
```csharp
await insights.TrackEventAsync("AddButtonTapped");
```

#### Track exceptions
```csharp
catch (Exception ex)
{
    await insights.TrackErrorAsync(ex);
}
```

#### Track additional data
For all the track methods, you can pass additional data by passing a Dictionary.
```csharp
var data = new Dictionary<string, string>()
          {           
              {"key", "value"},
              {"key2", "value2"}
          };

await insights.TrackPageViewAsync("MainView", data);
```

#### Track dependencies
To automatically track HTTP calls you can use the ***InsightsMessageHandler*** together with the HttpClient.
```csharp
private readonly InsightsMessageHandler insightsMessageHandler;
private readonly HttpClient client;

public MainViewModel(InsightsMessageHandler insightsMessageHandler)
{
  this.insightsMessageHandler = insightsMessageHandler;
  client = new HttpClient(insightsMessageHandler);
}
```
If you don't want to track all dependencies, you can specify a filter when setting up TinyInsights.  

```csharp
.UseTinyInsights("{CONNECTION-STRING}", (provider) =>
{
    provider.TrackDependencyFilter = (dependency) =>
    {
        return !dependency.Success && dependency.ResultCode != 401;
    };
});

```


You can also create a DependencyTracker and use it to track dependencies.
```csharp
using var dependency = insights.CreateDependencyTracker("BLOB",blobContainer.Uri.Host, url);
await blob.DownloadToAsync(stream);
```
or if you don't want to wait for the scope of the using.
```csharp
var dependency = insights.CreateDependencyTracker("BLOB",blobContainer.Uri.Host, url);
await blob.DownloadToAsync(stream);
dependency.Dispose();
```

#### UserId
By default a random UserId is generated for each user. If you want to set a specific UserId you can do it like below.
```csharp
ìnsights.OverrideAnonymousUserId("MyOwnUserId");
```

To generate a new random UserId you can call the method below.
```csharp
ìnsights.GenerateNewAnonymousUserId();
```

### Set and update global properties
It is possible to set/update global properties with the UpsertGlobalProperties method.
```csharp
insights.UpsertGlobalProperty("language", "sv-SE");
````

For predefined properties on the Client object, like RoleName you can use, Cloud.RoleName as key.
```csharp
insights.UpsertGlobalProperty("Cloud.RoleName", "MyRoleName");
````
Right now Cloud.RoleName, Cloud.RoleInstance, Device.Id, Device.Type, Device.Model and Device.OperatingSystem are supported to change that way.


## Use with ILogger
If you want, you can also use TinyInsights with the ILogger interface.

In ***MauiProgram.cs***, you should call ***UseTinyInsights*** like below.
```csharp
var builder = MauiApp.CreateBuilder();
builder
  .UseMauiApp<App>()
  .ConfigureFonts(fonts =>
    {
      fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
      fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
     })
  .UseTinyInsightsAsILogger("{YOUR_CONNECTION_STRING}")
```

If you want, you can configure what type of events you want to track.
```csharp
builder.UseTinyInsightsAsILogger("{YOUR_CONNECTION_STRING}",
        (provider) =>
        {
            provider.IsTrackDependencyEnabled = true;
            provider.IsTrackEventsEnabled = true;
            provider.IsTrackErrorsEnabled = true;
            provider.IsTrackPageViewsEnabled = true;
            provider.IsTrackCrashesEnabled = true;
            provider.IsAutoTrackPageViewsEnabled = true;           
        });
```

### Use ILogger
To use **ILogger**, just inject the interface in the class you want to use it in.

```csharp
private readonly ILogger logger;

public class MainViewModel(ILogger logger)
{
    this.logger = logger;
}
```

#### Track page views
By default, page views are automatically tracked. But you can turn that off, and do it manually if you prefer.
```csharp
logger.LogTrace("MainView");
```

#### Track event
```csharp
logger.LogInformation("EventButton");
```

#### Track error
```csharp
 logger.LogError(ex, ex.Message);
 ```

#### Track debug info

 **LogDebug** will only work with the debugger attached because it is using **Debug.WriteLine**.

 ```csharp
logger.LogDebug("Debug message");
```
