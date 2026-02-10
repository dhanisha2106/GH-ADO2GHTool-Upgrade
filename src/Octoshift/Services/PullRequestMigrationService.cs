using System;
using System.Text;
using Octoshift.Models;

namespace OctoshiftCLI.Services;

/// <summary>
/// Service to transform Azure DevOps Pull Requests to GitHub Pull Requests
/// </summary>
public class PullRequestMigrationService
{
    private readonly OctoLogger _log;

    public PullRequestMigrationService(OctoLogger log)
    {
        _log = log;
    }

    /// <summary>
    /// Generates GitHub PR body with ADO metadata preserved
    /// </summary>
    public string GenerateGitHubPrBody(AdoPullRequest adoPr, string adoOrgUrl)
    {
        if (adoPr is null)
        {
            throw new ArgumentNullException(nameof(adoPr));
        }

        var body = new StringBuilder();

        // Add original PR metadata header
        body.AppendLine("---");
        body.AppendLine($"**Migrated from Azure DevOps**");
        body.AppendLine($"- **Original PR**: [#{adoPr.PullRequestId}]({adoPr.Url})");
        body.AppendLine($"- **Created by**: {adoPr.CreatedBy?.DisplayName ?? "Unknown"} ({adoPr.CreatedBy?.UniqueName ?? "Unknown"})");
        body.AppendLine($"- **Created on**: {adoPr.CreationDate:yyyy-MM-dd HH:mm:ss} UTC");

        if (adoPr.ClosedDate.HasValue)
        {
            body.AppendLine($"- **Closed on**: {adoPr.ClosedDate.Value:yyyy-MM-dd HH:mm:ss} UTC");
        }

        body.AppendLine($"- **Status**: {adoPr.Status}");
        body.AppendLine("---");
        body.AppendLine();

        // Add original description
        if (!string.IsNullOrWhiteSpace(adoPr.Description))
        {
            body.AppendLine("## Original Description");
            body.AppendLine();
            body.AppendLine(adoPr.Description);
        }
        else
        {
            body.AppendLine("*No description provided in original PR*");
        }

        return body.ToString();
    }

    /// <summary>
    /// Formats a comment with original author attribution
    /// </summary>
    public string FormatComment(AdoComment comment)
    {
        if (comment is null)
        {
            throw new ArgumentNullException(nameof(comment));
        }

        var formatted = new StringBuilder();

        formatted.AppendLine($"**Comment by {comment.Author?.DisplayName ?? "Unknown"}** _{comment.PublishedDate:yyyy-MM-dd HH:mm}_");
        formatted.AppendLine();
        formatted.AppendLine(comment.Content ?? "*No content*");

        return formatted.ToString();
    }

    /// <summary>
    /// Extracts branch name from ADO ref
    /// </summary>
    public string GetBranchName(string refName)
    {
        if (string.IsNullOrEmpty(refName))
        {
            return null;
        }

        // Remove refs/heads/ prefix
        return refName.StartsWith("refs/heads/", StringComparison.OrdinalIgnoreCase) ? refName["refs/heads/".Length..] : refName;
    }

    /// <summary>
    /// Determines if PR should be migrated based on status
    /// </summary>
    public bool ShouldMigratePr(AdoPullRequest pr, bool includeAbandoned, bool includeCompleted, bool includeActive)
    {
        return pr is null
            ? throw new ArgumentNullException(nameof(pr))
            : pr.Status.ToLower() switch
        {
            "abandoned" => includeAbandoned,
            "completed" => includeCompleted,
            "active" => includeActive,
            _ => false
        };
    }

    /// <summary>
    /// Determines if PR should be closed after migration
    /// </summary>
    public bool ShouldClosePr(AdoPullRequest pr)
    {
        if (pr is null)
        {
            throw new ArgumentNullException(nameof(pr));
        }

        // Close abandoned and completed PRs
        return pr.Status.Equals("abandoned", StringComparison.OrdinalIgnoreCase) ||
               pr.Status.Equals("completed", StringComparison.OrdinalIgnoreCase);
    }
}
