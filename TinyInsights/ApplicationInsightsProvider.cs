using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.WindowsServer.TelemetryChannel;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Globalization;
using TinyInsights.CrashHandlers;

namespace TinyInsights;

public class ApplicationInsightsProvider : IInsightsProvider, ILogger
{
    public string? ConnectionString { get; set; }
    private static ApplicationInsightsProvider? provider;
    private const string UserIdKey = nameof(UserIdKey);

    private ICrashHandler crashHandler;
    private TelemetryConfiguration? configuration;
    private TelemetryClient? client;
    private TelemetryClient? Client => client ?? CreateTelemetryClient();
    private ITelemetryChannel? telemetryChannel;

    /// <summary>
    /// The telemetry channel to use for sending telemetry data. If null the default channel will be used.
    /// </summary>
    public ITelemetryChannel? TelemetryChannel
    {
        get => telemetryChannel;
        set
        {
            telemetryChannel = value;

            if (IsInitialized && configuration is not null && value is ServerTelemetryChannel serverTelemetryChannel)
            {
                configuration.TelemetryChannel = value;
                InitializeServerChannel(serverTelemetryChannel);
                return;
            }

            if (IsInitialized && configuration is not null)
            {
                configuration.TelemetryChannel = value;
            }
        }
    }

    public bool IsTrackErrorsEnabled { get; set; } = true;
    public bool IsTrackCrashesEnabled { get; set; } = true;
    public bool WriteCrashes { get; set; } = true;
    public bool IsTrackPageViewsEnabled { get; set; } = true;
    public bool IsAutoTrackPageViewsEnabled { get; set; } = true;
    public bool IsTrackEventsEnabled { get; set; } = true;
    public bool IsTrackDependencyEnabled { get; set; } = true;
    public bool EnableConsoleLogging { get; set; }
    public bool IsTelemetryClientInitialized => client is not null;

    private static ICrashHandler CreateDefaultCrashHandlerType() => new CrashToJsonFileStorageHandler();

    public Action<Dictionary<string, string>>? AfterCrash { get; set; }
    public Func<Dictionary<string, string>, Task>? BeforeSendCrash { get; set; }

    public Func<(string DependencyType, string DependencyName, string Data, DateTimeOffset StartTime, TimeSpan Duration, bool Success, int ResultCode, Exception? Exception), bool>? TrackDependencyFilter { get; set; }

#if IOS || MACCATALYST || ANDROID

    public ApplicationInsightsProvider(string? connectionString = null, ICrashHandler? crashHandler = null)
    {
        ConnectionString = connectionString;
        provider = this;

        this.crashHandler = crashHandler ?? CreateDefaultCrashHandlerType();

        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

        async void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            var crashProperties = CaptureGlobalProperties();
            AfterCrash?.Invoke(crashProperties);

            if (IsTrackCrashesEnabled)
            {
                this.crashHandler.PushCrashToStorage(e.Exception, crashProperties);
            }

            if (Client is not null)
            {
                await Client.FlushAsync(CancellationToken.None);
            }
        }

        async void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var crashProperties = CaptureGlobalProperties();

            AfterCrash?.Invoke(crashProperties);

            if (IsTrackCrashesEnabled)
            {
                this.crashHandler.PushCrashToStorage((Exception)e.ExceptionObject, crashProperties);
            }

            if (Client is not null)
            {
                await Client.FlushAsync(CancellationToken.None);
            }
        }
    }

#elif WINDOWS
    public ApplicationInsightsProvider(MauiWinUIApplication app, string? connectionString = null, ICrashHandler? crashHandler = null)
    {
        ConnectionString = connectionString;
        provider = this;

        this.crashHandler = crashHandler ?? CreateDefaultCrashHandlerType();

        app.UnhandledException += App_UnhandledException;

        void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            var crashProperties = CaptureGlobalProperties();

            AfterCrash?.Invoke(crashProperties);

            if (IsTrackCrashesEnabled)
            {
                this.crashHandler.PushCrashToStorage(e.Exception, crashProperties);
            }

            if (Client is not null)
            {
                Client.Flush();
            }
        }
    }
