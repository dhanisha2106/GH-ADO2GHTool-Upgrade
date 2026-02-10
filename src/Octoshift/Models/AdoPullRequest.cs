using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Octoshift.Models;

/// <summary>
/// Represents an Azure DevOps Pull Request
/// </summary>
public class AdoPullRequest
{
    [JsonProperty("pullRequestId")]
    public int PullRequestId { get; set; }

    [JsonProperty("title")]
    public string Title { get; set; }

    [JsonProperty("description")]
    public string Description { get; set; }

    [JsonProperty("status")]
    public string Status { get; set; }

    [JsonProperty("createdBy")]
    public AdoIdentity CreatedBy { get; set; }

    [JsonProperty("creationDate")]
    public DateTime CreationDate { get; set; }

    [JsonProperty("closedDate")]
    public DateTime? ClosedDate { get; set; }

    [JsonProperty("sourceRefName")]
    public string SourceRefName { get; set; }

    [JsonProperty("targetRefName")]
    public string TargetRefName { get; set; }

    [JsonProperty("mergeStatus")]
    public string MergeStatus { get; set; }

    [JsonProperty("url")]
    public string Url { get; set; }
}

/// <summary>
/// Represents an Azure DevOps identity (user)
/// </summary>
public class AdoIdentity
{
    [JsonProperty("displayName")]
    public string DisplayName { get; set; }

    [JsonProperty("uniqueName")]
    public string UniqueName { get; set; }

    [JsonProperty("id")]
    public string Id { get; set; }
}

/// <summary>
/// Represents a comment thread in ADO
/// </summary>
public class AdoThread
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("comments")]
    public IEnumerable<AdoComment> Comments { get; init; }

    [JsonProperty("status")]
    public string Status { get; set; }
}

/// <summary>
/// Represents a comment in ADO
/// </summary>
public class AdoComment
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("content")]
    public string Content { get; set; }

    [JsonProperty("author")]
    public AdoIdentity Author { get; set; }

    [JsonProperty("publishedDate")]
    public DateTime PublishedDate { get; set; }

    [JsonProperty("commentType")]
    public string CommentType { get; set; }
}
