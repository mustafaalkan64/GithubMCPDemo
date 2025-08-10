using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DotnetCoreMCPDemo.Services;

public interface IMcpRepoService
{
    Task<IReadOnlyList<string>> ListToolNamesAsync(CancellationToken ct = default);
    Task<object?> ListRepositoriesAsync(CancellationToken ct = default);
    Task<object?> CallToolAsync(string toolName, Dictionary<string, object?>? arguments = null, CancellationToken ct = default);
}
