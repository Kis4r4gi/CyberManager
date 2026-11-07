using CyberManager.Agent;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    // En modo servicio el directorio actual es System32; la configuración vive junto al .exe.
    ContentRootPath = AppContext.BaseDirectory
});
builder.Services.AddWindowsService(options => options.ServiceName = "CyberManagerAgent");
builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection("Agent"));
builder.Services.AddHostedService<AgentWorker>();

var host = builder.Build();
host.Run();
