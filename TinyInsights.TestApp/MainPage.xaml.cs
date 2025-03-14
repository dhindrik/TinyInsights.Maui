﻿using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace TinyInsights.TestApp;

public partial class MainPage : ContentPage
{
    private readonly IInsights insights;
    private readonly ILogger logger;

    public MainPage(IInsights insights, ILogger logger)
    {
        this.insights = insights;
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
            OnPropertyChanged();
        }
    }

    private async void PageViewButton_OnClicked(object? sender, EventArgs e)
    {
        var data = new Dictionary<string, string>()
        {
            {"key", "value"},
            {"key2", "value2"}
        };

        if (UseILogger)
        {
            logger.LogTrace("MainView");
            return;
        }

        await insights.TrackPageViewAsync("MainView", data);
    }


    private async void EventButton_OnClicked(object? sender, EventArgs e)
    {
        if (UseILogger)
        {
            logger.LogInformation("EventButton");
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
                logger.LogError(ex, "ExceptionButton");
                return;
            }

            await insights.TrackErrorAsync(ex);
        }
    }

    private async void TrackHttpButton_OnClicked(object? sender, EventArgs e)
    {
        var client = new HttpClient();

        for (int i = 0; i < 10; i++)
        {
            _ = await client.GetAsync("https://google.se");
        }
    }

    private void CrashButton_OnClicked(object? sender, EventArgs e)
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

    private void CustomCrashButton_Clicked(object sender, EventArgs e)
    {
        throw new CustomException();
    }
}