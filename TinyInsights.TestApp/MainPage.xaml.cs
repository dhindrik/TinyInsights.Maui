using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace TinyInsights.TestApp;

public partial class MainPage : ContentPage
{
    private readonly IInsights insights;
    private readonly InsightsMessageHandler insightsMessageHandler;
    private readonly ILogger logger;

    public MainPage(IInsights insights, InsightsMessageHandler insightsMessageHandler, ILogger logger)
    {
        this.insights = insights;
        this.insightsMessageHandler = insightsMessageHandler;
        this.logger = logger;

        BindingContext = this;
        insights.OverrideAnonymousUserId("TestUser");

        InitializeComponent();
    }

    private bool useILogger;
    public bool UseILogger
    {
        get => useILogger;
        set
        {
            useILogger = value;
            OnPropertyChanged(nameof(UseILogger));
        }
    }

    private async void PageViewButton_OnClicked(object? sender, EventArgs e)
    {
        if (UseILogger)
        {
            logger.LogTrace("MainView");
            return;
        }

        var data = new Dictionary<string, string>()
        {
            {"key", "value"},
            {"key2", "value2"}
        };

        await insights.TrackPageViewAsync("MainView", data);
    }


    private async void EventButton_OnClicked(object? sender, EventArgs e)
    {
        if (UseILogger)
        {
            logger.LogInformation("EventButton");
            logger.LogDebug("EventButton clicked");
            return;
        }

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
            if (UseILogger)
            {
                logger.LogError(ex, ex.Message);
                return;
            }

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

    private async void NewPageButton_Clicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new NewPage());
    }

    private void Button_Clicked(object sender, EventArgs e)
    {
        Debug.WriteLine($"Memory: {GC.GetTotalMemory(true)}");
    }
}