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

    public static MauiAppBuilder UseTinyInsights(this MauiAppBuilder appBuilder, string applicationInsightsConnectionString)
    {
        return UseTinyInsights(appBuilder, applicationInsightsConnectionString, null);
    }

    public static MauiAppBuilder UseTinyInsights(this MauiAppBuilder appBuilder, string? applicationInsightsConnectionString = null, Action<IInsightsProvider>? configureProvider = null)
    {
        return UseTinyInsights(appBuilder, applicationInsightsConnectionString, configureProvider, null);
    }

    private static MauiAppBuilder UseTinyInsights(this MauiAppBuilder appBuilder, string? applicationInsightsConnectionString = null, Action<IInsightsProvider>? configureProvider = null, Action<IInsightsProvider, IServiceProvider>? configureProviderWithServiceCollection = null)
    {
        appBuilder.Services.AddSingleton<IInsights>((serviceProvider) =>
        {
#if WINDOWS

            var provider = new ApplicationInsightsProvider(MauiWinUIApplication.Current, applicationInsightsConnectionString);
#elif ANDROID || IOS || MACCATALYST
            var provider = new ApplicationInsightsProvider(applicationInsightsConnectionString);
#else
            var provider = new ApplicationInsightsProvider();
#endif

            if (configureProviderWithServiceCollection is not null)
            {
                configureProviderWithServiceCollection.Invoke(provider, serviceProvider);
            }
            else if (configureProvider is not null)
            {
                configureProvider.Invoke(provider);
            }

            provider.Initialize();

            var insights = new Insights();
            insights.AddProvider(provider);

            return insights;
        });

        appBuilder.Services.AddTransient<InsightsMessageHandler>();

        return appBuilder;
    }


    public static MauiAppBuilder UseTinyInsightsAsILogger(this MauiAppBuilder appBuilder, string? applicationInsightsConnectionString = null, Action<IInsightsProvider>? configureProvider = null)
    {
        appBuilder.Services.AddSingleton<ILogger>((_) =>
        {
#if WINDOWS
            var provider = new ApplicationInsightsProvider(MauiWinUIApplication.Current, applicationInsightsConnectionString);
#elif ANDROID || IOS || MACCATALYST
            var provider = new ApplicationInsightsProvider(applicationInsightsConnectionString);
#else
            var provider = new ApplicationInsightsProvider();
#endif

            configureProvider?.Invoke(provider);

            provider.Initialize();

            return provider;
        });

        appBuilder.Services.AddTransient<InsightsMessageHandler>();

        return appBuilder;
    }
}