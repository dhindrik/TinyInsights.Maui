using System.Diagnostics;
using System.Text.Json;

namespace TinyInsights.CrashHandlers;

public class CrashToJsonFileStorageHandler : ICrashHandler
{
    private const string CrashLogFilename = "crashes.mauiinsights";

    private string CrashStorageFilePath => Path.Combine(FileSystem.CacheDirectory, CrashLogFilename);

    public virtual bool HasCrashed()
    {
        try
        {
            return File.Exists(CrashStorageFilePath);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"TinyInsights: Error on {nameof(HasCrashed)}. Message: {ex.Message}");
            return false;
        }
    }

    public virtual void PushCrashToStorage(Exception ex)
    {
        try
        {
            Trace.WriteLine("TinyInsights: Handle crashes");

            var crashes = ReadCrashes() ?? [];

            crashes.Add(new Crash(ex));

            var json = JsonSerializer.Serialize(crashes);

            File.WriteAllText(CrashStorageFilePath, json);
        }
        catch (Exception exception)
        {
            Trace.WriteLine($"TinyInsights: Error on {nameof(PushCrashToStorage)}. Message: {exception.Message}");
        }
    }

    public virtual List<Crash>? PopCrashesFromStorage()
    {
        List<Crash>? crashes = ReadCrashes();
        if (crashes is null)
        {
            return null;
        }

        EraseCrashes();

        return crashes;
    }

    public virtual void EraseCrashes()
    {
        try
        {
            Trace.WriteLine($"TinyInsights: {nameof(EraseCrashes)}");

            File.Delete(CrashStorageFilePath);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"TinyInsights: Error on {nameof(EraseCrashes)}. Message: {ex.Message}");
        }
    }

    protected virtual List<Crash>? ReadCrashes()
    {
        try
        {
            Trace.WriteLine("TinyInsights: Read crashes");

            if (!File.Exists(CrashStorageFilePath))
            {
                return null;
            }

            var json = File.ReadAllText(CrashStorageFilePath);

            return string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<List<Crash>>(json);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"TinyInsights: Error reading crashes. Message: {ex.Message}");
        }

        return null;
    }
}
