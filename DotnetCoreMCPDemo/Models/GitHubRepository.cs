using System.Text.Json.Serialization;

namespace DotnetCoreMCPDemo.Models;

public class GitHubRepository
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("full_name")]
    public string FullName { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = string.Empty;

    [JsonPropertyName("clone_url")]
    public string CloneUrl { get; set; } = string.Empty;

    [JsonPropertyName("ssh_url")]
    public string SshUrl { get; set; } = string.Empty;

    [JsonPropertyName("private")]
    public bool IsPrivate { get; set; }

    [JsonPropertyName("fork")]
    public bool IsFork { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [JsonPropertyName("pushed_at")]
    public DateTime? PushedAt { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("stargazers_count")]
    public int StargazersCount { get; set; }

    [JsonPropertyName("watchers_count")]
    public int WatchersCount { get; set; }

    [JsonPropertyName("forks_count")]
    public int ForksCount { get; set; }

    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("default_branch")]
    public string DefaultBranch { get; set; } = "main";

    [JsonPropertyName("topics")]
    public string[] Topics { get; set; } = Array.Empty<string>();
}

public class GitHubRepositorySearchResponse
{
    [JsonPropertyName("total_count")]
    public int TotalCount { get; set; }

    [JsonPropertyName("incomplete_results")]
    public bool IncompleteResults { get; set; }

    [JsonPropertyName("items")]
    public GitHubRepository[] Items { get; set; } = Array.Empty<GitHubRepository>();
}
