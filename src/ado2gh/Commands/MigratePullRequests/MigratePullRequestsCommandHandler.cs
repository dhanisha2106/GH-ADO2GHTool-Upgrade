using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.AdoToGithub.Commands.MigratePullRequests;

public class MigratePullRequestsCommandHandler : ICommandHandler<MigratePullRequestsCommandArgs>
{
    private readonly OctoLogger _log;
    private readonly AdoApi _adoApi;
    private readonly GithubApi _githubApi;
    private readonly PullRequestMigrationService _prService;

    public MigratePullRequestsCommandHandler(
        OctoLogger log,
        AdoApi adoApi,
        GithubApi githubApi,
        PullRequestMigrationService prService)
    {
        _log = log;
        _adoApi = adoApi;
        _githubApi = githubApi;
        _prService = prService;
    }

    public async Task Handle(MigratePullRequestsCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        _log.LogInformation("Migrating Pull Requests from Azure DevOps to GitHub...");
        _log.LogInformation($"Source: {args.AdoOrg}/{args.AdoTeamProject}/{args.AdoRepo}");
        _log.LogInformation($"Target: {args.GithubOrg}/{args.GithubRepo}");
        _log.LogInformation("");

        var adoBaseUrl = args.AdoServerUrl?.TrimEnd('/') ?? "https://dev.azure.com";

        // Get ADO repo ID
        var repoId = await _adoApi.GetRepoId(args.AdoOrg, args.AdoTeamProject, args.AdoRepo);
        _log.LogInformation($"Found Azure DevOps repository (ID: {repoId})");

        // Fetch all PRs
        _log.LogInformation("Fetching pull requests from Azure DevOps...");
        var allPrs = (await _adoApi.GetPullRequests(args.AdoOrg, args.AdoTeamProject, repoId, "all")).ToList();

        // Filter based on user selection
        var prsToMigrate = allPrs.Where(pr => _prService.ShouldMigratePr(pr, args.IncludeAbandoned, args.IncludeCompleted, args.IncludeActive)).ToList();

        if (!prsToMigrate.Any())
        {
            _log.LogWarning("No pull requests match the specified criteria");
            _log.LogInformation($"Total PRs found: {allPrs.Count}");
            _log.LogInformation($"  - Abandoned: {allPrs.Count(pr => pr.Status.Equals("abandoned", StringComparison.OrdinalIgnoreCase))}");
            _log.LogInformation($"  - Completed: {allPrs.Count(pr => pr.Status.Equals("completed", StringComparison.OrdinalIgnoreCase))}");
            _log.LogInformation($"  - Active: {allPrs.Count(pr => pr.Status.Equals("active", StringComparison.OrdinalIgnoreCase))}");
            return;
        }

        _log.LogSuccess($"Found {prsToMigrate.Count} pull request(s) to migrate");
        _log.LogInformation("");

        var successCount = 0;
        var skipCount = 0;
        var errorCount = 0;

        foreach (var pr in prsToMigrate)
        {
            try
            {
                _log.LogInformation($"Processing PR #{pr.PullRequestId}: {pr.Title}");
                _log.LogInformation($"  Status: {pr.Status}");

                var sourceBranch = _prService.GetBranchName(pr.SourceRefName);
                var targetBranch = _prService.GetBranchName(pr.TargetRefName);

                _log.LogVerbose($"  Source: {sourceBranch} → Target: {targetBranch}");

                if (string.IsNullOrEmpty(sourceBranch) || string.IsNullOrEmpty(targetBranch))
                {
                    _log.LogWarning($"  ⚠ Invalid branch names - skipping");
                    skipCount++;
                    _log.LogInformation("");
                    continue;
                }

                // Generate PR body with metadata
                var prBody = _prService.GenerateGitHubPrBody(pr, $"{adoBaseUrl}/{args.AdoOrg}/{args.AdoTeamProject}");

                // Create PR in GitHub
                int githubPrNumber;
                try
                {
                    githubPrNumber = await _githubApi.CreatePullRequest(
                        args.GithubOrg,
                        args.GithubRepo,
                        $"[ADO #{pr.PullRequestId}] {pr.Title}",
                        prBody,
                        sourceBranch,
                        targetBranch
                    );

                    _log.LogSuccess($"  ✓ Created GitHub PR #{githubPrNumber}");

                    // Add labels if provided
                    if (args.Labels != null && args.Labels.Length > 0)
                    {
                        await _githubApi.AddLabelsToIssue(args.GithubOrg, args.GithubRepo, githubPrNumber, args.Labels);
                        _log.LogInformation($"  ✓ Added label(s): {string.Join(", ", args.Labels)}");
                    }
                }
                catch (OctoshiftCliException ex) when (ex.Message.Contains("branches may not exist"))
                {
                    _log.LogWarning($"  ⚠ Branches don't exist in GitHub - skipping");
                    _log.LogVerbose($"     {ex.Message}");
                    skipCount++;
                    _log.LogInformation("");
                    continue;
                }

                // Migrate comments if requested
                if (!args.SkipComments)
                {
                    try
                    {
                        var threads = (await _adoApi.GetPullRequestThreads(args.AdoOrg, args.AdoTeamProject, repoId, pr.PullRequestId)).ToList();
                        var commentCount = 0;

                        foreach (var thread in threads)
                        {
                            foreach (var comment in thread.Comments.Where(c => !string.IsNullOrWhiteSpace(c.Content)))
                            {
                                var formattedComment = _prService.FormatComment(comment);
                                await _githubApi.CreatePullRequestComment(args.GithubOrg, args.GithubRepo, githubPrNumber, formattedComment);
                                commentCount++;
                            }
                        }

                        if (commentCount > 0)
                        {
                            _log.LogInformation($"  ✓ Migrated {commentCount} comment(s)");
                        }
                    }
#pragma warning disable CA1031
                    catch (Exception ex)
#pragma warning restore CA1031
                    {
                        _log.LogWarning($"  ⚠ Failed to migrate comments: {ex.Message}");
                    }
                }

                // Close PR if it was abandoned/completed in ADO
                if (_prService.ShouldClosePr(pr))
                {
                    await _githubApi.ClosePullRequest(args.GithubOrg, args.GithubRepo, githubPrNumber);
                    _log.LogInformation($"  ✓ Closed PR (was {pr.Status} in ADO)");
                }

                successCount++;
                _log.LogInformation("");
            }
#pragma warning disable CA1031
            catch (OctoshiftCliException ex) when (ex.Message.Contains("Permission denied"))
#pragma warning restore CA1031
            {
                _log.LogError($"  ✗ Permission Denied");
                _log.LogError($"     Ensure your GitHub PAT has 'repo' scope and write access");
                _log.LogVerbose($"     {ex.Message}");
                errorCount++;
                _log.LogInformation("");
            }
#pragma warning disable CA1031
            catch (HttpRequestException ex)
#pragma warning restore CA1031
            {
                _log.LogError($"  ✗ Network error: {ex.Message}");
                _log.LogVerbose($"     Status: {ex.StatusCode}");
                errorCount++;
                _log.LogInformation("");
            }
#pragma warning disable CA1031
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _log.LogError($"  ✗ Unexpected error: {ex.Message}");
                _log.LogVerbose($"     {ex.GetType().Name}");
                errorCount++;
                _log.LogInformation("");
            }
        }

        // Summary
        _log.LogInformation("=".PadRight(60, '='));
        _log.LogInformation("Migration Summary:");
        _log.LogSuccess($"  ✓ Successfully migrated: {successCount} PR(s)");

        if (skipCount > 0)
        {
            _log.LogWarning($"  ⚠ Skipped: {skipCount} PR(s)");
        }

        if (errorCount > 0)
        {
            _log.LogError($"  ✗ Failed: {errorCount} PR(s)");
        }

        _log.LogInformation("=".PadRight(60, '='));
        _log.LogInformation("");
        _log.LogInformation("Note: Migrated PRs have new timestamps and PR numbers in GitHub.");
        _log.LogInformation("Original metadata is preserved in the PR description.");
        _log.LogInformation("");

        if (successCount > 0 && errorCount == 0)
        {
            _log.LogSuccess("Pull request migration completed successfully!");
        }
        else if (successCount > 0 && errorCount > 0)
        {
            _log.LogWarning("Pull request migration completed with errors.");
            throw new OctoshiftCliException($"Migration partially completed: {successCount} succeeded, {errorCount} failed");
        }
        else if (errorCount > 0)
        {
            throw new OctoshiftCliException("Pull request migration failed - see errors above");
        }
    }
}
