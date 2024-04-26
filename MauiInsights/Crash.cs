namespace MauiInsights;

public class Crash
{
    public Crash()
    {
    }

    public Crash(Exception exception)
    {
        Message = exception.Message;
        StackTrace = exception.StackTrace;
        ExceptionType = exception.GetType().ToString();
        Source = exception.Source;
    }

    public string Message { get; init; }
    public string StackTrace { get; init; }
    public string ExceptionType { get; init; }
    public string Source { get; init; }

    public Exception GetException()
    {
        var ex = (Exception)Activator.CreateInstance(Type.GetType(ExceptionType), args: Message);
        ex.Source = Source;

        return ex;
    }
}