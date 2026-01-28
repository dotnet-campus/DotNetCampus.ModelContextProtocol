using DotNetCampus.ModelContextProtocol.Protocol.Messages.JsonRpc;

namespace DotNetCampus.ModelContextProtocol.Transports.Http;

public class HttpClientTransport : IClientTransport
{
    public HttpClientTransport(IClientTransportManager manager, HttpClientTransportOptions options)
    {
        throw new NotImplementedException();
    }

    public ValueTask ConnectAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public ValueTask SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask DisposeAsync()
    {
        throw new NotImplementedException();
    }
}
