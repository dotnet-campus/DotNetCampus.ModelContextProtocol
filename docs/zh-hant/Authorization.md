# Authorization

MCP Authorization 用於讓用戶端和伺服端在連線前或連線過程中完成身分認證和授權。

> 當前版本尚未提供內建的 Authorization 支援（規劃中）。在此期間，你可以透過自訂 HTTP 標頭（`HttpClientTransportOptions` 的 `HttpClient`）或 `WithRequestHandlers` 攔截器自行實作認證邏輯。
