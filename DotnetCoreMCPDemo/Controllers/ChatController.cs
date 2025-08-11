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


        // OpenAI Chat Completion + MCP Tools entegrasyonu
        [HttpPost("ask")]
        public async Task<IActionResult> AiMcpChat([FromBody] PromptRequest request, CancellationToken ct)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Prompt))
                return BadRequest("prompt zorunludur.");

            var response = await _aiChat.ChatWithMcpToolsAsync(request.Prompt, ct);
            return Ok(new { response });

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

    public class TestSearchRequest
    {
        public string Username { get; set; } = string.Empty;
        public int? PerPage { get; set; }
    }
}
