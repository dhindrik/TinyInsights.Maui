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
    private readonly string _connectionString;
    private static ApplicationInsightsProvider provider;
    private const string userIdKey = nameof(userIdKey);

    private const string crashLogFilename = "crashes.mauiinsights";

    private readonly string logPath = FileSystem.CacheDirectory;

    private TelemetryClient? _client;
    private TelemetryClient? Client => _client ?? CreateTelemetryClient();

    public bool IsTrackErrorsEnabled { get; set; } = true;
    public bool IsTrackCrashesEnabled { get; set; } = true;
    public bool IsTrackPageViewsEnabled { get; set; } = true;
    public bool IsAutoTrackPageViewsEnabled { get; set; } = true;
    public bool IsTrackEventsEnabled { get; set; } = true;
    public bool IsTrackDependencyEnabled { get; set; } = true;

#if IOS || MACCATALYST || ANDROID

    public ApplicationInsightsProvider(string connectionString)
    {
        _connectionString = connectionString;
        provider = this;

        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

        void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            if (IsTrackCrashesEnabled)
            {
                HandleCrash(e.Exception);
            }
        }

        void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (IsTrackCrashesEnabled)
            {
                HandleCrash((Exception)e.ExceptionObject);
            }
        }
    }

#elif WINDOWS
    public ApplicationInsightsProvider(MauiWinUIApplication app, string connectionString)
    {
        _connectionString = connectionString;
        provider = this;

        app.UnhandledException += App_UnhandledException;

        void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            if (IsTrackCrashesEnabled)
            {
                HandleCrash(e.Exception);
            }
        }
    }
#elif NET8_0_OR_GREATER
    public ApplicationInsightsProvider(string connectionString)
    {
        // Do nothing. The net8.0 target exists for enabling unit testing, not for actual use.
    }
