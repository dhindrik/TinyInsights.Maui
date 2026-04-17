---
title: "TinyInsights.Maui"
description: "Instructions for using TinyInsights, an Application Insights helper library for .NET MAUI apps."
applyTo: "**/*.cs"
---

# TinyInsights for .NET MAUI

TinyInsights is a library for tracking insights in .NET MAUI apps using Application Insights as the backend. This project already has the `TinyInsights.Maui.AppInsights` NuGet package installed.

## Setup in MauiProgram.cs

Register TinyInsights in the MAUI app builder inside `MauiProgram.cs` by calling `UseTinyInsights` with your Application Insights connection string:

```csharp
var builder = MauiApp.CreateBuilder();
builder
  .UseMauiApp<App>()
  .UseTinyInsights("{YOUR_CONNECTION_STRING}");
```

### Configure tracking options

You can optionally configure which event types to track:

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

### Use with ILogger

Alternatively, use `UseTinyInsightsAsILogger` to integrate with the standard `ILogger` interface:

```csharp
builder.UseTinyInsightsAsILogger("{YOUR_CONNECTION_STRING}");
```

With optional configuration:

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

## Tracking Events with IInsights

Inject `IInsights` via dependency injection to track events:

```csharp
private readonly IInsights insights;

public MainViewModel(IInsights insights)
{
    this.insights = insights;
}
```

### Track page views

```csharp
await insights.TrackPageViewAsync("MainView");
```

### Track custom events

```csharp
await insights.TrackEventAsync("AddButtonTapped");
```

### Track exceptions

```csharp
catch (Exception ex)
{
    await insights.TrackErrorAsync(ex);
}
```

### Track with additional data

All track methods accept an optional `Dictionary<string, string>` for additional properties:

```csharp
var data = new Dictionary<string, string>()
{
    {"key", "value"},
    {"key2", "value2"}
};

await insights.TrackPageViewAsync("MainView", data);
```

## Tracking Events with ILogger

Inject `ILogger` via dependency injection:

```csharp
private readonly ILogger logger;

public MainViewModel(ILogger logger)
{
    this.logger = logger;
}
```

- **Page views** (auto-tracked by default, or manually): `logger.LogTrace("MainView");`
- **Custom events**: `logger.LogInformation("EventButton");`
- **Errors**: `logger.LogError(ex, ex.Message);`
- **Debug** (only with debugger attached): `logger.LogDebug("Debug message");`

## Dependency Tracking

### Automatic HTTP tracking

Use `InsightsMessageHandler` with `HttpClient` to automatically track HTTP calls:

```csharp
private readonly InsightsMessageHandler insightsMessageHandler;
private readonly HttpClient client;

public MainViewModel(InsightsMessageHandler insightsMessageHandler)
{
    this.insightsMessageHandler = insightsMessageHandler;
    client = new HttpClient(insightsMessageHandler);
}
```

### Dependency filter

Filter which dependencies to track:

```csharp
.UseTinyInsights("{CONNECTION-STRING}", (provider) =>
{
    provider.TrackDependencyFilter = (dependency) =>
    {
        return !dependency.Success && dependency.ResultCode != 401;
    };
});
```

### Manual dependency tracking

Use `CreateDependencyTracker` for non-HTTP dependencies:

```csharp
using var dependency = insights.CreateDependencyTracker("BLOB", blobContainer.Uri.Host, url);
await blob.DownloadToAsync(stream);
```

Or dispose manually:

```csharp
var dependency = insights.CreateDependencyTracker("BLOB", blobContainer.Uri.Host, url);
await blob.DownloadToAsync(stream);
dependency.Dispose();
```

## Session Management

Sessions are created automatically on app start. To create new sessions when the user returns to the app, call `CreateNewSession` in `App.xaml.cs`:

```csharp
protected override void OnResume()
{
    base.OnResume();

    var insights = serviceProvider.GetRequiredService<IInsights>();
    insights.CreateNewSession();
}
```

## Crash Handling

Crashes are tracked automatically when `IsTrackCrashesEnabled` is true.

### Crash callbacks

The `ApplicationInsightsProvider` exposes two callbacks for crash handling:

- **`AfterCrash`** – `Action<Dictionary<string,string>>` invoked immediately after a crash is captured and stored.
- **`BeforeSendCrash`** – `Func<Dictionary<string,string>,Task>` awaited before stored crashes are sent on next app launch.

```csharp
.UseTinyInsights("{CONNECTION-STRING}", (provider) =>
{
    if (provider is ApplicationInsightsProvider appProvider)
    {
        appProvider.AfterCrash = (globalProperties) =>
        {
            // Runs right after a crash is captured.
        };
        appProvider.BeforeSendCrash = async (telemetryProperties) =>
        {
            // Runs before stored crashes are sent to Application Insights.
            await Task.CompletedTask;
        };
    }
});
```

## User Identity

By default a random UserId is generated. Override it or regenerate:

```csharp
insights.OverrideAnonymousUserId("MyOwnUserId");
insights.GenerateNewAnonymousUserId();
```

## Global Properties

Set or update global properties attached to all telemetry:

```csharp
insights.UpsertGlobalProperty("language", "sv-SE");
```

For predefined client properties, use the dotted key name:

```csharp
insights.UpsertGlobalProperty("Cloud.RoleName", "MyRoleName");
```

Supported predefined keys: `Cloud.RoleName`, `Cloud.RoleInstance`, `Device.Id`, `Device.Type`, `Device.Model`, `Device.OperatingSystem`.

## Offline Support

By default TinyInsights uses `MemoryTelemetryChannel` from the Application Insights SDK, which stores events in memory. This works for short offline periods, but events are lost if the OS terminates the app before they are sent.

To persist events to disk and survive app restarts, set the `TelemetryChannel` property to `ServerTelemetryChannel`:

```csharp
.UseTinyInsights("{CONNECTION-STRING}", (provider) =>
{
    if (provider is ApplicationInsightsProvider appProvider)
    {
        appProvider.TelemetryChannel = new ServerTelemetryChannel();
    }
});
```

> **Note:** The Application Insights SDK batches data and only publishes when enough data is collected, so there may be a delay before data appears in Application Insights.

## Flush Behavior

Since version 1.8.0, events are no longer flushed on every call. Call `FlushAsync` before the app goes to sleep to avoid data loss:

```csharp
protected async override void OnSleep()
{
    base.OnSleep();

    var insights = serviceProvider.GetRequiredService<IInsights>();
    await insights.FlushAsync();
}
```

## Key Types and Interfaces

- **`IInsights`** – Main interface for tracking. Inject via DI.
- **`IInsightsProvider`** – Provider interface. `ApplicationInsightsProvider` is the built-in implementation.
- **`InsightsMessageHandler`** – `DelegatingHandler` for automatic HTTP dependency tracking.
- **`Dependency`** – Disposable tracker returned by `CreateDependencyTracker`.
- **`InsightsExtension`** – Extension methods: `UseTinyInsights` and `UseTinyInsightsAsILogger` on `MauiAppBuilder`.
