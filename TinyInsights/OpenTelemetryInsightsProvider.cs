using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Globalization;
using TinyInsights.CrashHandlers;

namespace TinyInsights;

public class OpenTelemetryInsightsProvider : IInsightsProvider, ILogger
{
    public string? ConnectionString { get; set; }
    private static OpenTelemetryInsightsProvider? provider;
    private const string UserIdKey = nameof(UserIdKey);

    private readonly ActivitySource activitySource = new("TinyInsights");
    private TracerProvider? tracerProvider;
    private ILoggerFactory? loggerFactory;
    private ILogger? logger;
    private ICrashHandler crashHandler;

    public bool IsTrackErrorsEnabled { get; set; } = true;
    public bool IsTrackCrashesEnabled { get; set; } = true;
    public bool WriteCrashes { get; set; } = true;
    public bool IsTrackPageViewsEnabled { get; set; } = true;
    public bool IsAutoTrackPageViewsEnabled { get; set; } = true;
    public bool IsTrackEventsEnabled { get; set; } = true;
    public bool IsTrackDependencyEnabled { get; set; } = true;
    public bool EnableConsoleLogging { get; set; }

    public bool IsTelemetryClientInitialized => tracerProvider is not null && logger is not null;

    public Func<(string DependencyType, string DependencyName, string Data, DateTimeOffset StartTime, TimeSpan Duration, bool Success, int ResultCode, Exception? Exception), bool>? TrackDependencyFilter { get; set; }

    readonly Dictionary<string, string> globalProperties = [];
    private string? sessionId;

    private static ICrashHandler CreateDefaultCrashHandlerType() => new CrashToJsonFileStorageHandler();

#if IOS || MACCATALYST || ANDROID
	public OpenTelemetryInsightsProvider(string? connectionString = null, ICrashHandler? crashHandler = null)
	{
		ConnectionString = connectionString;
		provider = this;
		this.crashHandler = crashHandler ?? CreateDefaultCrashHandlerType();

		AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
		TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

		async void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
		{
			if (IsTrackCrashesEnabled)
			{
				this.crashHandler.PushCrashToStorage(e.Exception);
			}

			await FlushAsync();
		}

		async void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			if (IsTrackCrashesEnabled)
			{
				this.crashHandler.PushCrashToStorage((Exception)e.ExceptionObject);
			}

			await FlushAsync();
		}
	}
#elif WINDOWS
	public OpenTelemetryInsightsProvider(MauiWinUIApplication app, string? connectionString = null, ICrashHandler? crashHandler = null)
	{
		ConnectionString = connectionString;
		provider = this;
		this.crashHandler = crashHandler ?? CreateDefaultCrashHandlerType();

		app.UnhandledException += App_UnhandledException;

		async void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
		{
			if (IsTrackCrashesEnabled)
			{
				this.crashHandler.PushCrashToStorage(e.Exception);
			}

			await FlushAsync();
		}
	}
#elif NET8_0_OR_GREATER
    public OpenTelemetryInsightsProvider()
    {
        // Do nothing. The net8.0 target exists for enabling unit testing, not for actual use.
        crashHandler = CreateDefaultCrashHandlerType();
    }