#endif

    public static bool IsInitialized { get; private set; }

    public void Initialize()
    {
        CreateTelemetryClient();

        if (IsInitialized)
        {
            return;
        }

        if (IsAutoTrackPageViewsEnabled)
        {
            if (Application.Current is null)
            {
                throw new NullReferenceException("Unable to configure `IsAutoTrackPageViewsEnabled` as `Application.Current` is null. You can either set `IsAutoTrackPageViewsEnabled` to false to ignore this issue, or check out this link for a possible reason - https://github.com/dhindrik/TinyInsights.Maui/issues/21");
            }
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

    readonly Dictionary<string, string> _globalProperties = [];

    private TelemetryClient? CreateTelemetryClient()
    {
        if (_client is not null)
        {
            return _client;
        }

        try
        {
            var configuration = new TelemetryConfiguration()
            {
                ConnectionString = _connectionString
            };

            _client = new TelemetryClient(configuration);

            _client.Context.Device.OperatingSystem = DeviceInfo.Platform.ToString();
            _client.Context.Device.Model = DeviceInfo.Model;
            _client.Context.Device.Type = DeviceInfo.Idiom.ToString();

            // Role name will show device name if we don't set it to empty and we want it to be so anonymous as possible.
            _client.Context.Cloud.RoleName = string.Empty;
            _client.Context.Cloud.RoleInstance = string.Empty;
            _client.Context.User.Id = GetUserId();

            // Add any global properties, the user has already added
            foreach (var property in _globalProperties)
            {
                _client.Context.GlobalProperties[property.Key] = property.Value;
            }

            _client.Context.GlobalProperties.TryAdd("Language", CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
            _client.Context.GlobalProperties.TryAdd("Manufacturer", DeviceInfo.Manufacturer);
            _client.Context.GlobalProperties.TryAdd("AppVersion", AppInfo.VersionString);
            _client.Context.GlobalProperties.TryAdd("AppBuildNumber", AppInfo.BuildString);
            _client.Context.GlobalProperties.TryAdd("OperatingSystemVersion", DeviceInfo.VersionString);

            Task.Run(SendCrashes);

            return _client;
        }
        catch (Exception)
        {
            Debug.WriteLine("TinyInsights: Error creating TelemetryClient");
        }

        return null;
    }

    public void UpsertGlobalProperty(string key, string value)
    {
        if (Client is null)
        {
            return;
        }

        switch (key)
        {
            case "Cloud.RoleName":
                Client.Context.Cloud.RoleName = value;
                return;
            case "Cloud.RoleInstance":
                Client.Context.Cloud.RoleInstance = value;
                return;

            case "Device.OperatingSystem":
                Client.Context.Device.OperatingSystem = value;
                return;
            case "Device.Model":
                Client.Context.Device.Model = value;
                return;
            case "Device.Type":
                Client.Context.Device.Type = value;
                return;
            case "Device.Id":
                Client.Context.Device.Id = value;
                return;
        }

        _globalProperties[key] = value;

        Client.Context.GlobalProperties[key] = value;

    }

    public void OverrideAnonymousUserId(string userId)
    {
        Preferences.Set(userIdKey, userId);
        if (Client is not null)
        {
            Client.Context.User.Id = userId;
        }
    }

    private string GetUserId()
    {
        var userId = Preferences.Get(userIdKey, null);

        return userId ?? GenerateNewAnonymousUserId();
    }

    public string GenerateNewAnonymousUserId()
    {
        var userId = Guid.NewGuid().ToString();
        Preferences.Set(userIdKey, userId);

        return userId;
    }

    private async Task SendCrashes()
    {
        try
        {
            if (Client is null)
            {
                return;
            }

            var crashes = ReadCrashes();

            if (crashes is null || crashes.Count == 0)
            {
                return;
            }

            Debug.WriteLine($"TinyInsights: Sending {crashes.Count} crashes");

            foreach (var crash in crashes)
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
        catch (Exception)
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

            if (!File.Exists(path))
            {
                return null;
            }

            var json = File.ReadAllText(path);

            return string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<List<Crash>>(json);
        }
        catch (Exception)
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
        catch (Exception)
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
        catch (Exception)
        {
            Debug.WriteLine("TinyInsights: Error handling crashes");
        }
    }

    public Task TrackErrorAsync(Exception ex, Dictionary<string, string>? properties = null)
    {
        try
        {
            if (Client is null)
            {
                return Task.CompletedTask;
            }

            Debug.WriteLine($"TinyInsights: Tracking error {ex.Message}");

            properties ??= [];

            if (ex.StackTrace is not null)
            {
                properties.TryAdd("StackTrace", ex.StackTrace);
            }

            Client.TrackException(ex, properties);
            Client.Flush();
        }
        catch (Exception)
        {
            Debug.WriteLine("TinyInsights: Error tracking error");
        }

        return Task.CompletedTask;
    }

    public Task TrackEventAsync(string eventName, Dictionary<string, string>? properties = null)
    {
        try
        {
            if (Client is null)
            {
                return Task.CompletedTask;
            }

            Debug.WriteLine($"TinyInsights: Tracking event {eventName}");

            Client.TrackEvent(eventName, properties);
            Client.Flush();
        }
        catch (Exception)
        {
            Debug.WriteLine("TinyInsights: Error tracking event");
        }

        return Task.CompletedTask;
    }

    public Task TrackPageViewAsync(string viewName, Dictionary<string, string>? properties = null)
    {
        try
        {
            if (Client is null)
            {
                return Task.CompletedTask;
            }

            Debug.WriteLine($"TinyInsights: tracking page view {viewName}");

            Client.TrackPageView(viewName);
            Client.Flush();
        }
        catch (Exception)
        {
            Debug.WriteLine("TinyInsights: Error tracking page view");
        }

        return Task.CompletedTask;
    }

    public Task TrackDependencyAsync(string dependencyType, string dependencyName, string data, HttpMethod? httpMethod, DateTimeOffset startTime, TimeSpan duration, bool success, int resultCode = 0, Exception? exception = null)
    {
        try
        {
            if (Client is null)
            {
                return Task.CompletedTask;
            }

            Debug.WriteLine($"TinyInsights: Tracking dependency {dependencyName}");

            var fullUrl = data;

            if (data.Contains('?'))
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

            if (httpMethod is not null)
            {
                dependency.Properties.Add("HttpMethod", httpMethod.ToString());
            }

            if (exception is not null)
            {
                dependency.Properties.Add("ExceptionMessage", exception.Message);
                dependency.Properties.Add("StackTrace", exception.StackTrace);

                if (exception.InnerException is not null)
                {
                    dependency.Properties.Add("InnerExceptionMessage", exception.InnerException.Message);
                    dependency.Properties.Add("InnerExceptionStackTrace", exception.InnerException.StackTrace);
                }
            }

            Client.TrackDependency(dependency);
        }
        catch (Exception)
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
        if (!IsEnabled(logLevel))
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