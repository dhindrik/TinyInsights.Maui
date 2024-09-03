using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace TinyInsights;

public class ApplicationInsightsProvider : IInsightsProvider, ILogger
{
    private static ApplicationInsightsProvider provider;
    private const string userIdKey = nameof(userIdKey);

    private const string crashLogFilename = "crashes.mauiinsights";

    private readonly string logPath = FileSystem.CacheDirectory;

    private TelemetryClient client;

    private readonly TelemetryConfiguration telemetryConfiguration;

    public bool IsTrackErrorsEnabled { get; set; } = true;
    public bool IsTrackCrashesEnabled { get; set; } = true;
    public bool IsTrackPageViewsEnabled { get; set; } = true;
    public bool IsAutoTrackPageViewsEnabled { get; set; } = true;
    public bool IsTrackEventsEnabled { get; set; } = true;
    public bool IsTrackDependencyEnabled { get; set; } = true;

#if IOS || MACCATALYST || ANDROID
    public ApplicationInsightsProvider(string connectionString)
    {
        provider = this;

        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

        telemetryConfiguration = new TelemetryConfiguration()
        {
            ConnectionString = connectionString
        };

        void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            if(IsTrackCrashesEnabled)
            {
                HandleCrash(e.Exception);
            }
        }

        void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if(IsTrackCrashesEnabled)
            {
                HandleCrash((Exception)e.ExceptionObject);
            }
        }
    }
#elif WINDOWS
    public ApplicationInsightsProvider(MauiWinUIApplication app, string connectionString)
    {
        provider = this;

        app.UnhandledException += App_UnhandledException;

        telemetryConfiguration = new TelemetryConfiguration()
        {
            ConnectionString = connectionString
        };

        void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            if (IsTrackCrashesEnabled)
            {
                HandleCrash(e.Exception);
            }
        }
    }