#endif

    public static bool IsInitialized { get; private set; }

    static List<(Type pageType, DateTime appearTime)> pageVisitTimeTracking = [];

    public void Initialize()
    {
        EnsureInitialized();

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

    public void SetCrashHandler(ICrashHandler customCrashHandler)
    {
        crashHandler = customCrashHandler;
    }

    public void Connect(string connectionString)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        ConnectionString = connectionString;
        EnsureInitialized();
    }

    private void EnsureInitialized()
    {
        if (tracerProvider is not null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            Console.WriteLine("ConnectionString is required to initialize TinyInsights");
            return;
        }

        sessionId ??= Guid.NewGuid().ToString();

		var serviceName = globalProperties.TryGetValue("Cloud.RoleName", out var roleName) && !string.IsNullOrWhiteSpace(roleName)
			? roleName
			: "TinyInsights";

		var resource = ResourceBuilder
			.CreateDefault()
			.AddService(serviceName: serviceName, serviceVersion: AppInfo.VersionString)
            .AddAttributes(new Dictionary<string, object>
            {
                ["device.os"] = DeviceInfo.Platform.ToString(),
                ["device.model"] = DeviceInfo.Model,
                ["device.type"] = DeviceInfo.Idiom.ToString(),
                ["enduser.id"] = GetUserId(),
                ["session.id"] = sessionId!,
                ["app.version"] = AppInfo.VersionString,
                ["app.build"] = AppInfo.BuildString,
                ["device.manufacturer"] = DeviceInfo.Manufacturer,
                ["os.version"] = DeviceInfo.VersionString,
                ["language"] = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName,
            });

        foreach (var kvp in globalProperties)
        {
            resource = resource.AddAttributes(new Dictionary<string, object> { [kvp.Key] = kvp.Value });
        }

        tracerProvider = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(resource)
            .AddSource(activitySource.Name)
            .AddAzureMonitorTraceExporter(o => o.ConnectionString = ConnectionString)
            .Build();

        loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(o =>
            {
                o.SetResourceBuilder(resource);
                // Ensure state from BeginScope / structured logging is exported as custom dimensions.
                o.IncludeScopes = true;
                o.IncludeFormattedMessage = true;
                o.ParseStateValues = true;
                o.AddAzureMonitorLogExporter(exp => exp.ConnectionString = ConnectionString);
            });
        });
        logger = loggerFactory.CreateLogger("TinyInsights");
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

    public void UpsertGlobalProperty(string key, string value)
    {
        globalProperties[key] = value;
    }

    public void RemoveGlobalProperty(string key)
    {
        if (globalProperties.ContainsKey(key))
        {
            globalProperties.Remove(key);
        }
    }

    public void OverrideAnonymousUserId(string userId)
    {
        Preferences.Set(UserIdKey, userId);
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
        sessionId = Guid.NewGuid().ToString();
    }

    public async Task SendCrashes()
    {
        try
        {
            EnsureInitialized();
            if (tracerProvider is null)
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

    public bool HasCrashed() => crashHandler.HasCrashed();

    public void ResetCrashes() => crashHandler.EraseCrashes();

    public Task TrackErrorAsync(Exception ex, Dictionary<string, string>? properties = null, Dictionary<string, double>? metrics = null)
    {
        try
        {
            EnsureInitialized();
            if (tracerProvider is null || logger is null || !IsTrackErrorsEnabled)
            {
                return Task.CompletedTask;
            }

            if (EnableConsoleLogging)
                Console.WriteLine($"TinyInsights: Tracking error {ex.Message}");

            properties ??= [];
            if (ex.StackTrace is not null)
            {
                properties.TryAdd("StackTrace", ex.StackTrace);
            }

            // Also emit as a log entry so custom dimensions appear under Application Insights Logs.
            // Exception-only spans may not surface dimensions in the Logs table.
            using (var scope = BeginTelemetryScope(properties, metrics))
            {
                // The exporter will capture the exception + scope state as customDimensions.
                logger.LogError(ex, "TinyInsightsException:{ExceptionType}", ex.GetType().FullName ?? ex.GetType().Name);
            }
        }
        catch (Exception exception)
        {
            if (EnableConsoleLogging)
                Console.WriteLine($"TinyInsights: Error tracking error. Message: {exception.Message}");
        }

        return Task.CompletedTask;
    }

    public Task TrackEventAsync(string eventName, Dictionary<string, string>? properties = null, Dictionary<string, double>? metrics = null)
    {
        try
        {
            EnsureInitialized();
            if (tracerProvider is null || logger is null || !IsTrackEventsEnabled)
            {
                return Task.CompletedTask;
            }

            if (EnableConsoleLogging)
                Console.WriteLine($"TinyInsights: Tracking event {eventName}");

            using var scope = BeginTelemetryScope(properties, metrics);
            logger.LogInformation("TinyInsightsEvent:{EventName}", eventName);
        }
        catch (Exception ex)
        {
            if (EnableConsoleLogging)
                Console.WriteLine($"TinyInsights: Error tracking event. Message: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    public Task TrackPageVisitTime(string pageFullName, string pageDisplayName, double pageVisitTime)
    {
        EnsureInitialized();
        if (tracerProvider is null || logger is null)
        {
            return Task.CompletedTask;
        }

        var properties = new Dictionary<string, string>
        {
            { "Page", pageFullName },
            { "DisplayName", pageDisplayName },
        };

        var metrics = new Dictionary<string, double>
        {
            { "PageVisitTime", pageVisitTime }
        };

        using var scope = BeginTelemetryScope(properties, metrics);
        logger.LogInformation("TinyInsightsMetric:PageVisitTime");
        return FlushAsync();
    }

    public Task TrackPageViewAsync(string viewName, Dictionary<string, string>? properties = null, Dictionary<string, double>? metrics = null)
    {
        try
        {
            EnsureInitialized();
            if (tracerProvider is null || logger is null || !IsTrackPageViewsEnabled)
            {
                return Task.CompletedTask;
            }

            if (EnableConsoleLogging)
                Console.WriteLine($"TinyInsights: tracking page view {viewName}");

            properties ??= [];
            properties["PageName"] = viewName;

            // Emit a dedicated span marked as a page view so it can appear in the Application
            // Insights Page views experience (exporter-specific mapping).
            using (var activity = activitySource.StartActivity(viewName, ActivityKind.Internal))
            {
                if (activity is not null)
                {
                    ApplyCommonTags(activity, properties, metrics);
                    activity.SetTag("azm.ms.page_view.name", viewName);
                    activity.SetStatus(ActivityStatusCode.Ok);
                }
            }

            // Also emit a log record for querying via Logs (traces table).
            using (var scope = BeginTelemetryScope(properties, metrics))
            {
                logger.LogInformation("TinyInsightsPageView:{ViewName}", viewName);
            }

            return FlushAsync();
        }
        catch (Exception ex)
        {
            if (EnableConsoleLogging)
                Console.WriteLine($"TinyInsights: Error tracking page view. Message: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    public Task TrackDependencyAsync(string dependencyType, string dependencyName, string data, HttpMethod? httpMethod, DateTimeOffset startTime, TimeSpan duration, bool success, int resultCode = 0, Exception? exception = null)
    {
        try
        {
            EnsureInitialized();
            if (tracerProvider is null || !IsTrackDependencyEnabled)
            {
                return Task.CompletedTask;
            }

            var args = (dependencyType, dependencyName, data, startTime, duration, success, resultCode, exception);
            if (TrackDependencyFilter is not null && !TrackDependencyFilter(args))
            {
                return Task.CompletedTask;
            }

            if (EnableConsoleLogging)
                Console.WriteLine($"TinyInsights: Tracking dependency {dependencyName}");

            var fullUrl = data;
            if (data.Contains('?'))
            {
                data = data.Split("?")[0];
            }

            using var activity = activitySource.StartActivity(dependencyName, ActivityKind.Client, parentContext: default, tags: null, links: null, startTime: startTime);
            if (activity is null)
            {
                return Task.CompletedTask;
            }

            activity.SetTag("dependency.type", dependencyType);
            activity.SetTag("dependency.name", dependencyName);
            activity.SetTag("dependency.data", data);
            activity.SetTag("dependency.full_url", fullUrl);
            activity.SetTag("dependency.result_code", resultCode.ToString());
            if (httpMethod is not null)
            {
                activity.SetTag("http.method", httpMethod.ToString());
            }

            if (exception is not null)
            {
                activity.AddException(exception);
                if (exception.InnerException is not null)
                {
                    activity.SetTag("InnerExceptionMessage", exception.InnerException.Message);
                    activity.SetTag("InnerExceptionStackTrace", exception.InnerException.StackTrace);
                }
            }

            activity.SetStatus(success ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
            activity.SetEndTime((startTime + duration).DateTime);
        }
        catch (Exception ex)
        {
            if (EnableConsoleLogging)
                Console.WriteLine($"TinyInsights: Error tracking dependency. Message: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    public Task TrackDependencyAsync(string dependencyType, string dependencyName, string data, DateTimeOffset startTime, TimeSpan duration, bool success, int resultCode = 0, Exception? exception = null)
        => TrackDependencyAsync(dependencyType, dependencyName, data, null, startTime, duration, success, resultCode, exception);

    public Task FlushAsync()
    {
        try
        {
            if (tracerProvider is null)
            {
                return Task.CompletedTask;
            }

            var otelFlush = Task.Run(() => tracerProvider.ForceFlush());
            return otelFlush;
        }
        catch (Exception ex)
        {
            if (EnableConsoleLogging)
                Console.WriteLine($"TinyInsights: Error flushing. Message: {ex.Message}");
            return Task.CompletedTask;
        }
    }

    private IDisposable? BeginTelemetryScope(Dictionary<string, string>? properties, Dictionary<string, double>? metrics)
    {
        var scopeState = new Dictionary<string, object?>
        {
            ["enduser.id"] = GetUserId(),
            ["session.id"] = sessionId ?? string.Empty,
        };

        foreach (var gp in globalProperties)
        {
            scopeState[gp.Key] = gp.Value;
        }

        if (properties is not null)
        {
            foreach (var p in properties)
            {
                scopeState[p.Key] = p.Value;
            }
        }

        if (metrics is not null)
        {
            foreach (var m in metrics)
            {
                scopeState[m.Key] = m.Value;
            }
        }

        return logger?.BeginScope(scopeState);
    }

    private void ApplyCommonTags(Activity activity, Dictionary<string, string>? properties, Dictionary<string, double>? metrics)
    {
        activity.SetTag("enduser.id", GetUserId());
        activity.SetTag("session.id", sessionId ?? string.Empty);

        foreach (var gp in globalProperties)
        {
            activity.SetTag(gp.Key, gp.Value);
        }

        if (properties is not null)
        {
            foreach (var property in properties)
            {
                activity.SetTag(property.Key, property.Value);
            }
        }

        if (metrics is not null)
        {
            foreach (var metric in metrics)
            {
                activity.SetTag(metric.Key, metric.Value);
            }
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

    private static string GetEventName(EventId eventId) => eventId.Name ?? eventId.Id.ToString();

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

    // Delegate scopes to the underlying OpenTelemetry-enabled logger so dimensions flow to exporters.
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => logger?.BeginScope(state);
    #endregion ILogger
}
