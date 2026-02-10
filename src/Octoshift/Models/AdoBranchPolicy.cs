using System.Collections.Generic;
using Newtonsoft.Json;

namespace Octoshift.Models;

/// <summary>
/// Represents an Azure DevOps branch policy configuration
/// </summary>
public class AdoBranchPolicy
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("type")]
    public AdoPolicyType Type { get; set; }

    [JsonProperty("isEnabled")]
    public bool IsEnabled { get; set; }

    [JsonProperty("settings")]
    public AdoBranchPolicySettings Settings { get; set; }
}

/// <summary>
/// Represents the type information for an Azure DevOps policy
/// </summary>
public class AdoPolicyType
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("displayName")]
    public string DisplayName { get; set; }
}

/// <summary>
/// Represents the settings for an Azure DevOps branch policy
/// </summary>
public class AdoBranchPolicySettings
{
    [JsonProperty("buildDefinitionId")]
    public string BuildDefinitionId { get; set; }

    [JsonProperty("displayName")]
    public string DisplayName { get; set; }

    [JsonProperty("queueOnSourceUpdateOnly")]
    public bool QueueOnSourceUpdateOnly { get; set; }

    [JsonProperty("manualQueueOnly")]
    public bool ManualQueueOnly { get; set; }

    [JsonProperty("validDuration")]
    public double ValidDuration { get; set; }

    [JsonProperty("minimumApproverCount")]
    public int? MinimumApproverCount { get; set; }

    [JsonProperty("creatorVoteCounts")]
    public bool? CreatorVoteCounts { get; set; }

    [JsonProperty("allowDownvotes")]
    public bool? AllowDownvotes { get; set; }

    [JsonProperty("resetOnSourcePush")]
    public bool? ResetOnSourcePush { get; set; }

    [JsonProperty("requireVoteOnLastIteration")]
    public bool? RequireVoteOnLastIteration { get; set; }

    [JsonProperty("blockLastPusherVote")]
    public bool? BlockLastPusherVote { get; set; }

    [JsonProperty("scope")]
    public IEnumerable<AdoBranchPolicyScope> Scope { get; set; }
}

/// <summary>
/// Represents the scope (branches) where a policy applies
/// </summary>
public class AdoBranchPolicyScope
{
    [JsonProperty("refName")]
    public string RefName { get; set; }

    [JsonProperty("matchKind")]
    public string MatchKind { get; set; }

    [JsonProperty("repositoryId")]
    public string RepositoryId { get; set; }
}

/// <summary>
/// Represents the response wrapper for Azure DevOps branch policies
/// </summary>
public class AdoBranchPolicyResponse
{
    [JsonProperty("value")]
    public IReadOnlyList<AdoBranchPolicy> Value { get; set; }

    [JsonProperty("count")]
    public int Count { get; set; }
}
