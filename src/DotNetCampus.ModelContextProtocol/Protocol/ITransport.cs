using System.Threading.Channels;
using DotNetCampus.ModelContextProtocol.Messages;

namespace DotNetCampus.ModelContextProtocol.Protocol;

public interface ITransport : IAsyncDisposable
{
    string? SessionId { get; }

    ChannelReader<JsonRpcMessage> MessageReader { get; }

    Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default);
}
