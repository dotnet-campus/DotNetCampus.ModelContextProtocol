using DotNetCampus.ModelContextProtocol.CompilerServices;

namespace DotNetCampus.SampleMcpServer;

[GenerateMcpTransport(McpTransportPackageId.TouchSocketHttp, McpSide.Server)]
public partial class TouchSocketHttpServerTransport;
