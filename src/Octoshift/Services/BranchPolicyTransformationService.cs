using System;
using System.Collections.Generic;
using System.Linq;
using Octoshift.Models;

namespace OctoshiftCLI.Services;

/// <summary>
/// Service to transform Azure DevOps branch policies to GitHub branch protection rules
/// </summary>
public class BranchPolicyTransformationService
{
    private readonly OctoLogger _log;

    // ADO Policy Type IDs (these are standard GUIDs used by Azure DevOps)
    private const string MINIMUM_REVIEWER_POLICY_TYPE = "fa4e907d-c16b-4a4c-9dfa-4906e5d171dd";
    private const string BUILD_POLICY_TYPE = "0609b952-1397-4640-95ec-e00a01b2c241";
    private const string COMMENT_REQUIREMENTS_POLICY_TYPE = "c6a1889d-b943-4856-b76f-9e46bb6b0df2";
    private const string WORK_ITEM_LINKING_POLICY_TYPE = "40e92b44-2fe1-4dd6-b3d8-74a9c21d0c6e";
    private const string MERGE_STRATEGY_POLICY_TYPE = "fa4e907d-c16b-4a4c-9dfa-4916e5d171ab";

    public BranchPolicyTransformationService(OctoLogger log)
    {
        _log = log;
    }

    /// <summary>
    /// Transforms ADO branch policies to GitHub branch protection rules
    /// </summary>
    public GithubBranchProtection TransformPolicies(IEnumerable<AdoBranchPolicy> adoPolicies, string branchName, bool useBasicProtection = false)
    {
        if (adoPolicies == null || !adoPolicies.Any())
        {
            _log.LogWarning($"No branch policies found for branch '{branchName}' in Azure DevOps");
            return null;
        }

        var enabledPolicies = adoPolicies.Where(p => p.IsEnabled).ToList();

        if (!enabledPolicies.Any())
        {
            _log.LogWarning($"No enabled branch policies found for branch '{branchName}' in Azure DevOps");
            return null;
        }

        var protection = new GithubBranchProtection();
        
        // Only set these fields if not using basic protection (to avoid compatibility issues)
        if (!useBasicProtection)
        {
            protection.RequiredLinearHistory = false;
            protection.AllowForcePushes = false;
            protection.AllowDeletions = false;
        }

        var statusChecks = new List<string>();
        var hasReviewerPolicy = false;
        var minReviewers = 0;
        var dismissStaleReviews = false;
        var requireLastPushApproval = false;
        var requireConversationResolution = false;

        foreach (var policy in enabledPolicies)
        {
            switch (policy.Type.Id)
            {
                case MINIMUM_REVIEWER_POLICY_TYPE:
                    hasReviewerPolicy = true;
                    minReviewers = Math.Max(minReviewers, policy.Settings?.MinimumApproverCount ?? 1);
                    
                    // Only use advanced features if not using basic protection
                    if (!useBasicProtection)
                    {
                        dismissStaleReviews = dismissStaleReviews || (policy.Settings?.ResetOnSourcePush ?? false);
                        requireLastPushApproval = requireLastPushApproval || (policy.Settings?.BlockLastPusherVote ?? false);
                    }

                    _log.LogInformation($"  ✓ Minimum reviewer policy: {minReviewers} approver(s) required");
                    if (dismissStaleReviews && !useBasicProtection)
                    {
                        _log.LogInformation("  ✓ Dismiss stale reviews on new push");
                    }
                    if (requireLastPushApproval && !useBasicProtection)
                    {
                        _log.LogInformation("  ✓ Require approval from someone other than the last pusher");
                    }
                    break;

                case BUILD_POLICY_TYPE:
                    var buildName = policy.Settings?.DisplayName ?? $"Build-{policy.Id}";
                    statusChecks.Add(buildName);
                    _log.LogInformation($"  ✓ Build validation policy: '{buildName}' must pass");
                    break;

                case COMMENT_REQUIREMENTS_POLICY_TYPE:
                    requireConversationResolution = true;
                    if (!useBasicProtection)
                    {
                        _log.LogInformation("  ✓ Comment resolution required");
                    }
                    else
                    {
                        _log.LogInformation("  ℹ Comment resolution (not available in basic protection)");
                    }
                    break;

                case WORK_ITEM_LINKING_POLICY_TYPE:
                    _log.LogWarning($"  ⚠ Work item linking policy cannot be migrated (no GitHub equivalent)");
                    break;

                case MERGE_STRATEGY_POLICY_TYPE:
                    _log.LogWarning($"  ⚠ Merge strategy policy cannot be fully migrated (configure in repository settings)");
                    break;

                default:
                    _log.LogWarning($"  ⚠ Unknown policy type '{policy.Type.DisplayName}' (ID: {policy.Type.Id})");
                    break;
            }
        }

        // Set required status checks
        if (statusChecks.Any())
        {
            protection.RequiredStatusChecks = new RequiredStatusChecks
            {
                Strict = true,  // Require branches to be up to date before merging
                Contexts = statusChecks
            };
        }

        // Set required pull request reviews
        if (hasReviewerPolicy)
        {
            var reviewConfig = new RequiredPullRequestReviews
            {
                RequireCodeOwnerReviews = false,  // ADO doesn't have code owners concept
                RequiredApprovingReviewCount = useBasicProtection ? 1 : Math.Max(1, Math.Min(6, minReviewers)) // Basic mode: always 1, Full mode: respect ADO setting
            };

            // Only add advanced features if not using basic protection (these require Pro/Team/Enterprise)
            if (!useBasicProtection)
            {
                reviewConfig.DismissStaleReviews = dismissStaleReviews;
                reviewConfig.RequireLastPushApproval = requireLastPushApproval;
            }

            protection.RequiredPullRequestReviews = reviewConfig;
        }

        // Only require conversation resolution if not using basic protection
        if (!useBasicProtection)
        {
            protection.RequiredConversationResolution = requireConversationResolution;
        }
        
        // For basic protection, if we have no status checks and no reviewers, return null
        if (useBasicProtection && !hasReviewerPolicy && !statusChecks.Any())
        {
            _log.LogWarning("  ⚠ No basic protection rules to apply (GitHub Free tier has limited features)");
            return null;
        }

        return protection;
    }

