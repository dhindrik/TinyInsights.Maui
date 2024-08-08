using Microsoft.Extensions.Logging;

namespace TinyInsights;

public static class InsightsExtension
{
    public static MauiAppBuilder UseTinyInsights(this MauiAppBuilder appBuilder)
    {
        appBuilder.Services.AddSingleton<IInsights, Insights>();
        appBuilder.Services.AddTransient<InsightsMessageHandler>();
        return appBuilder;
    }

    public static MauiAppBuilder UseTinyInsights(this MauiAppBuilder appBuilder, string applicationInsightsConnectionString, Action<IInsightsProvider>? configureProvider = null)
    {

        appBuilder.Services.AddSingleton<IInsights>((services) =>
        {
#if WINDOWS

            var provider = new ApplicationInsightsProvider(MauiWinUIApplication.Current, applicationInsightsConnectionString);
#else
            var provider = new ApplicationInsightsProvider(applicationInsightsConnectionString);
#endif
            configureProvider?.Invoke(provider);

            var insights = new Insights();
            insights.AddProvider(provider);

            return insights;
        });

        appBuilder.Services.AddTransient<InsightsMessageHandler>();

        return appBuilder;
    }

    public static MauiAppBuilder UseTinyInsightsAsILogger(this MauiAppBuilder appBuilder, string applicationInsightsConnectionString, Action<IInsightsProvider>? configureProvider = null)
    {

        appBuilder.Services.AddSingleton<ILogger>((services) =>
        {
#if WINDOWS
            var provider = new ApplicationInsightsProvider(MauiWinUIApplication.Current, applicationInsightsConnectionString);
#else
            var provider = new ApplicationInsightsProvider(applicationInsightsConnectionString);
#endif

            configureProvider?.Invoke(provider);

            return provider;
        });

        appBuilder.Services.AddTransient<InsightsMessageHandler>();

        return appBuilder;
    }

}