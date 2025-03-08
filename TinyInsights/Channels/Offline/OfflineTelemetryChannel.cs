using Microsoft.ApplicationInsights.Channel;

namespace TinyInsights.Channels.Offline;

public class OfflineTelemetryChannel : ITelemetryChannel
{
    public bool? DeveloperMode { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    public string EndpointAddress { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public void Flush()
    {
        throw new NotImplementedException();
    }

    public void Send(ITelemetry item)
    {
        throw new NotImplementedException();
    }
}
