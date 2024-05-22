using System.Globalization;
using System.Text.Json;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace TinyInsights;

public class ApplicationInsightsProvider : IInsightsProvider
{
private const string userIdKey = nameof(userIdKey);

    private const string crashLogFilename = "crashes.mauiinsights";

    private readonly string logPath = FileSystem.CacheDirectory;

    private TelemetryClient client;

    public bool IsTrackErrorsEnabled { get; set; } = true;
    public bool IsTrackPageViewsEnabled { get; set; } = true;
    public bool IsTrackEventsEnabled { get; set; } = true;
    public bool IsTrackDependencyEnabled { get; set; } = true;

#if IOS || MACCATALYST || ANDROID
    public ApplicationInsightsProvider(string connectionString)
    {
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

        var configuration = new TelemetryConfiguration()
        {
            ConnectionString = connectionString
        };

        try
        {
            client = new TelemetryClient(configuration);

            AddMetaData();
        }
        catch (Exception)
        {
        }

        Task.Run(SendCrashes);
    }

    private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
    {
        HandleCrash(e.Exception);
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        HandleCrash((Exception)e.ExceptionObject);
    }

#elif WINDOWS
    public ApplicationInsightsProvider(MauiWinUIApplication app, string connectionString)
    {
        app.UnhandledException += App_UnhandledException;

        var configuration = new TelemetryConfiguration()
        {
            ConnectionString = connectionString
        };

        try
        {
            client = new TelemetryClient(configuration);

            AddMetaData();
        }
        catch (Exception)
        {
        }

        AddMetaData();

        Task.Run(SendCrashes);
    }

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        HandleCrash(e.Exception);
    }
#endif

    private void AddMetaData()
    {
        client.Context.Device.OperatingSystem = DeviceInfo.Platform.ToString();
        client.Context.Device.Model = DeviceInfo.Model;
        client.Context.Device.Type = DeviceInfo.Idiom.ToString();
        
        //Role name will show device name if we don't set it to empty and we want it to be so anonymous as possible.
        client.Context.Cloud.RoleName = string.Empty;
        client.Context.Cloud.RoleInstance = string.Empty;

        if (Preferences.ContainsKey(userIdKey))
        {
            var userId = Preferences.Get(userIdKey, string.Empty);

            client.Context.User.Id = userId;
        }
        else
        {
            var userId = Guid.NewGuid().ToString();
            Preferences.Set(userIdKey, userId);

            client.Context.User.Id = userId;
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

            if (crashes != null)
            {


                foreach (var crash in crashes)
                {
                    var ex = crash.GetException();
                    var properties = new Dictionary<string, string>
                    {
                        { "IsCrash", "true" },
                        {"StackTrace", crash.StackTrace },
                        {"ExceptionType", crash.ExceptionType },
                        {"Source", crash.Source}
                    };



                    await TrackErrorAsync(ex, properties);
                }
            }
        }
        catch (Exception)
        {
        }
    }

    private List<Crash> ReadCrashes()
    {
        try
        {
            var path = Path.Combine(logPath, crashLogFilename);

            if (!File.Exists(path))
            {
                return new List<Crash>();
            }

            var json = File.ReadAllText(path);

            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<Crash>();
            }

            return JsonSerializer.Deserialize<List<Crash>>(json);

        }
        catch (Exception)
        {
        }

        return new List<Crash>();
    }

    private void HandleCrash(Exception ex)
    {
        try
        {
            var crashes = ReadCrashes();

            crashes.Add(new Crash(ex));

            var json = JsonSerializer.Serialize(crashes);

            var path = Path.Combine(logPath, crashLogFilename);

            if (!File.Exists(path))
            {
                File.Create(path);
            }

            System.IO.File.WriteAllText(path, json);

        }
        catch (Exception)
        {
        }
    }

    public Task TrackErrorAsync(Exception ex, Dictionary<string, string>? properties = null)
    {
        try
        {
            if (properties == null)
            {
                properties = new Dictionary<string, string>();
            }

            properties.Add("StackTrace", ex.StackTrace);
            
            client.TrackException(ex, properties);
            client.Flush();
        }
        catch (Exception)
        {
        }
        
        return Task.CompletedTask;
    }

    public Task TrackEventAsync(string eventName, Dictionary<string, string>? properties = null)
    {
        try
        {
            client.TrackEvent(eventName, properties);
            client.Flush();
        }
        catch (Exception ex)
        {
          
        }
        
        return Task.CompletedTask;
    }

    public Task TrackPageViewAsync(string viewName, Dictionary<string, string>? properties = null)
    {
        try
        {
            client.TrackPageView(viewName);
            client.Flush();
        }
        catch (Exception ex)
        {
         
        }
        
        return Task.CompletedTask;
    }

    public Task TrackDependencyAsync(string dependencyType, string dependencyName, string data, DateTimeOffset startTime, TimeSpan duration, bool success, int resultCode = 0, Exception? exception = null)
    {
        try
        {
            var fullUrl = data;
            
            if (data.Contains("?"))
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
                ResultCode = resultCode.ToString()
            };
            
            dependency.Properties.Add("FullUrl", fullUrl);
            
            if (exception != null)
            {
                dependency.Properties.Add("ExceptionMessage", exception.Message);
                dependency.Properties.Add("StackTrace", exception.StackTrace);

                if (exception.InnerException != null)
                {
                    dependency.Properties.Add("InnerExceptionMessage", exception.InnerException.Message);
                    dependency.Properties.Add("InnerExceptionStackTrace", exception.InnerException.StackTrace);
                }
            }

            client.TrackDependency(dependency);
        }
        catch (Exception ex)
        {
          
        }
        
        return Task.CompletedTask;
    }
}