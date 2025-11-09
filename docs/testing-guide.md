# MCP 示例服务器测试

这个文件包含了一些简单的测试脚本，用于验证 MCP 服务器的基本功能。

## 使用 curl 测试

### 测试 initialize 请求

```powershell
# 发送 initialize 请求
$body = @'
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "initialize",
  "params": {
    "protocolVersion": "2024-11-05",
    "capabilities": {},
    "clientInfo": {
      "name": "test-client",
      "version": "1.0.0"
    }
  }
}
'@

Invoke-WebRequest -Uri "http://localhost:5942/" -Method POST -Body $body -ContentType "application/json" | Select-Object -ExpandProperty Content
```

### 测试 ping 请求

```powershell
$body = @'
{
  "jsonrpc": "2.0",
  "id": 2,
  "method": "ping"
}
'@

Invoke-WebRequest -Uri "http://localhost:5942/" -Method POST -Body $body -ContentType "application/json" | Select-Object -ExpandProperty Content
```

### 测试 tools/list 请求

```powershell
$body = @'
{
  "jsonrpc": "2.0",
  "id": 3,
  "method": "tools/list"
}
'@

Invoke-WebRequest -Uri "http://localhost:5942/" -Method POST -Body $body -ContentType "application/json" | Select-Object -ExpandProperty Content
```

## 使用 MCP Inspector

更推荐使用 MCP Inspector 进行调试，它提供了更好的用户界面：

```powershell
npx @modelcontextprotocol/inspector http://localhost:5942/
```

Inspector 会自动在浏览器中打开，你可以：
1. 查看 initialize 响应
2. 探索服务器能力
3. 调用各种方法
4. 查看请求/响应的 JSON 格式
