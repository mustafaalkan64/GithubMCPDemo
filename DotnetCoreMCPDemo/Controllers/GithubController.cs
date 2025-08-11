using DotnetCoreMCPDemo.Services;
using DotnetCoreMCPDemo.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace DotnetCoreMCPDemo.Controllers;

[ApiController]
[Route("github")]
public class GithubController : ControllerBase
{
    private readonly IMcpRepoService _mcp;

    public GithubController(IMcpRepoService mcp)
    {
        _mcp = mcp;
    }

    [HttpGet("repos")]
    public async Task<IActionResult> GetRepos(CancellationToken ct)
    {
        var result = await _mcp.ListRepositoriesAsync(ct);
        return Ok(result);
    }

    [HttpGet("tools")]
    public async Task<IActionResult> GetTools(CancellationToken ct)
    {
        var tools = await _mcp.ListToolNamesAsync(ct);
        return Ok(tools);
    }
}