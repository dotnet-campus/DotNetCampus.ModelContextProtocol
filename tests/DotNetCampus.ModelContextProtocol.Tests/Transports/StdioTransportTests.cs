namespace DotNetCampus.ModelContextProtocol.Tests.Transports;

/// <summary>
/// Stdio 传输层特性测试：分包、粘包、Header 解析。
/// </summary>
/// <remarks>
/// 目前 Stdio 传输在本库中尚未完全实现。
/// 这些测试用例作为规划，待 Stdio 传输层完成后启用。
/// </remarks>
[TestClass]
public class StdioTransportTests
{
    // TODO: 待 Stdio 传输层实现后添加以下测试：
    //
    // [TestMethod("Receive_ChunkedJson: 分包 JSON 能正确拼合")]
    // public async Task Receive_ChunkedJson()
    // {
    //     // 将一个 JSON 报文拆成多个 byte 数组分次写入 Stream
    //     // 验证能够完整拼合并解析出消息
    // }
    //
    // [TestMethod("Receive_StickyPacket: 粘包 JSON 能正确分离")]
    // public async Task Receive_StickyPacket()
    // {
    //     // 将多个 JSON 报文一次性写入 Stream（粘包）
    //     // 验证能够依次触发多次消息接收
    // }
    //
    // [TestMethod("Receive_InvalidHeader: 错误的 Content-Length 应抛异常")]
    // public async Task Receive_InvalidHeader()
    // {
    //     // 写入错误的 Content-Length
    //     // 验证抛出协议异常或断开连接
    // }

    [TestMethod("Placeholder: Stdio 测试占位符")]
    public void Placeholder()
    {
        // 占位测试，确保测试类能够运行
        Assert.Inconclusive("Stdio 传输层测试尚未实现，待传输层完成后启用。");
    }
}
