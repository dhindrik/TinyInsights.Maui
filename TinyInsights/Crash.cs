namespace TinyInsights;

public class Crash
{
    public Crash(Exception exception)
    {
        Message = exception.Message;
        StackTrace = exception.StackTrace;
        ExceptionType = exception.GetType().ToString();
        Source = exception.Source;
    }

    public string Message { get; init; }
    public string? StackTrace { get; init; }
    public string ExceptionType { get; init; }
    public string? Source { get; init; }

    public Exception? GetException()
    {
        var type = Type.GetType(ExceptionType);

        if(type is null)
        {
            return null;
        }

        var ex = Activator.CreateInstance(type, args: Message) as Exception;

        if(ex is not null)
        {
            ex.Source = Source;
        }

        return ex;
    }
}