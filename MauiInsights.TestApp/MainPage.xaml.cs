namespace MauiInsights.TestApp;

public partial class MainPage : ContentPage
{
    private readonly IInsights insights;
    private readonly InsightsMessageHandler insightsMessageHandler;

    public MainPage(IInsights insights, InsightsMessageHandler insightsMessageHandler)
    {
        this.insights = insights;
        this.insightsMessageHandler = insightsMessageHandler;
        InitializeComponent();
    }

    private async void PageViewButton_OnClicked(object? sender, EventArgs e)
    {
        await insights.TrackPageViewAsync("MainPage");
    }
    
    private async void EventButton_OnClicked(object? sender, EventArgs e)
    {
        await insights.TrackEventAsync("EventButton");
    }

    private async void ExceptionButton_OnClicked(object? sender, EventArgs e)
    {
        try
        {
            throw new Exception("Test excception");
        }
        catch (Exception ex)
        {
            await insights.TrackErrorAsync(ex);
        }
    }

    private async void TrackHttpButton_OnClicked(object? sender, EventArgs e)
    {
        var client = new HttpClient(insightsMessageHandler);
        
        for (int i = 0; i < 10; i++)
        {
            _ = await client.GetAsync("https://google.se");
        }
    }

    private void CrashButtom_OnClicked(object? sender, EventArgs e)
    {
        throw new Exception("Crash Boom Bang!");
    }
}