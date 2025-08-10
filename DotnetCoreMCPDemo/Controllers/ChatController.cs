using DotnetCoreMCPDemo.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;

namespace DotnetCoreMCPDemo.Controllers
{
    [ApiController]
    [Route("chat")]
    public class ChatController : ControllerBase
    {
        private readonly IMcpRepoService _mcp;
        private readonly IAIChatService _aiChat;

        public ChatController(IMcpRepoService mcp, IAIChatService aiChat)
        {
            _mcp = mcp;
            _aiChat = aiChat;
        }

        // List all available MCP tools for discovery
        [HttpGet("tools")]
        public async Task<IActionResult> GetTools(CancellationToken ct)
        {
            var tools = await _mcp.ListToolNamesAsync(ct);
            return Ok(tools);
        }

        // Generic tool call: provide toolName and optional arguments
        [HttpPost("call")]
        public async Task<IActionResult> CallTool([FromBody] ToolCallRequest request, CancellationToken ct)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.ToolName))
                return BadRequest("toolName zorunludur.");

            var content = await _mcp.CallToolAsync(request.ToolName, request.Arguments, ct);
            return Ok(new ToolCallResponse { ToolName = request.ToolName, Content = content });
        }

        // OpenAI Chat Completion ile basit sohbet
        [HttpPost("ai")]
        public async Task<IActionResult> AiChat([FromBody] PromptRequest request, CancellationToken ct)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Prompt))
                return BadRequest("prompt zorunludur.");

            try
            {
                var response = await _aiChat.ChatAsync(request.Prompt, ct);
                return Ok(new { response });
            }
            catch (Exception ex)
            {
                return Problem($"AI chat hatası: {ex.Message}");
            }
        }

        // OpenAI Chat Completion + MCP Tools entegrasyonu
        [HttpPost("ai-mcp")]
        public async Task<IActionResult> AiMcpChat([FromBody] PromptRequest request, CancellationToken ct)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Prompt))
                return BadRequest("prompt zorunludur.");

            try
            {
                var response = await _aiChat.ChatWithMcpToolsAsync(request.Prompt, ct);
                return Ok(new { response });
            }
            catch (Exception ex)
            {
                return Problem($"AI-MCP chat hatası: {ex.Message}");
            }
        }

        // Prompt tabanlı basit agent: doğal dilden uygun MCP aracını seçip çağırır
        // Şimdilik GitHub repo arama (search_repositories) için yönlendirme yapar
        [HttpPost("ask")]
        public async Task<IActionResult> Ask([FromBody] PromptRequest request, CancellationToken ct)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Prompt))
                return BadRequest("prompt zorunludur.");

            var tools = await _mcp.ListToolNamesAsync(ct);
            var searchTool = tools.FirstOrDefault(t => string.Equals(t, "search_repositories", StringComparison.OrdinalIgnoreCase)
                                                    || string.Equals(t, "search-repositories", StringComparison.OrdinalIgnoreCase));

            if (searchTool == null)
            {
                return BadRequest(new
                {
                    message = "Uygun araç bulunamadı. Lütfen önce /chat/tools ile mevcut araçları kontrol edin.",
                    availableTools = tools
                });
            }

            var (query, perPage, page) = ExtractGitHubSearchArgs(request.Prompt);
            var args = new Dictionary<string, object?>
            {
                ["query"] = query,
                ["per_page"] = perPage,
                ["page"] = page
            };

            var content = await _mcp.CallToolAsync(searchTool, args, ct);
            return Ok(new
            {
                tool = searchTool,
                usedArgs = args,
                content
            });
        }

        private static (string query, int perPage, int page) ExtractGitHubSearchArgs(string prompt)
        {
            // Varsayılanlar
            int perPage = 10;
            int page = 1;
            string query = "user:mustafaalkan64"; // güvenli varsayılan

            var p = prompt.ToLowerInvariant();

            // user:qualifier geçiyorsa doğrudan al
            var userQualifier = Regex.Match(prompt, @"user:\s*([\w-]+)", RegexOptions.IgnoreCase);
            if (userQualifier.Success)
            {
                query = $"user:{userQualifier.Groups[1].Value}";
            }
            else
            {
                // '@kullanici' formunu yakala
                var atUser = Regex.Match(prompt, @"@([\w-]+)");
                if (atUser.Success)
                {
                    query = $"user:{atUser.Groups[1].Value}";
                }
            }

            // "ilk 5", "top 5", "5 repo" gibi limitleri yakala
            var number = Regex.Match(prompt, @"(ilk|top)?\s*(\d{1,3})\s*(repo|repository|depo)?", RegexOptions.IgnoreCase);
            if (number.Success && int.TryParse(number.Groups[2].Value, out var n) && n > 0)
            {
                perPage = Math.Min(n, 100);
            }

            // Sayfa numarası
            var pageMatch = Regex.Match(prompt, @"sayfa\s*(\d{1,4})", RegexOptions.IgnoreCase);
            if (pageMatch.Success && int.TryParse(pageMatch.Groups[1].Value, out var pg) && pg > 0)
            {
                page = pg;
            }

            // Basit anahtar kelime araması: prompt içeriğini de aramaya kat
            // Örn: "dotnet core" → name,description araması
            var keywords = ExtractKeywords(prompt);
            if (!string.IsNullOrWhiteSpace(keywords))
            {
                // GitHub Search API sorgu örneği: user:xxx in:name,description dotnet core
                query = $"{query} in:name,description {keywords}";
            }

            return (query, perPage, page);
        }

        private static string ExtractKeywords(string prompt)
        {
            // user: veya @user ve bilinen Türkçe kalıpları temizleyip kalan kelimeleri anahtar olarak kullan
            var cleaned = Regex.Replace(prompt, @"user:\s*[\w-]+", string.Empty, RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"@[\w-]+", string.Empty);
            cleaned = Regex.Replace(cleaned, @"(repo(lar)?|depo(lar)?|repository|en popüler|popüler|yıldız|star|sayfa\s*\d+)", string.Empty, RegexOptions.IgnoreCase);
            cleaned = cleaned.Trim();
            // Çok kısa ise boş döndür
            if (cleaned.Length < 3) return string.Empty;
            return cleaned;
        }
    }

    public class ToolCallRequest
    {
        public string ToolName { get; set; } = string.Empty;
        public Dictionary<string, object?>? Arguments { get; set; }
    }

    public class ToolCallResponse
    {
        public string ToolName { get; set; } = string.Empty;
        public object? Content { get; set; }
    }

    public class PromptRequest
    {
        public string Prompt { get; set; } = string.Empty;
    }
}
