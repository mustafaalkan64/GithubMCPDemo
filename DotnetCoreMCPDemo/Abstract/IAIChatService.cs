namespace DotnetCoreMCPDemo.Services;

public interface IAIChatService
{
    Task<string> ChatAsync(string userMessage, CancellationToken ct = default);
    Task<string> ChatWithMcpToolsAsync(string userMessage, CancellationToken ct = default);
}
