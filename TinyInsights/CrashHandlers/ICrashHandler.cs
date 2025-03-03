namespace TinyInsights.CrashHandlers;

/// <summary>
/// Crash handler for unhandled exceptions.
/// </summary>
public interface ICrashHandler
{
	/// <summary>
	/// Indicates if the app has crashed on the app previous run.
	/// There are some crash data stored in the underlying storage.
	/// </summary>	
	bool HasCrashed();

	/// <summary>
	/// Reads and removes the crash data from the underlying storage and returns a list of crashes.
	/// </summary>
	List<Crash>? PopCrashesFromStorage();

	/// <summary>
	/// Pushes the unhandled exception. 
	/// This method is supposed to be called when the app crashes in order to store the crash information in the underlying storage.
	/// </summary>	
	void PushCrashToStorage(Exception ex);
}