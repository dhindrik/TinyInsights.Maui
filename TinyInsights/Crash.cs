using System.Reflection;

namespace TinyInsights;

public class Crash
{
    public Crash()
    {
        Message = string.Empty;
        ExceptionType = string.Empty;
    }

    public Crash(Exception exception)
    {
        var type = exception.GetType();

        Message = exception.Message;
        StackTrace = exception.StackTrace;
        ExceptionType = type.ToString();
        ExceptionAssembly = type.Assembly.FullName!;
        Source = exception.Source;
    }

    public string? Message { get; init; }
    public string? StackTrace { get; init; }
    public string ExceptionType { get; init; }
    public string ExceptionAssembly { get; init; }
    public string? Source { get; init; }

    public Exception? GetException()
    {

        try
        {
            Assembly assembly = Assembly.Load(ExceptionAssembly);
            Type type = assembly.GetType(ExceptionType)!;

            Exception? ex;

            try
            {
                ex = Activator.CreateInstance(type, args: Message) as Exception;
            }
            catch (MissingMethodException)
            {
                ex = Activator.CreateInstance(type) as Exception;
            }

            if (ex is not null)
            {
                ex.Source = Source;
            }

            return ex;
        }
        catch (Exception)
        {
            return null;
        }
    }
}