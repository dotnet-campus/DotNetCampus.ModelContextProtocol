namespace DotNetCampus.ModelContextProtocol.Tests.Transports;

/// <summary>
/// Stdio 传输层特性测试：换行分隔 JSON-RPC 消息读取。
/// </summary>
/// <remarks>
/// 当前 Stdio 传输层直接绑定 Console 标准输入输出；后续若支持 Stream 注入，可在此补充无需外部进程的换行分隔消息测试。
/// </remarks>
[TestClass]
public class StdioTransportTests
{
    // TODO: 待 Stdio 传输层支持 Stream 注入后添加以下测试：
    //
    // [TestMethod("Receive_LineDelimitedJson: 单行 JSON 能正确解析")]
    // public async Task Receive_LineDelimitedJson()
    // {
    //     // 写入一行完整 JSON-RPC 消息，以 \n 结束。
    //     // 验证能够解析出消息。
    // }
    //
    // [TestMethod("Receive_MultipleLines: 连续多行 JSON 能逐条解析")]
    // public async Task Receive_MultipleLines()
    // {
    //     // 连续写入多条以 \n 分隔的 JSON-RPC 消息。
    //     // 验证能够依次触发多次消息处理。
    // }
    //
    // [TestMethod("Receive_InvalidMessage: 非 JSON-RPC 消息会返回 InvalidRequest")]
    // public async Task Receive_InvalidMessage()
    // {
    //     // 写入无法解析为 JSON-RPC 的消息。
    //     // 验证服务端返回 InvalidRequest 错误响应。
    // }

    [TestMethod("Placeholder: Stdio 测试占位符")]
    public void Placeholder()
    {
        // 占位测试，确保测试类能够运行
        Assert.Inconclusive("Stdio 传输层 Stream 注入测试尚未实现，待支持后启用。");
    }
}
