using ModelContextProtocol.Client;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace DotnetCoreMCPDemo.Services;

public class SkMcpRepoService : IMcpRepoService, IAsyncDisposable
{
    private readonly McpOptions _options;
    private IMcpClient? _client;
    private readonly SemaphoreSlim _lock = new(1,1);

    public SkMcpRepoService(IOptions<McpOptions> options)
    {
        _options = options.Value;
    }

    private async Task<IMcpClient> GetClientAsync(CancellationToken ct)
    {
        if (_client is not null) return _client;
        await _lock.WaitAsync(ct);
        try
        {
            if (_client is not null) return _client;
            if (string.IsNullOrWhiteSpace(_options.Command))
                throw new InvalidOperationException("MCP Command boş olamaz. appsettings.json -> Mcp:Command ayarlayın.");

            // Expand any %ENV_VAR% placeholders from appsettings before passing to the server process
            var envVars = new Dictionary<string, string>();
            if (_options.Env is not null)
            {
                foreach (var kv in _options.Env)
                {
                    var expanded = Environment.ExpandEnvironmentVariables(kv.Value);
                    envVars[kv.Key] = expanded;
                }
            }

            var stdio = new StdioClientTransport(new()
            {
                Command = _options.Command!,
                Arguments = _options.Args ?? Array.Empty<string>(),
                Name = "GitHub MCP Server",
                WorkingDirectory = string.IsNullOrWhiteSpace(_options.WorkingDirectory) ? null : _options.WorkingDirectory,
                EnvironmentVariables = envVars
            });

            _client = await McpClientFactory.CreateAsync(stdio);
            return _client;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<string>> ListToolNamesAsync(CancellationToken ct = default)
    {
        var client = await GetClientAsync(ct);
        var tools = await client.ListToolsAsync();
        return tools.Select(t => t.Name).ToArray();
    }

    public async Task<object?> ListRepositoriesAsync(CancellationToken ct = default)
    {
        var client = await GetClientAsync(ct);
        string? toolName = _options.RepoListToolName;
        if (string.IsNullOrWhiteSpace(toolName))
        {
            var tools = await client.ListToolsAsync();
            toolName = tools.FirstOrDefault(t => t.Name.Contains("repo", StringComparison.OrdinalIgnoreCase))?.Name
                       ?? tools.FirstOrDefault(t => t.Name.Contains("repository", StringComparison.OrdinalIgnoreCase))?.Name
                       ?? tools.FirstOrDefault(t => t.Name.Contains("list", StringComparison.OrdinalIgnoreCase))?.Name;
            if (string.IsNullOrWhiteSpace(toolName))
                throw new InvalidOperationException("Repo listeleme için uygun bir MCP aracı bulunamadı.");
        }

        var args = NormalizeArguments(_options.RepoListArguments) ?? new Dictionary<string, object?>();
        var result = await client.CallToolAsync(toolName!, args);
        var content = result.Content;
        return content;
    }

    public async Task<object?> CallToolAsync(string toolName, Dictionary<string, object?>? arguments = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(toolName))
            throw new ArgumentException("Tool adı boş olamaz.", nameof(toolName));

        var client = await GetClientAsync(ct);
        var args = NormalizeArguments(arguments) ?? new Dictionary<string, object?>();
        var result = await client.CallToolAsync(toolName, args);
        return result.Content;
    }

    private static Dictionary<string, object?>? NormalizeArguments(Dictionary<string, object?>? input)
    {
        if (input is null) return null;
        var output = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in input)
        {
            output[kv.Key] = NormalizeValue(kv.Value);
        }
        return output;
    }

    private static object? NormalizeValue(object? value)
    {
        if (value is null) return null;

        // Unwrap JsonElement recursively
        if (value is JsonElement elem)
        {
            switch (elem.ValueKind)
            {
                case JsonValueKind.Object:
                    var obj = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    foreach (var p in elem.EnumerateObject())
                    {
                        obj[p.Name] = NormalizeValue(p.Value);
                    }
                    return obj;
                case JsonValueKind.Array:
                    var list = new List<object?>();
                    foreach (var item in elem.EnumerateArray())
                    {
                        list.Add(NormalizeValue(item));
                    }
                    return list;
                case JsonValueKind.String:
                    return NormalizeValue(elem.GetString());
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.Number:
                    if (elem.TryGetInt64(out var li)) return li;
                    return elem.GetDouble();
                case JsonValueKind.Null:
                default:
                    return null;
            }
        }

        // Convert strings to bool/number when applicable
        if (value is string s)
        {
            if (bool.TryParse(s, out var b)) return b;
            if (long.TryParse(s, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var l)) return l;
            if (double.TryParse(s, System.Globalization.NumberStyles.Float | System.Globalization.NumberStyles.AllowThousands, System.Globalization.CultureInfo.InvariantCulture, out var d)) return d;
            return s; // keep as string
        }

        // For dictionaries: normalize entries
        if (value is IDictionary<string, object?> dict)
        {
            var obj = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in dict)
                obj[kv.Key] = NormalizeValue(kv.Value);
            return obj;
        }

        // For arrays/lists
        if (value is IEnumerable<object?> arr && value is not string)
        {
            return arr.Select(NormalizeValue).ToList();
        }

        return value;
    }

    public async ValueTask DisposeAsync()
    {
        if (_client != null)
        {
            await _client.DisposeAsync();
            _client = null;
        }
    }
}
