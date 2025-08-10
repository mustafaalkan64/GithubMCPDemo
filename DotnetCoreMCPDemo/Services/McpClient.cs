using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using System.Linq;

namespace DotnetCoreMCPDemo.Services;

public class McpClient : IAsyncDisposable
{
    private readonly McpOptions _options;
    private readonly SemaphoreSlim _startLock = new(1, 1);
    private Process? _proc;
    private StreamWriter? _stdin;
    private Stream? _stdout;
    private readonly ConcurrentDictionary<int, TaskCompletionSource<string>> _pending = new();
    private int _idCounter = 0;
    private CancellationTokenSource? _readerCts;
    private Task? _readerTask;

    public McpClient(IOptions<McpOptions> options)
    {
        _options = options.Value;
    }

    public async Task EnsureStartedAsync(CancellationToken ct = default)
    {
        if (_proc is { HasExited: false } && _stdin != null && _stdout != null)
            return;

        await _startLock.WaitAsync(ct);
        try
        {
            if (_proc is { HasExited: false } && _stdin != null && _stdout != null)
                return;

            if (string.IsNullOrWhiteSpace(_options.Command))
                throw new InvalidOperationException("MCP Command boş olamaz. appsettings.json -> Mcp:Command ayarlayın.");

            var psi = new ProcessStartInfo
            {
                FileName = _options.Command,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = string.IsNullOrWhiteSpace(_options.WorkingDirectory) ? Environment.CurrentDirectory : _options.WorkingDirectory
            };

            if (_options.Args != null)
            {
                foreach (var a in _options.Args)
                    psi.ArgumentList.Add(a);
            }

            // Inherit env and optionally override/add
            if (_options.Env != null)
            {
                foreach (var kv in _options.Env)
                    psi.Environment[kv.Key] = kv.Value;
            }

            _proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _proc.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) Debug.WriteLine($"[MCP-ERR] {e.Data}"); };

            if (!_proc.Start())
                throw new InvalidOperationException("MCP server başlatılamadı.");

            _proc.BeginErrorReadLine();

            _stdin = new StreamWriter(_proc.StandardInput.BaseStream, new UTF8Encoding(false)) { AutoFlush = true };
            _stdout = _proc.StandardOutput.BaseStream;

            _readerCts = new CancellationTokenSource();
            _readerTask = Task.Run(() => ReaderLoopAsync(_readerCts.Token));