#elif NET8_0_OR_GREATER

    public ApplicationInsightsProvider()
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

            Application.Current.PageAppearing += OnAppearing;
            Application.Current.PageDisappearing += OnDisappearing;
        }

        if (IsTrackCrashesEnabled && WriteCrashes)
        {
            Task.Run(SendCrashes);
        }

        IsInitialized = true;
    }

    static List<(Type pageType, DateTime appearTime)> pageVisitTimeTracking = [];
    public void SetCrashHandler(ICrashHandler customCrashHandler)
    {
        crashHandler = customCrashHandler;
    }

    public void Connect(string connectionString)
    {
        ArgumentNullException.ThrowIfNull(connectionString);

        if (!IsTelemetryClientInitialized)
        {
            ConnectionString = connectionString;
            CreateTelemetryClient();
        }
    }

    private static void OnAppearing(object? sender, Page e)
    {
        var pageType = e.GetType();
        provider?.TrackPageViewAsync(pageType.FullName ?? pageType.Name, new Dictionary<string, string> { { "DisplayName", pageType.Name } });

        pageVisitTimeTracking.Add((pageType, DateTime.Now));
    }

    private static void OnDisappearing(object? sender, Page e)
    {
        var pageType = e.GetType();

        var lastIndex = pageVisitTimeTracking.FindLastIndex(x => x.pageType == pageType);

        if (lastIndex == -1)
        {
            return;
        }

        var lastPageAdded = pageVisitTimeTracking[lastIndex];
        pageVisitTimeTracking.RemoveAt(lastIndex);

        var duration = DateTime.Now - lastPageAdded.appearTime;
        provider?.TrackPageVisitTime(pageType.FullName ?? pageType.Name, pageType.Name, duration.TotalMilliseconds);
    }

    readonly Dictionary<string, string> globalProperties = [];

    private TelemetryClient? CreateTelemetryClient()
    {
        if (client is not null)
        {
            return client;
        }

        try
        {
            if (string.IsNullOrWhiteSpace(ConnectionString))
            {
                Console.WriteLine("ConnectionString is required to initialize TinyInsights");
                return null;
            }

            configuration = new TelemetryConfiguration()
            {
                ConnectionString = ConnectionString
            };

            client = new TelemetryClient(configuration);

            if (TelemetryChannel is not null)
            {
                configuration.TelemetryChannel = TelemetryChannel;

                if (TelemetryChannel is ServerTelemetryChannel serverTelemetryChannel)
                {
                    InitializeServerChannel(serverTelemetryChannel);
                }
            }

            client.Context.Device.OperatingSystem = DeviceInfo.Platform.ToString();
            client.Context.Device.Model = DeviceInfo.Model;
            client.Context.Device.Type = DeviceInfo.Idiom.ToString();

            // Role name will show device name if we don't set it to empty and we want it to be so anonymous as possible.
            client.Context.Cloud.RoleName = string.Empty;
            client.Context.Cloud.RoleInstance = string.Empty;
            client.Context.User.Id = GetUserId();

            client.Context.Component.Version = AppInfo.VersionString;

            // Add any global properties, the user has already added
            foreach (var property in globalProperties)
            {
                switch (property.Key)
                {
                    case "Cloud.RoleName":
                        client.Context.Cloud.RoleName = property.Value;
                        break;

                    case "Cloud.RoleInstance":
                        client.Context.Cloud.RoleInstance = property.Value;
                        break;

                    case "Device.OperatingSystem":
                        client.Context.Device.OperatingSystem = property.Value;
                        break;

                    case "Device.Model":
                        client.Context.Device.Model = property.Value;
                        break;

                    case "Device.Type":
                        client.Context.Device.Type = property.Value;
                        break;

                    case "Device.Id":
                        client.Context.Device.Id = property.Value;
                        break;

                    default:
                        client.Context.GlobalProperties[property.Key] = property.Value;
                        break;
                }
            }

            client.Context.GlobalProperties.TryAdd("Language", CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
            client.Context.GlobalProperties.TryAdd("Manufacturer", DeviceInfo.Manufacturer);
            client.Context.GlobalProperties.TryAdd("AppVersion", AppInfo.VersionString);
            client.Context.GlobalProperties.TryAdd("AppBuildNumber", AppInfo.BuildString);
            client.Context.GlobalProperties.TryAdd("OperatingSystemVersion", DeviceInfo.VersionString);
            client.Context.Session.Id = Guid.NewGuid().ToString();

            return client;
        }
        catch (Exception)
        {
            if (EnableConsoleLogging)
                Console.WriteLine("TinyInsights: Error creating TelemetryClient");
        }

        return null;
    }

    private void InitializeServerChannel(ServerTelemetryChannel channel)
    {
        channel.StorageFolder = FileSystem.CacheDirectory;
        Environment.SetEnvironmentVariable("TMPDIR", FileSystem.CacheDirectory);

        channel.Initialize(configuration);
    }

    public void UpsertGlobalProperty(string key, string value)
    {
        globalProperties[key] = value;

        if (Client is null)
        {
            return;
        }

        switch (key)
        {
            case "Cloud.RoleName":
                Client.Context.Cloud.RoleName = value;
                break;

            case "Cloud.RoleInstance":
                Client.Context.Cloud.RoleInstance = value;
                break;

            case "Device.OperatingSystem":
                Client.Context.Device.OperatingSystem = value;
                break;

            case "Device.Model":
                Client.Context.Device.Model = value;
                break;

            case "Device.Type":
                Client.Context.Device.Type = value;
                break;

            case "Device.Id":
                Client.Context.Device.Id = value;
                break;

            default:
                Client.Context.GlobalProperties[key] = value;
                break;
        }
    }

    public void RemoveGlobalProperty(string key)
    {
        if (Client is null)
        {
            return;
        }

        if (globalProperties.ContainsKey(key))
        {
            globalProperties.Remove(key);
        }

        if (Client.Context.GlobalProperties.ContainsKey(key))
        {
            Client.Context.GlobalProperties.Remove(key);
        }
    }

    private Dictionary<string, string> CaptureGlobalProperties()
    {
        var snapshot = new Dictionary<string, string>(globalProperties);

        if (client is not null)
        {
            // Capture context properties from the live client so we get
            // the actual values (defaults + user overrides) at crash time
            snapshot.TryAdd("Device.OperatingSystem", client.Context.Device.OperatingSystem);
            snapshot.TryAdd("Device.Model", client.Context.Device.Model);
            snapshot.TryAdd("Device.Type", client.Context.Device.Type);

            if (!string.IsNullOrEmpty(client.Context.Device.Id))
            {
                snapshot.TryAdd("Device.Id", client.Context.Device.Id);
            }

            snapshot.TryAdd("Cloud.RoleName", client.Context.Cloud.RoleName);
            snapshot.TryAdd("Cloud.RoleInstance", client.Context.Cloud.RoleInstance);
            snapshot.TryAdd("User.Id", client.Context.User.Id);
            snapshot.TryAdd("Session.Id", client.Context.Session.Id);
            snapshot.TryAdd("Component.Version", client.Context.Component.Version);

            foreach (var property in client.Context.GlobalProperties)
            {
                snapshot.TryAdd(property.Key, property.Value);
            }
        }

        return snapshot;
    }

    public void OverrideAnonymousUserId(string userId)
    {
        Preferences.Set(UserIdKey, userId);
        if (Client is not null)
        {
            Client.Context.User.Id = userId;
        }
    }

    private string GetUserId()
    {
        var userId = Preferences.Get(UserIdKey, null);

        return userId ?? GenerateNewAnonymousUserId();
    }

    public string GenerateNewAnonymousUserId()
    {
        var userId = Guid.NewGuid().ToString();
        Preferences.Set(UserIdKey, userId);

        return userId;
    }

    public void CreateNewSession()
    {
        if (Client is null)
        {
            return;
        }

        Client.Context.Session.Id = Guid.NewGuid().ToString();
    }

    public async Task SendCrashes()
    {
        try
        {
            if (Client is null)
            {
                return;
            }

            var crashes = crashHandler.PopCrashesFromStorage();

            if (crashes is null || crashes.Count == 0)
            {
                return;
            }


            if (EnableConsoleLogging)
                Console.WriteLine($"TinyInsights: Sending {crashes.Count} crashes");

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


                if (BeforeSendCrash is not null)
                {
                    try
                    {
                        await BeforeSendCrash.Invoke(properties);
                    }
                    catch (Exception e)
                    {
                        if (EnableConsoleLogging)
                            Console.WriteLine($"TinyInsights: Error sending crashes. Message: {e.Message}");
                    }
                }

                if (crash.GlobalProperties is not null && crash.GlobalProperties.Count > 0)
                {
                    var crashClient = CreateCrashTelemetryClient(crash.GlobalProperties);

                    if (crashClient is not null)
                    {
                        crashClient.TrackException(ex, properties);
                        await crashClient.FlushAsync(CancellationToken.None);
                        continue;
                    }
                }

                await TrackErrorAsync(ex, properties);
            }

            await FlushAsync();
        }
        catch (Exception ex)
        {
            if (EnableConsoleLogging)
                Console.WriteLine($"TinyInsights: Error sending crashes. Message: {ex.Message}");
        }
    }

    private TelemetryClient? CreateCrashTelemetryClient(Dictionary<string, string> crashGlobalProperties)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ConnectionString))
            {
                return null;
            }

            var crashConfiguration = new TelemetryConfiguration()
            {
                ConnectionString = ConnectionString
            };

            if (TelemetryChannel is not null)
            {
                crashConfiguration.TelemetryChannel = TelemetryChannel;
            }

            var crashClient = new TelemetryClient(crashConfiguration);

            foreach (var property in crashGlobalProperties)
            {
                switch (property.Key)
                {
                    case "Cloud.RoleName":
                        crashClient.Context.Cloud.RoleName = property.Value;
                        break;

                    case "Cloud.RoleInstance":
                        crashClient.Context.Cloud.RoleInstance = property.Value;
                        break;

                    case "Device.OperatingSystem":
                        crashClient.Context.Device.OperatingSystem = property.Value;
                        break;

                    case "Device.Model":
                        crashClient.Context.Device.Model = property.Value;
                        break;

                    case "Device.Type":
                        crashClient.Context.Device.Type = property.Value;
                        break;

                    case "Device.Id":
                        crashClient.Context.Device.Id = property.Value;
                        break;

                    case "User.Id":
                        crashClient.Context.User.Id = property.Value;
                        break;

                    case "Session.Id":
                        crashClient.Context.Session.Id = property.Value;
                        break;

                    case "Component.Version":
                        crashClient.Context.Component.Version = property.Value;
                        break;

                    default:
                        crashClient.Context.GlobalProperties[property.Key] = property.Value;
                        break;
                }
            }

            return crashClient;
        }
        catch (Exception ex)
        {
            if (EnableConsoleLogging)
                Console.WriteLine($"TinyInsights: Error creating crash TelemetryClient. Message: {ex.Message}");

            return null;
        }
    }

    public bool HasCrashed()
    {
        return crashHandler.HasCrashed();
    }

    public void ResetCrashes()
    {
        crashHandler.EraseCrashes();
    }

    public async Task TrackErrorAsync(Exception ex, Dictionary<string, string>? properties = null, Dictionary<string, double>? metrics = null)
    {
        try
        {
            if (Client is null)
            {
                return;
            }

            if (EnableConsoleLogging)
                Console.WriteLine($"TinyInsights: Tracking error {ex.Message}");

            properties ??= [];

            if (ex.StackTrace is not null)
            {
                properties.TryAdd("StackTrace", ex.StackTrace);
            }

            Client.TrackException(ex, properties, metrics);
        }
        catch (Exception exception)
        {
            if (EnableConsoleLogging)
                Console.WriteLine($"TinyInsights: Error tracking error. Message: {exception.Message}");
        }
    }

    public async Task TrackEventAsync(string eventName, Dictionary<string, string>? properties = null, Dictionary<string, double>? metrics = null)
    {
        try
        {
            if (Client is null)
            {
                return;
            }

            if (EnableConsoleLogging)
                Console.WriteLine($"TinyInsights: Tracking event {eventName}");

            Client.TrackEvent(eventName, properties, metrics);
        }
        catch (Exception ex)
        {
            if (EnableConsoleLogging)
                Console.WriteLine($"TinyInsights: Error tracking event. Message: {ex.Message}");
        }
    }

    public async Task TrackPageVisitTime(string pageFullName, string pageDisplayName, double pageVisitTime)
    {
        if (Client is null)
        {
            return;
        }

        var properties = new Dictionary<string, string>
        {
            { "Page", pageFullName },
            { "DisplayName", pageDisplayName },
        };

        Client.TrackMetric("PageVisitTime", pageVisitTime, properties);
        await Client.FlushAsync(CancellationToken.None);
    }

    public async Task TrackPageViewAsync(string viewName, Dictionary<string, string>? properties = null, Dictionary<string, double>? metrics = null)
    {
        try
        {
            if (Client is null)
            {
                return;
            }

            if (!IsTrackPageViewsEnabled)
            {
                return;
            }

            if (EnableConsoleLogging)
                Console.WriteLine($"TinyInsights: tracking page view {viewName}");

            var pageView = new PageViewTelemetry(viewName)
            {
                Timestamp = new DateTimeOffset(DateTime.Now),
            };

            if (properties is not null)
            {
                foreach (var property in properties)
                {
                    pageView.Properties.Add(property.Key, property.Value);
                }
            }

            if (metrics is not null)
            {
                foreach (var metric in metrics)
                {
                    pageView.Metrics.Add(metric.Key, metric.Value);
                }
            }

            Client.TrackPageView(pageView);
        }
        catch (Exception ex)
        {
            if (EnableConsoleLogging)
                Console.WriteLine($"TinyInsights: Error tracking page view. Message: {ex.Message}");
        }
    }

    public async Task TrackDependencyAsync(string dependencyType, string dependencyName, string data, HttpMethod? httpMethod, DateTimeOffset startTime, TimeSpan duration, bool success, int resultCode = 0, Exception? exception = null)
    {
        try
        {
            if (Client is null)
            {
                return;
            }

            if (EnableConsoleLogging)
                Console.WriteLine($"TinyInsights: Tracking dependency {dependencyName}");

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
        catch (Exception ex)
        {
            if (EnableConsoleLogging)
                Console.WriteLine($"TinyInsights: Error tracking dependency. Message: {ex.Message}");
        }
    }

    public Task TrackDependencyAsync(string dependencyType, string dependencyName, string data, DateTimeOffset startTime, TimeSpan duration, bool success, int resultCode = 0, Exception? exception = null)
    {
        return TrackDependencyAsync(dependencyType, dependencyName, data, null, startTime, duration, success, resultCode, exception);
    }

    public async Task FlushAsync()
    {
        try
        {
            if (Client is null)
            {
                return;
            }

            await Client.FlushAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            if (EnableConsoleLogging)
                Console.WriteLine($"TinyInsights: Error flushing. Message: {ex.Message}");
        }
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
        Console.WriteLine($"TinyInsights: DebugLogging, Event: {GetEventName(eventId)}, State: {state}, Exception: {exception?.Message}");
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