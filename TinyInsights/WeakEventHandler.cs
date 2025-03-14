namespace TinyInsights;

internal class WeakEventHandler<TEventArgs>
{
    private readonly WeakReference<EventHandler<TEventArgs>> weakHandler;

    public WeakEventHandler(EventHandler<TEventArgs> handler)
    {
        WeakEventManager test = new WeakEventManager();
        weakHandler = new WeakReference<EventHandler<TEventArgs>>(handler);
    }

    public void Handler(object? sender, TEventArgs e)
    {
        if (weakHandler.TryGetTarget(out var handler))
        {
            handler(sender, e);
        }
    }
}

