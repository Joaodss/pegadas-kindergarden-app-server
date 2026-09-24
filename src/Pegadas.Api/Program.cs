using Pegadas.Api;
using Pegadas.Api.Hosting;
using Pegadas.BuildingBlocks;

var builder = WebApplication.CreateBuilder(args);

builder.AddPegadasConfiguration();
builder.AddPegadasTelemetry();
builder.AddPegadasApi();
builder.Services.AddPegadasBuildingBlocks(builder.Configuration);

foreach (var module in PegadasModules.All)
{
    module.AddServices(builder.Services, builder.Configuration);
}

var app = builder.Build();

app.UsePegadasPipeline();
app.MapPegadasEndpoints();

await app.RunAsync();

/// <summary>Entry point; public for <c>WebApplicationFactory</c> in the integration tests.</summary>
public partial class Program;
