using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.Extensions.Options;
using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel.ChatCompletion;

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
        #region Agent çalıştırılıyor.
        var chatHistory = new ChatHistory();
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
- search_repositories: GitHub'da repo arama (query parametresi gerekli)
- search_users: GitHub'da kullanıcı arama (q parametresi gerekli)
- get_repository: Belirli bir repo hakkında detay (owner ve repo parametreleri gerekli)

Kullanıcının sorusuna göre uygun aracı seç ve çağır, sonucu anlamlı şekilde özetle.
Eğer araç çağrısında hata alırsan, hatayı kullanıcıya açıkla ve alternatif çözüm öner.";

        var fullPrompt = $"{systemPrompt}\n\nKullanıcı sorusu: {userMessage}";

        var result = await _kernel.InvokePromptAsync(fullPrompt, new(settings), cancellationToken: ct);
        chatHistory.AddAssistantMessage(result.GetValue<string>());
        return result.GetValue<string>() ?? "Yanıt alınamadı.";
        #endregion
    }

    private async Task RegisterMcpToolsAsync(CancellationToken ct)
    {
        var toolNames = await _mcpService.ListToolNamesAsync(ct);

        foreach (var toolName in toolNames)
        {
            var pluginName = $"mcp_{toolName}";

            // Check if plugin already exists
            if (_kernel.Plugins.Any(p => p.Name == pluginName))
            {
                Console.WriteLine($"MCP plugin '{pluginName}' already exists, skipping...");
                continue;
            }

            // Her MCP aracını Semantic Kernel fonksiyonu olarak kaydet
            var mcpFunction = CreateMcpFunction(toolName, ct);
            _kernel.Plugins.AddFromFunctions(pluginName, [mcpFunction]);
        }
    }

    private KernelFunction CreateMcpFunction(string toolName, CancellationToken ct)
    {
        return toolName.ToLowerInvariant() switch
        {
            "search_repositories" => KernelFunctionFactory.CreateFromMethod(
                method: async (string q, int per_page = 10, int page = 1) =>
                {
                    try
                    {
                        var args = new Dictionary<string, object?>
                        {
                            ["query"] = q,
                            ["per_page"] = per_page,
                            ["page"] = page
                        };
                        var result = await _mcpService.CallToolAsync(toolName, args, ct);
                        return FormatRepositoryResults(result);
                    }
                    catch (Exception ex)
                    {
                        return $"Repository arama hatası: {ex.Message}";
                    }
                },
                functionName: "search_repositories",
                description: "GitHub'da repository arar. q parametresi zorunludur (örn: 'user:kullaniciadi' veya 'dotnet core')"
            ),

            "search_users" => KernelFunctionFactory.CreateFromMethod(
                method: async (string q, int per_page = 10, int page = 1) =>
                {
                    try
                    {
                        var args = new Dictionary<string, object?>
                        {
                            ["q"] = q,
                            ["per_page"] = per_page,
                            ["page"] = page
                        };
                        var result = await _mcpService.CallToolAsync(toolName, args, ct);
                        return FormatUserResults(result);
                    }
                    catch (Exception ex)
                    {
                        return $"Kullanıcı arama hatası: {ex.Message}";
                    }
                },
                functionName: "search_users",
                description: "GitHub'da kullanıcı arar. q parametresi zorunludur"
            ),

            "get_repository" => KernelFunctionFactory.CreateFromMethod(
                method: async (string owner, string repo) =>
                {
                    try
                    {
                        var args = new Dictionary<string, object?>
                        {
                            ["owner"] = owner,
                            ["repo"] = repo
                        };
                        var result = await _mcpService.CallToolAsync(toolName, args, ct);
                        return FormatRepositoryDetail(result);
                    }
                    catch (Exception ex)
                    {
                        return $"Repository detay hatası: {ex.Message}";
                    }
                },
                functionName: "get_repository",
                description: "Belirli bir GitHub repository'sinin detaylarını getirir. owner ve repo parametreleri zorunludur"
            ),

            _ => KernelFunctionFactory.CreateFromMethod(
                method: async (string query = "", int per_page = 10, int page = 1) =>
                {
                    try
                    {
                        var args = new Dictionary<string, object?>
                        {
                            ["q"] = query,
                            ["per_page"] = per_page,
                            ["page"] = page
                        };
                        var result = await _mcpService.CallToolAsync(toolName, args, ct);
                        return result?.ToString() ?? "Sonuç bulunamadı.";
                    }
                    catch (Exception ex)
                    {
                        return $"{toolName} aracı hatası: {ex.Message}";
                    }
                },
                functionName: toolName,
                description: $"GitHub {toolName} aracını çağırır"
            )
        };
    }

    private static string FormatRepositoryResults(object? result)
    {
        if (result == null) return "Repository bulunamadı.";

        try
        {
            var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
            // Basit formatting - gerçek uygulamada JSON parse edip özet çıkarabilirsiniz
            return $"Repository arama sonuçları:\n{json}";
        }
        catch
        {
            return result.ToString() ?? "Repository sonuçları formatlanamadı.";
        }
    }

    private static string FormatUserResults(object? result)
    {
        if (result == null) return "Kullanıcı bulunamadı.";

        try
        {
            var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
            return $"Kullanıcı arama sonuçları:\n{json}";
        }
        catch
        {
            return result.ToString() ?? "Kullanıcı sonuçları formatlanamadı.";
        }
    }

    private static string FormatRepositoryDetail(object? result)
    {
        if (result == null) return "Repository detayı bulunamadı.";

        try
        {
            var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
            return $"Repository detayları:\n{json}";
        }
        catch
        {
            return result.ToString() ?? "Repository detayı formatlanamadı.";
        }
    }
}
