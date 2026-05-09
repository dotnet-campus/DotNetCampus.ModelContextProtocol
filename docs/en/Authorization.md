# Authorization

MCP Authorization enables the client and server to complete authentication and authorization before or during a connection.

> The current version does not yet provide built-in Authorization support (planned). In the meantime, you can implement authentication logic yourself using custom HTTP headers (via `HttpClientTransportOptions`'s `HttpClient`) or `WithRequestHandlers` interceptors.
