using System.Net;
using System.Text.Json;

namespace DotNetCampus.ModelContextProtocol.Transports;

public class HttpTransport
{
    private readonly HttpListener _listener = new();

    public HttpTransport(string urlPrefix)
    {
        _listener.Prefixes.Add(urlPrefix); // 例如 "http://localhost:8080/"
    }

    public async Task StartAsync(Func<JsonDocument, Task<JsonDocument>> handler)
    {
        _listener.Start();
        Console.WriteLine($"Listening on {_listener.Prefixes.First()}");

        while (true)
        {
            var ctx = await _listener.GetContextAsync();
            _ = Task.Run(async () =>
            {
                try
                {
                    using var reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding);
                    var body = await reader.ReadToEndAsync();
                    var requestJson = JsonDocument.Parse(body);

                    var responseJson = await handler(requestJson);

                    var bytes = JsonSerializer.SerializeToUtf8Bytes(responseJson);
                    ctx.Response.ContentType = "application/json";
                    await ctx.Response.OutputStream.WriteAsync(bytes);
                }
                catch (Exception ex)
                {
                    var err = JsonSerializer.SerializeToUtf8Bytes(new { error = ex.Message });
                    ctx.Response.StatusCode = 500;
                    await ctx.Response.OutputStream.WriteAsync(err);
                }
                finally
                {
                    ctx.Response.OutputStream.Close();
                }
            });
        }
    }
}
