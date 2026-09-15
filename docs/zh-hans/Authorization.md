# Authorization

MCP Authorization 用于让客户端和服务端在连接前或连接过程中完成身份认证和授权。

> 当前版本尚未提供内置的 Authorization 支持（规划中）。在此期间，你可以通过自定义 HTTP 头（`HttpClientTransportOptions` 的 `HttpClient`）或 `WithRequestHandlers` 拦截器自行实现认证逻辑。
