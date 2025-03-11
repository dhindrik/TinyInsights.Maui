1.8.0 BREAKING CHANGE - CHANGED FLUSH BEHAVIOR
Before 1.8.0, the TinyInsights SDK would flush events to the server every time an event was logged. 
This was inefficient and could lead to performance issues in some cases.

If you still want that behavior, you need to call Flush manually after logging an event.
To avoid data to get lost, you should call Flush before the app goes to sleep.
You can do that by adding the code below to your App.xaml.cs file.

protected async override void OnSleep()
{
    base.OnSleep();

    var insights = serviceProvider.GetRequiredService<IInsights>();
    await insights.FlushAsync();
}
