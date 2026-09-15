# 13 - Authorization and OAuth 2.1

## 目标

为 HTTP transport 提供符合 MCP `2025-11-25` Authorization 规范的客户端与服务端实现。

## 前置任务

- 12 - Streamable HTTP compliance and resumability

## 任务列表

- [ ] 服务端支持 401 和规范的 `WWW-Authenticate`。
- [ ] 提供 OAuth Protected Resource Metadata。
- [ ] 支持授权服务器 metadata discovery。
- [ ] 客户端实现 authorization server 发现和 token 获取。
- [ ] 支持 token refresh、resource indicator 和 token audience 校验。
- [ ] 支持 OAuth/OIDC metadata 兼容路径。
- [ ] HTTP options 提供标准认证配置和可替换 token storage。
- [ ] 建立 fake authorization server 端到端测试。
- [ ] 补齐安全文档和错误处理。

## 完成标准

- 客户端能从受保护 MCP server 的 401 响应自动发现授权信息。
- 获取的 token 绑定正确 resource，服务端验证 audience。
- token 过期、刷新失败、权限不足和 metadata 异常均有测试。

## 官方依据

- [Authorization](https://modelcontextprotocol.io/specification/2025-11-25/basic/authorization)

## 当前代码证据

- 项目文档明确尚无内置 Authorization：`docs/zh-hans/Authorization.md:5`。
- `src` 中没有 OAuth、OIDC、Protected Resource Metadata 或 `WWW-Authenticate` 的协议实现。

