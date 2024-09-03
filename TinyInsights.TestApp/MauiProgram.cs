﻿using Microsoft.Extensions.Logging;

namespace TinyInsights.TestApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            })
            .UseTinyInsights("InstrumentationKey=8b51208f-7926-4b7b-9867-16989206b950;IngestionEndpoint=https://swedencentral-0.in.applicationinsights.azure.com/;ApplicationId=0c04d3a0-9ee2-41a5-996e-526552dc730f",
                (provider) =>
                {
                    provider.IsTrackErrorsEnabled = true;
                    provider.IsTrackCrashesEnabled = true;
                    provider.IsTrackPageViewsEnabled = true;
                    provider.IsAutoTrackPageViewsEnabled = true;
                    provider.IsTrackEventsEnabled = true;
                    provider.IsTrackDependencyEnabled = true;
                    provider.IsAutoTrackPageViewsEnabled = true;
                })
            .UseTinyInsightsAsILogger("InstrumentationKey=8b51208f-7926-4b7b-9867-16989206b950;IngestionEndpoint=https://swedencentral-0.in.applicationinsights.azure.com/;ApplicationId=0c04d3a0-9ee2-41a5-996e-526552dc730f");

#if DEBUG
        builder.Logging.AddDebug();
#endif

        builder.Services.AddTransient<MainPage>();
        return builder.Build();
    }
}