            // initialize
            var initParams = new InitializeParams
            {
                ClientInfo = new ClientInfo { Name = "dotnet-mcp-client", Version = "0.1.0" },
                Capabilities = new { }
            };
            _ = await SendRequestAsync<InitializeResult>("initialize", initParams, ct);
        }
        finally
        {
            _startLock.Release();
        }
    }

    public async Task<List<McpTool>> ListToolsAsync(CancellationToken ct = default)
    {
        await EnsureStartedAsync(ct);
        var res = await SendRequestAsync<ToolsListResult>("tools/list", new { }, ct);
        return res.Tools;
    }

    public async Task<object?> CallToolAsync(string name, object? args, CancellationToken ct = default)
    {
        await EnsureStartedAsync(ct);
        var res = await SendRequestAsync<ToolsCallResult>("tools/call", new ToolsCallParams { Name = name, Arguments = args }, ct);
        return res.Content;
    }

    public async Task<object?> ListRepositoriesAsync(CancellationToken ct = default)
    {
        await EnsureStartedAsync(ct);
        string? toolName = _options.RepoListToolName;
        if (string.IsNullOrWhiteSpace(toolName))
        {
            // Discover tool name heuristically
            var tools = await ListToolsAsync(ct);
            toolName = tools.FirstOrDefault(t => t.Name.Contains("repo", StringComparison.OrdinalIgnoreCase))?.Name
                       ?? tools.FirstOrDefault(t => t.Name.Contains("repository", StringComparison.OrdinalIgnoreCase))?.Name
                       ?? tools.FirstOrDefault(t => t.Name.Contains("list", StringComparison.OrdinalIgnoreCase))?.Name;
            if (string.IsNullOrWhiteSpace(toolName))
                throw new InvalidOperationException("Repo listeleme için uygun bir MCP aracı bulunamadı.");
        }
        return await CallToolAsync(toolName!, null, ct);
    }

    private async Task<T> SendRequestAsync<T>(string method, object? @params, CancellationToken ct)
    {
        var id = Interlocked.Increment(ref _idCounter);
        var req = new JsonRpcRequest { Id = id, Method = method, Params = @params };
        var json = JsonSerializer.Serialize(req);
        var payload = Encoding.UTF8.GetBytes(json);
        var header = Encoding.ASCII.GetBytes($"Content-Length: {payload.Length}\r\n\r\n");

        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;

        // write frame
        if (_stdin == null) throw new InvalidOperationException("stdin yok");
        await _stdin.BaseStream.WriteAsync(header, 0, header.Length, ct);
        await _stdin.BaseStream.WriteAsync(payload, 0, payload.Length, ct);
        await _stdin.BaseStream.FlushAsync(ct);

        using var timeoutCts = new CancellationTokenSource(_options.RequestTimeoutMs);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        await using var reg = linked.Token.Register(() => tcs.TrySetException(new TimeoutException($"MCP yanıtı zaman aşımı: {method}")));
        var raw = await tcs.Task.ConfigureAwait(false);

        var resp = JsonSerializer.Deserialize<JsonRpcResponse<T>>(raw) ?? throw new InvalidOperationException("Geçersiz JSON-RPC yanıtı");
        if (resp.Error != null)
            throw new InvalidOperationException($"RPC hata {resp.Error.Code}: {resp.Error.Message}");
        if (resp.Result == null)
            throw new InvalidOperationException("RPC yanıtı boş");
        return resp.Result;
    }

    private async Task ReaderLoopAsync(CancellationToken ct)
    {
        if (_stdout == null) return;
        var reader = _stdout;
        var headerBuf = new List<byte>();
        var buffer = new byte[8192];

        while (!ct.IsCancellationRequested)
        {
            // Read headers to get Content-Length
            int contentLength = -1;
            headerBuf.Clear();
            while (true)
            {
                var line = await ReadLineAsync(reader, ct);
                if (line == null) return; // stream closed
                if (line.Length == 0) // empty line after headers
                    break;
                var s = Encoding.ASCII.GetString(line);
                if (s.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                {
                    var val = s.Substring("Content-Length:".Length).Trim();
                    if (int.TryParse(val, out var len)) contentLength = len;
                }
            }
            if (contentLength < 0) continue;

            // Read body
            var data = new byte[contentLength];
            int read = 0;
            while (read < contentLength)
            {
                var n = await reader.ReadAsync(data, read, contentLength - read, ct);
                if (n == 0) return; // closed
                read += n;
            }

            var json = Encoding.UTF8.GetString(data);

            try
            {
                // Peek id to route to pending request
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.Number)
                {
                    var id = idProp.GetInt32();
                    if (_pending.TryRemove(id, out var tcs))
                    {
                        tcs.TrySetResult(json);
                    }
                }
                // else: notifications can be ignored for this simple client
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MCP-READER] parse error: {ex.Message}");
            }
        }
    }

    private static async Task<byte[]?> ReadLineAsync(Stream stream, CancellationToken ct)
    {
        var ms = new MemoryStream();
        int b;
        bool gotCR = false;
        while ((b = stream.ReadByte()) != -1)
        {
            if (b == '\r') { gotCR = true; continue; }
            if (b == '\n')
            {
                if (gotCR) break; // end of line
            }
            else
            {
                if (gotCR)
                {
                    // lone CR before non-LF, treat previous as end of line
                    stream.Position--; // not ideal for non-seekable; instead, we won't support this case
                }
                gotCR = false;
                ms.WriteByte((byte)b);
            }
        }
        if (ms.Length == 0 && b == -1) return null;
        return ms.ToArray();
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            _readerCts?.Cancel();
            if (_readerTask != null) await Task.WhenAny(_readerTask, Task.Delay(500));
            _stdin?.Dispose();
            _stdout?.Dispose();
            if (_proc != null && !_proc.HasExited) _proc.Kill(true);
            _proc?.Dispose();
        }
        catch { /* ignore */ }
    }
}
