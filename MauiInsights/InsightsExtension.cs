namespace MauiInsights;

public static class InsightsExtension
{
    public static MauiAppBuilder UseMauiInsights(this MauiAppBuilder appBuilder)
    {
        appBuilder.Services.AddSingleton<IInsights, Insights>();
        return appBuilder;
    }
    
    public static MauiAppBuilder UseMauiInsights(this MauiAppBuilder appBuilder, string applicationInsightsConnectionString, Action<IInsightsProvider>? configureProvider = null)
    {
        UseMauiInsights(appBuilder);

        appBuilder.Services.AddSingleton<IInsightsProvider>((services) =>
        {
            var provider = new ApplicationInsightsProvider(applicationInsightsConnectionString);
            
            configureProvider?.Invoke(provider);

            var insights = services.GetRequiredService<IInsights>();
            insights.AddProvider(provider);

            return provider;
        });

        appBuilder.Services.AddTransient<InsightsMessageHandler>();
        
        return appBuilder;
    }
}