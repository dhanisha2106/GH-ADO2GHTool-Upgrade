using System.Collections.Generic;
using Newtonsoft.Json;

namespace Octoshift.Models;

/// <summary>
/// Represents GitHub branch protection rule configuration
/// </summary>
public class GithubBranchProtection
{
    [JsonProperty("required_status_checks")]
    public RequiredStatusChecks RequiredStatusChecks { get; set; }

    [JsonProperty("enforce_admins")]
    public bool? EnforceAdmins { get; set; }

    [JsonProperty("required_pull_request_reviews")]
    public RequiredPullRequestReviews RequiredPullRequestReviews { get; set; }

    [JsonProperty("restrictions")]
    public object Restrictions { get; set; }

    [JsonProperty("required_linear_history")]
    public bool RequiredLinearHistory { get; set; }

    [JsonProperty("allow_force_pushes")]
    public bool AllowForcePushes { get; set; }

    [JsonProperty("allow_deletions")]
    public bool AllowDeletions { get; set; }

    [JsonProperty("required_conversation_resolution")]
    public bool RequiredConversationResolution { get; set; }
}

/// <summary>
/// Status checks that must pass before merging
/// </summary>
public class RequiredStatusChecks
{
    [JsonProperty("strict")]
    public bool Strict { get; set; }

    [JsonProperty("contexts")]
    public IEnumerable<string> Contexts { get; set; }
}

/// <summary>
/// Pull request review requirements
/// </summary>
public class RequiredPullRequestReviews
{
    [JsonProperty("dismissal_restrictions")]
    public object DismissalRestrictions { get; set; }

    [JsonProperty("dismiss_stale_reviews")]
    public bool DismissStaleReviews { get; set; }

    [JsonProperty("require_code_owner_reviews")]
    public bool RequireCodeOwnerReviews { get; set; }

    [JsonProperty("required_approving_review_count")]
    public int RequiredApprovingReviewCount { get; set; }

    [JsonProperty("require_last_push_approval")]
    public bool RequireLastPushApproval { get; set; }
}
