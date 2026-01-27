using DotNetCampus.ModelContextProtocol.Transports;

namespace DotNetCampus.ModelContextProtocol.TouchSocket.Transports.TouchSocket;

public class TouchSocketServerTransport : IServerTransport
{
    public async Task<Task> StartAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public ValueTask DisposeAsync()
    {
        throw new NotImplementedException();
    }
}
