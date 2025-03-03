using System.Diagnostics;
using System.Text.Json;

namespace TinyInsights.CrashHandlers;

public class CrashToJsonFileStorageHandler : ICrashHandler
{	
	private const string crashLogFilename = "crashes.mauiinsights";

	private string crashStorageFilePath => Path.Combine(FileSystem.CacheDirectory, crashLogFilename);

	public bool HasCrashed()
	{
		try
		{
			return File.Exists(crashStorageFilePath);
		}
		catch (Exception ex)
		{
			Trace.WriteLine($"TinyInsights: Error on {nameof(HasCrashed)}. Message: {ex.Message}");
			return false;
		}
	}

	public void PushCrashToStorage(Exception ex)
	{
		try
		{
			Trace.WriteLine("TinyInsights: Handle crashes");

			var crashes = ReadCrashes() ?? [];

			crashes.Add(new Crash(ex));

			var json = JsonSerializer.Serialize(crashes);

			File.WriteAllText(crashStorageFilePath, json);
		}
		catch (Exception exception)
		{
			Trace.WriteLine($"TinyInsights: Error on {nameof(PushCrashToStorage)}. Message: {exception.Message}");
		}
	}

	public List<Crash>? PopCrashesFromStorage()
	{
		List<Crash>? crashes = ReadCrashes();
		if (crashes is null)
		{
			return null;
		}
		
		EraseCrashes();

		return crashes;
	}

	private void EraseCrashes()
	{
		try
		{
			Trace.WriteLine($"TinyInsights: {nameof(EraseCrashes)}");

			File.Delete(crashStorageFilePath);
		}
		catch (Exception ex)
		{
			Trace.WriteLine($"TinyInsights: Error on {nameof(EraseCrashes)}. Message: {ex.Message}");
		}
	}

	private List<Crash>? ReadCrashes()
	{
		try
		{
			Trace.WriteLine("TinyInsights: Read crashes");
			
			if (!File.Exists(crashStorageFilePath))
			{
				return null;
			}

			var json = File.ReadAllText(crashStorageFilePath);

			return string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<List<Crash>>(json);
		}
		catch (Exception ex)
		{
			Trace.WriteLine($"TinyInsights: Error reading crashes. Message: {ex.Message}");
		}

		return null;
	}
}
