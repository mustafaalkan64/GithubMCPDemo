using System.Text.Json.Serialization;

namespace DotnetCoreMCPDemo.Services;

public class JsonRpcRequest
{
    [JsonPropertyName("jsonrpc")] public string JsonRpc { get; set; } = "2.0";
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("method")] public string Method { get; set; } = string.Empty;
    [JsonPropertyName("params")] public object? Params { get; set; }
}

public class JsonRpcResponse<T>
{
    [JsonPropertyName("jsonrpc")] public string JsonRpc { get; set; } = "2.0";
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("result")] public T? Result { get; set; }
    [JsonPropertyName("error")] public JsonRpcError? Error { get; set; }
}

public class JsonRpcError
{
    [JsonPropertyName("code")] public int Code { get; set; }
    [JsonPropertyName("message")] public string Message { get; set; } = string.Empty;
    [JsonPropertyName("data")] public object? Data { get; set; }
}

// MCP shapes (simplified)
public class InitializeParams
{
    [JsonPropertyName("clientInfo")] public ClientInfo ClientInfo { get; set; } = new();
    [JsonPropertyName("capabilities")] public object Capabilities { get; set; } = new { }; // minimal
}

public class ClientInfo
{
    [JsonPropertyName("name")] public string Name { get; set; } = "dotnet-mcp-client";
    [JsonPropertyName("version")] public string Version { get; set; } = "0.1.0";
}

public class InitializeResult
{
    [JsonPropertyName("serverInfo")] public ServerInfo ServerInfo { get; set; } = new();
    [JsonPropertyName("capabilities")] public object? Capabilities { get; set; }
}

public class ServerInfo
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("version")] public string Version { get; set; } = string.Empty;
}

public class ToolsListResult
{
    [JsonPropertyName("tools")] public List<McpTool> Tools { get; set; } = new();
}

public class McpTool
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string? Description { get; set; }
}

public class ToolsCallParams
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("arguments")] public object? Arguments { get; set; }
}

public class ToolsCallResult
{
    [JsonPropertyName("content")] public object? Content { get; set; }
}
