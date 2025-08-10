using DotnetCoreMCPDemo.Services;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// MCP options + client
builder.Services.Configure<McpOptions>(builder.Configuration.GetSection("Mcp"));
// builder.Services.AddSingleton<McpClient>(); // Replaced by SK-based service
builder.Services.AddSingleton<IMcpRepoService, SkMcpRepoService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
