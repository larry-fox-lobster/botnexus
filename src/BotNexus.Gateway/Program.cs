using BotNexus.Gateway;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddBotNexus(builder.Configuration);

var host = builder.Build();
await host.RunAsync();
