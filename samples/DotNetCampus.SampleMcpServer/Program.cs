// See https://aka.ms/new-console-template for more information

using DotNetCampus.ModelContextProtocol.Transports;

var httpTransport = new HttpTransport("http://localhost:5942/");
await httpTransport.StartAsync(async d =>
{
    return d;
});
