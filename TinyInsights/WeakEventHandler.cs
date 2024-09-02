namespace TinyInsights;

internal class WeakEventHandler<TEventArgs>
{
    private readonly WeakReference<EventHandler<TEventArgs>> _weakHandler;

    public WeakEventHandler(EventHandler<TEventArgs> handler)
    {
        _weakHandler = new WeakReference<EventHandler<TEventArgs>>(handler);
    }

    public void Handler(object sender, TEventArgs e)
    {
        if (_weakHandler.TryGetTarget(out var handler))
        {
            handler(sender, e);
        }
    }
}

