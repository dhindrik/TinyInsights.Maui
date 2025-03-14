namespace TinyInsights;

public class InsightsMessageHandler : DelegatingHandler
{
    private readonly IInsights insights;

    public InsightsMessageHandler(IInsights insights)
    {
        this.insights = insights;

        InnerHandler = new HttpClientHandler();
    }

    public InsightsMessageHandler(IInsights insights, DelegatingHandler? innerHandler)
    {
        this.insights = insights;

        if (innerHandler is not null)
        {
            InnerHandler = innerHandler;
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var startTime = DateTime.Now;

        try
        {
            request.Headers.Add("Request-Id", Guid.NewGuid().ToString());

            var response = await base.SendAsync(request, cancellationToken);

            var endTime = DateTime.Now;

            Exception? exception = null;

            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch (Exception e)
            {
                exception = e;
            }

            await insights.TrackDependencyAsync(
                "HTTP",
                request.RequestUri?.Host ?? "Unknown host",
                request.RequestUri?.ToString() ?? string.Empty,
                request.Method,
                startTime,
                endTime - startTime,
                response.IsSuccessStatusCode,
                (int)response.StatusCode,
                exception);

            return response;
        }
        catch (Exception ex)
        {
            var endTime = DateTime.Now;
            await insights.TrackDependencyAsync(
                "HTTP",
                request.RequestUri?.Host ?? "Unknown host",
                request.RequestUri?.ToString() ?? string.Empty,
                startTime,
                endTime - startTime,
                false,
                0,
                ex);

            throw;
        }
    }
}