#endif

    public static bool IsInitialized { get; private set; }
    public void Initialize()
    {
        try
        {
            client = new TelemetryClient(telemetryConfiguration);

            AddMetaData();
        }
        catch(Exception)
        {
            Debug.WriteLine("TinyInsights: Error creating TelemetryClient");
        }

        if(IsInitialized)
        {
            return;
        }

        if(Application.Current is not null && IsAutoTrackPageViewsEnabled)
        {
            WeakEventHandler<Page> weakHandler = new(OnAppearing);
            Application.Current.PageAppearing += weakHandler.Handler;
        }

        Task.Run(SendCrashes);

        IsInitialized = true;
    }

    private static void OnAppearing(object? sender, Page e)
    {
        var pageType = e.GetType();
        provider.TrackPageViewAsync(pageType.FullName ?? pageType.Name, new Dictionary<string, string> { { "DisplayName", pageType.Name } });
    }

    public void UpsertGlobalProperty(string key, string value)
    {
        client.Context.GlobalProperties[key] = value;
    }

    public void OverrideAnonymousUserId(string userId)
    {
        SetUserId(userId);
    }

    public void GenerateNewAnonymousUserId()
    {
        var userId = Guid.NewGuid().ToString();
        SetUserId(userId);
    }

    private void SetUserId(string userId)
    {
        Preferences.Set(userIdKey, userId);

        AddMetaData();
    }

    private void AddMetaData()
    {
        client.Context.Device.OperatingSystem = DeviceInfo.Platform.ToString();
        client.Context.Device.Model = DeviceInfo.Model;
        client.Context.Device.Type = DeviceInfo.Idiom.ToString();

        //Role name will show device name if we don't set it to empty and we want it to be so anonymous as possible.
        client.Context.Cloud.RoleName = string.Empty;
        client.Context.Cloud.RoleInstance = string.Empty;

        if(Preferences.ContainsKey(userIdKey))
        {
            var userId = Preferences.Get(userIdKey, string.Empty);

            client.Context.User.Id = userId;
        }
        else
        {
            GenerateNewAnonymousUserId();
        }

        client.Context.GlobalProperties.TryAdd("Language", CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
        client.Context.GlobalProperties.TryAdd("Manufacturer", DeviceInfo.Manufacturer);
        client.Context.GlobalProperties.TryAdd("AppVersion", AppInfo.VersionString);
        client.Context.GlobalProperties.TryAdd("AppBuildNumber", AppInfo.BuildString);
        client.Context.GlobalProperties.TryAdd("OperatingSystemVersion", DeviceInfo.VersionString);
    }

    private async Task SendCrashes()
    {
        try
        {
            var crashes = ReadCrashes();

            if(crashes is null || crashes.Count == 0)
            {
                return;
            }

            Debug.WriteLine($"TinyInsights: Sending {crashes.Count} crashes");

            foreach(var crash in crashes)
            {
                var ex = crash.GetException();

                if (ex is null)
                {
                    continue;
                }

                var properties = new Dictionary<string, string>
                {
                    { "IsCrash", "true" },
                    { "StackTrace", crash.StackTrace ?? string.Empty },
                    { "ExceptionType", crash.ExceptionType },
                    { "Source", crash.Source ?? string.Empty }
                };

                await TrackErrorAsync(ex, properties);
            }

            ResetCrashes();
        }
        catch(Exception)
        {
            Debug.WriteLine("TinyInsights: Error sending crashes");
        }
    }

    private List<Crash>? ReadCrashes()
    {
        try
        {
            Debug.WriteLine("TinyInsights: Read crashes");

            var path = Path.Combine(logPath, crashLogFilename);

            if(!File.Exists(path))
            {
                return null;
            }

            var json = File.ReadAllText(path);

            return string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<List<Crash>>(json);
        }
        catch(Exception)
        {
            Debug.WriteLine("TinyInsights: Error reading crashes");
        }

        return null;
    }

    private void ResetCrashes()
    {
        try
        {
            Debug.WriteLine("TinyInsights: Reset crashes");

            var path = Path.Combine(logPath, crashLogFilename);
            File.Delete(path);
        }
        catch(Exception)
        {
            Debug.WriteLine("TinyInsights: Error clearing crashes");
        }
    }

    private void HandleCrash(Exception ex)
    {
        try
        {
            Debug.WriteLine("TinyInsights: Handle crashes");

            var crashes = ReadCrashes() ?? [];

            crashes.Add(new Crash(ex));

            var json = JsonSerializer.Serialize(crashes);

            var path = Path.Combine(logPath, crashLogFilename);

            File.WriteAllText(path, json);
        }
        catch(Exception)
        {
            Debug.WriteLine("TinyInsights: Error handling crashes");
        }
    }

    public Task TrackErrorAsync(Exception ex, Dictionary<string, string>? properties = null)
    {
        try
        {
            Debug.WriteLine($"TinyInsights: Tracking error {ex.Message}");

            properties ??= [];

            if(ex.StackTrace is not null)
            {
                properties.TryAdd("StackTrace", ex.StackTrace);
            }

            client.TrackException(ex, properties);
            client.Flush();
        }
        catch(Exception)
        {
            Debug.WriteLine("TinyInsights: Error tracking error");
        }

        return Task.CompletedTask;
    }

    public Task TrackEventAsync(string eventName, Dictionary<string, string>? properties = null)
    {
        try
        {
            Debug.WriteLine($"TinyInsights: Tracking event {eventName}");

            client.TrackEvent(eventName, properties);
            client.Flush();
        }
        catch(Exception)
        {
            Debug.WriteLine("TinyInsights: Error tracking event");
        }

        return Task.CompletedTask;
    }

    public Task TrackPageViewAsync(string viewName, Dictionary<string, string>? properties = null)
    {
        try
        {
            Debug.WriteLine($"TinyInsights: tracking page view {viewName}");

            client.TrackPageView(viewName);
            client.Flush();
        }
        catch(Exception)
        {
            Debug.WriteLine("TinyInsights: Error tracking page view");
        }

        return Task.CompletedTask;
    }

    public Task TrackDependencyAsync(string dependencyType, string dependencyName, string data, HttpMethod? httpMethod, DateTimeOffset startTime, TimeSpan duration, bool success, int resultCode = 0, Exception? exception = null)
    {
        try
        {
            Debug.WriteLine($"TinyInsights: Tracking dependency {dependencyName}");

            var fullUrl = data;

            if(data.Contains('?'))
            {
                var split = data.Split("?");
                data = split[0];
            }

            var dependency = new DependencyTelemetry()
            {
                Type = dependencyType,
                Name = dependencyName,
                Data = data,
                Timestamp = startTime,
                Success = success,
                Duration = duration,
                ResultCode = resultCode.ToString(),
            };

            dependency.Properties.Add("FullUrl", fullUrl);

            if(httpMethod is not null)
            {
                dependency.Properties.Add("HttpMethod", httpMethod.ToString());
            }

            if(exception is not null)
            {
                dependency.Properties.Add("ExceptionMessage", exception.Message);
                dependency.Properties.Add("StackTrace", exception.StackTrace);

                if(exception.InnerException is not null)
                {
                    dependency.Properties.Add("InnerExceptionMessage", exception.InnerException.Message);
                    dependency.Properties.Add("InnerExceptionStackTrace", exception.InnerException.StackTrace);
                }
            }

            client.TrackDependency(dependency);
        }
        catch(Exception)
        {
            Debug.WriteLine("TinyInsights: Error tracking dependency");
        }

        return Task.CompletedTask;
    }

    public Task TrackDependencyAsync(string dependencyType, string dependencyName, string data, DateTimeOffset startTime, TimeSpan duration, bool success, int resultCode = 0, Exception? exception = null)
    {
        return TrackDependencyAsync(dependencyType, dependencyName, data, null, startTime, duration, success, resultCode, exception);
    }

    #region ILogger

    public async void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if(!IsEnabled(logLevel))
        {
            return;
        }

        var logTask = logLevel switch
        {
            LogLevel.Trace => TrackPageViewAsync(GetEventName(eventId), GetLoggerData(logLevel, eventId, state, exception, formatter)),
            LogLevel.Debug => TrackDebugAsync(eventId, state, exception),
            LogLevel.Information => TrackEventAsync(GetEventName(eventId), GetLoggerData(logLevel, eventId, state, exception, formatter)),
            LogLevel.Warning => TrackErrorAsync(exception!, GetLoggerData(logLevel, eventId, state, exception, formatter)),
            LogLevel.Error => TrackErrorAsync(exception!, GetLoggerData(logLevel, eventId, state, exception, formatter)),
            LogLevel.Critical => TrackErrorAsync(exception!, GetLoggerData(logLevel, eventId, state, exception, formatter)),
            LogLevel.None => Task.CompletedTask,
            _ => Task.CompletedTask
        };

        await logTask;
    }

    private static Task TrackDebugAsync<TState>(EventId eventId, TState state, Exception? exception)
    {
        Debug.WriteLine($"TinyInsights: DebugLogging, Event: {GetEventName(eventId)}, State: {state}, Exception: {exception?.Message}");
        return Task.CompletedTask;
    }

    private static string GetEventName(EventId eventId)
    {
        return eventId.Name ?? eventId.Id.ToString();
    }

    private static Dictionary<string, string> GetLoggerData<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        return new Dictionary<string, string>()
        {
            { "LogLevel", logLevel.ToString() },
            { "EventId", eventId.ToString() },
            { "EventName", eventId.Name?.ToString() ?? string.Empty },
            { "State", state?.ToString() ?? string.Empty },
            { "Message", formatter(state, exception) }
        };
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel switch
        {
            LogLevel.Trace => IsTrackPageViewsEnabled,
            LogLevel.Debug => Debugger.IsAttached,
            LogLevel.Information => IsTrackEventsEnabled,
            LogLevel.Warning => IsTrackErrorsEnabled,
            LogLevel.Error => IsTrackErrorsEnabled,
            LogLevel.Critical => IsTrackCrashesEnabled,
            LogLevel.None => false,
            _ => false
        };
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => default!;

    #endregion ILogger
}