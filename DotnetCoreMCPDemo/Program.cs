using DotnetCoreMCPDemo.Services;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();

// MCP options + client
builder.Services.Configure<McpOptions>(builder.Configuration.GetSection("Mcp"));
// builder.Services.AddSingleton<McpClient>(); // Replaced by SK-based service
builder.Services.AddSingleton<IMcpRepoService, SkMcpRepoService>();

// Configure OpenAI options
builder.Services.Configure<OpenAIOptions>(builder.Configuration.GetSection("OpenAI"));

// Register services
builder.Services.AddSingleton<IAIChatService, OpenAIChatService>();

var app = builder.Build();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
