namespace DotnetCoreMCPDemo.Services;

public class McpOptions
{
    public string? Command { get; set; }
    public string[]? Args { get; set; }
    public string? WorkingDirectory { get; set; }
    // Optional environment variables to add/override for the MCP server process
    public Dictionary<string, string>? Env { get; set; }

    // Optional: explicit repo-list tool name; if empty, client will try to discover
    public string? RepoListToolName { get; set; }

    // Optional: arguments to pass when calling the repo-list tool
    public Dictionary<string, object?>? RepoListArguments { get; set; }

    // Timeout (ms) for requests
    public int RequestTimeoutMs { get; set; } = 15000;
}