    /// <summary>
    /// Gets the list of unique branch names that have policies defined
    /// </summary>
    public IEnumerable<string> GetProtectedBranches(IEnumerable<AdoBranchPolicy> adoPolicies)
    {
        var branches = new HashSet<string>();

        foreach (var policy in adoPolicies.Where(p => p.IsEnabled && p.Settings?.Scope != null))
        {
            foreach (var scope in policy.Settings.Scope)
            {
                if (!string.IsNullOrEmpty(scope.RefName))
                {
                    var branchName = scope.RefName;

                    // Remove refs/heads/ prefix if present
                    if (branchName.StartsWith("refs/heads/", StringComparison.OrdinalIgnoreCase))
                    {
                        branchName = branchName["refs/heads/".Length..];
                    }

                    // Skip wildcard patterns for now (GitHub handles these differently)
                    if (!branchName.Contains("*"))
                    {
                        branches.Add(branchName);
                    }
                    else
                    {
                        _log.LogWarning($"  ⚠ Wildcard branch pattern '{branchName}' will be skipped (configure rulesets in GitHub for pattern matching)");
                    }
                }
            }
        }

        return branches;
    }

    /// <summary>
    /// Gets policies that apply to a specific branch
    /// </summary>
    public IEnumerable<AdoBranchPolicy> GetPoliciesForBranch(IEnumerable<AdoBranchPolicy> adoPolicies, string branchName)
    {
        var refName = $"refs/heads/{branchName}";

        return adoPolicies.Where(p =>
            p.IsEnabled &&
            p.Settings?.Scope != null &&
            p.Settings.Scope.Any(s =>
                s.RefName != null &&
                (s.RefName.Equals(refName, StringComparison.OrdinalIgnoreCase) ||
                 s.RefName.Equals(branchName, StringComparison.OrdinalIgnoreCase))
            )
        );
    }
}
