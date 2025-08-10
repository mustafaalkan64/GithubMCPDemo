using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.Extensions.Options;
using System.ComponentModel;

namespace DotnetCoreMCPDemo.Services;

public class OpenAIChatService : IAIChatService
{
    private readonly Kernel _kernel;
    private readonly IMcpRepoService _mcpService;
    private readonly OpenAIOptions _options;

    public OpenAIChatService(IOptions<OpenAIOptions> options, IMcpRepoService mcpService)
    {
        _options = options.Value;
        _mcpService = mcpService;

        var builder = Kernel.CreateBuilder();
        
        // Expand environment variables for API key
        var apiKey = Environment.ExpandEnvironmentVariables(_options.ApiKey);
        builder.AddOpenAIChatCompletion(_options.ModelId, apiKey);
        
        _kernel = builder.Build();
    }

    public async Task<string> ChatAsync(string userMessage, CancellationToken ct = default)
    {
        var result = await _kernel.InvokePromptAsync(userMessage, cancellationToken: ct);
        return result.GetValue<string>() ?? "Yanıt alınamadı.";
    }

    public async Task<string> ChatWithMcpToolsAsync(string userMessage, CancellationToken ct = default)
    {
        // MCP araçlarını Semantic Kernel fonksiyonları olarak kaydet
        await RegisterMcpToolsAsync(ct);

        var settings = new OpenAIPromptExecutionSettings
        {
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
            Temperature = 0.7,
            MaxTokens = 1000
        };

        var systemPrompt = @"Sen GitHub MCP sunucusuyla entegre edilmiş yardımcı bir asistansın. 
Kullanıcının GitHub repoları hakkındaki sorularını yanıtlamak için mevcut araçları kullanabilirsin.
Türkçe yanıt ver ve kullanıcıya yardımcı ol.

Mevcut araçlar:
- search_repositories: GitHub'da repo arama
- Diğer MCP araçları

Kullanıcının sorusuna göre uygun aracı seç ve çağır, sonucu anlamlı şekilde özetle.";

        var fullPrompt = $"{systemPrompt}\n\nKullanıcı sorusu: {userMessage}";
        
        var result = await _kernel.InvokePromptAsync(fullPrompt, new(settings), cancellationToken: ct);
        return result.GetValue<string>() ?? "Yanıt alınamadı.";
    }

    private async Task RegisterMcpToolsAsync(CancellationToken ct)
    {
        try
        {
            var toolNames = await _mcpService.ListToolNamesAsync(ct);
            
            foreach (var toolName in toolNames)
            {
                // Her MCP aracını Semantic Kernel fonksiyonu olarak kaydet
                var mcpFunction = KernelFunctionFactory.CreateFromMethod(
                    method: async (string query, int per_page = 10, int page = 1) =>
                    {
                        var args = new Dictionary<string, object?>
                        {
                            ["query"] = query,
                            ["per_page"] = per_page,
                            ["page"] = page
                        };
                        var result = await _mcpService.CallToolAsync(toolName, args, ct);
                        return result?.ToString() ?? "Sonuç bulunamadı.";
                    },
                    functionName: toolName,
                    description: $"GitHub {toolName} aracını çağırır"
                );

                _kernel.Plugins.AddFromFunctions($"mcp_{toolName}", [mcpFunction]);
            }
        }
        catch (Exception ex)
        {
            // MCP araçları yüklenemezse sessizce devam et
            Console.WriteLine($"MCP araçları yüklenirken hata: {ex.Message}");
        }
    }
}
