using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.AdoToGithub.Commands.MigrateBranchPolicies;

public class MigrateBranchPoliciesCommandHandler : ICommandHandler<MigrateBranchPoliciesCommandArgs>
{
    private readonly OctoLogger _log;
    private readonly AdoApi _adoApi;
    private readonly GithubApi _githubApi;
    private readonly BranchPolicyTransformationService _transformationService;

    public MigrateBranchPoliciesCommandHandler(
        OctoLogger log,
        AdoApi adoApi,
        GithubApi githubApi,
        BranchPolicyTransformationService transformationService)
    {
        _log = log;
        _adoApi = adoApi;
        _githubApi = githubApi;
        _transformationService = transformationService;
    }

    public async Task Handle(MigrateBranchPoliciesCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        _log.LogInformation("Migrating Branch Policies from Azure DevOps to GitHub...");
        _log.LogInformation($"Source: {args.AdoOrg}/{args.AdoTeamProject}/{args.AdoRepo}");
        _log.LogInformation($"Target: {args.GithubOrg}/{args.GithubRepo}");
        _log.LogInformation("");

        // Get the ADO repo ID
        var repoId = await _adoApi.GetRepoId(args.AdoOrg, args.AdoTeamProject, args.AdoRepo);
        _log.LogInformation($"Found Azure DevOps repository (ID: {repoId})");

        // Fetch all branch policies from ADO
        _log.LogInformation("Fetching branch policies from Azure DevOps...");
        var adoPolicies = (await _adoApi.GetBranchPolicies(args.AdoOrg, args.AdoTeamProject, repoId)).ToList();

        if (!adoPolicies.Any())
        {
            _log.LogWarning("No branch policies found in Azure DevOps repository");
            _log.LogInformation("Migration completed - nothing to migrate");
            return;
        }

        _log.LogSuccess($"Found {adoPolicies.Count} branch policy configuration(s) in Azure DevOps");

        // Get unique branches that have policies
        var protectedBranches = _transformationService.GetProtectedBranches(adoPolicies).ToList();

        if (!protectedBranches.Any())
        {
            _log.LogWarning("No specific branches with policies found (only wildcard patterns detected)");
            _log.LogInformation("Please configure GitHub repository rulesets manually for pattern-based protection");
            return;
        }

        _log.LogInformation($"Found {protectedBranches.Count} branch(es) with protection policies:");
        foreach (var branch in protectedBranches)
        {
            _log.LogInformation($"  • {branch}");
        }
        _log.LogInformation("");

        // Migrate policies for each branch
        var successCount = 0;
        var skipCount = 0;
        var errorCount = 0;
        var planLimitationError = false;

        foreach (var branchName in protectedBranches)
        {
            try
            {
                _log.LogInformation($"Processing branch: {branchName}");

                // Check if branch exists in GitHub
                try
                {
                    await _githubApi.GetBranch(args.GithubOrg, args.GithubRepo, branchName);
                }
                catch (OctoshiftCliException ex)
                {
                    _log.LogWarning($"  ⚠ Branch '{branchName}' does not exist in GitHub repository - skipping");
                    _log.LogVerbose($"     Error details: {ex.Message}");
                    skipCount++;
                    _log.LogInformation("");
                    continue;
                }

                // Get policies for this specific branch
                var branchPolicies = _transformationService.GetPoliciesForBranch(adoPolicies, branchName).ToList();

                // Transform ADO policies to GitHub protection rules
                var protection = _transformationService.TransformPolicies(branchPolicies, branchName);

                if (protection == null)
                {
                    _log.LogWarning($"  ⚠ No protection rules generated for branch '{branchName}' - skipping");
                    skipCount++;
                    _log.LogInformation("");
                    continue;
                }

                // Check if protection already exists
                try
                {
                    var existingProtection = await _githubApi.GetBranchProtection(args.GithubOrg, args.GithubRepo, branchName);
                    if (existingProtection != null)
                    {
                        _log.LogWarning($"  ⚠ Branch protection already exists for '{branchName}' - will be updated");
                    }
                }
                catch (OctoshiftCliException ex)
                {
                    _log.LogVerbose($"  Unable to check existing protection: {ex.Message}");
                }

                // Apply protection to GitHub - try with full protection first, then fall back to basic if needed
                try
                {
                    await _githubApi.CreateBranchProtection(args.GithubOrg, args.GithubRepo, branchName, protection);
                    _log.LogSuccess($"  ✓ Branch protection applied to '{branchName}'");
                    successCount++;
                }
                catch (OctoshiftCliException ex) when (ex.Message.Contains("Invalid", StringComparison.OrdinalIgnoreCase) || 
                                                         ex.Message.Contains("Validation Failed", StringComparison.OrdinalIgnoreCase))
                {
                    // Retry with basic protection (GitHub Free tier compatible)
                    _log.LogWarning($"  ⚠ Advanced protection features not available - applying basic protection");
                    _log.LogVerbose($"     Error: {ex.Message}");
                    
                    var basicProtection = _transformationService.TransformPolicies(branchPolicies, branchName, useBasicProtection: true);
                    if (basicProtection != null)
                    {
                        await _githubApi.CreateBranchProtection(args.GithubOrg, args.GithubRepo, branchName, basicProtection);
                        _log.LogSuccess($"  ✓ Basic branch protection applied to '{branchName}' (GitHub Free tier)");
                        successCount++;
                    }
                    else
                    {
                        throw; // Re-throw if we can't create basic protection either
                    }
                }

                _log.LogInformation("");
            }
#pragma warning disable CA1031 // We want to continue processing other branches even if one fails
            catch (OctoshiftCliException ex) when (ex.Message.Contains("GitHub Pro", StringComparison.OrdinalIgnoreCase) ||
                                                     ex.Message.Contains("make this repository public", StringComparison.OrdinalIgnoreCase))
#pragma warning restore CA1031
            {
                if (!planLimitationError)
                {
                    _log.LogError($"  ✗ GitHub Plan Limitation Detected");
                    _log.LogError($"     Branch protection requires GitHub Pro/Team/Enterprise for private repositories.");
                    _log.LogError($"     ");
                    _log.LogError($"     Solutions:");
                    _log.LogError($"     1. Make the repository public (Settings → Danger Zone → Change visibility)");
                    _log.LogError($"     2. Upgrade to GitHub Team or Enterprise Cloud");
                    _log.LogError($"     ");
                    _log.LogError($"     See: https://docs.github.com/get-started/learning-about-github/githubs-plans");
                    planLimitationError = true;
                }
                else
                {
                    _log.LogError($"  ✗ Branch '{branchName}' - same plan limitation");
                }
                errorCount++;
                _log.LogInformation("");
            }
#pragma warning disable CA1031 // We want to continue processing other branches even if one fails
            catch (OctoshiftCliException ex) when (ex.Message.Contains("Permission denied", StringComparison.OrdinalIgnoreCase))
#pragma warning restore CA1031
            {
                _log.LogError($"  ✗ Permission Denied");
                _log.LogError($"     Your GitHub PAT lacks permission to set branch protection.");
                _log.LogError($"     Ensure your PAT has:");
                _log.LogError($"     - 'repo' scope (full control of private repositories)");
                _log.LogError($"     - Admin access to the {args.GithubOrg}/{args.GithubRepo} repository");
                _log.LogVerbose($"     Error details: {ex.Message}");
                errorCount++;
                _log.LogInformation("");
            }
#pragma warning disable CA1031 // We want to continue processing other branches even if one fails
            catch (OctoshiftCliException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
#pragma warning restore CA1031
            {
                _log.LogError($"  ✗ Branch or repository not found");
                _log.LogError($"     {ex.Message}");
                _log.LogVerbose($"     Error details: {ex.Message}");
                errorCount++;
                _log.LogInformation("");
            }
#pragma warning disable CA1031 // We want to continue processing other branches even if one fails
            catch (OctoshiftCliException ex) when (ex.Message.Contains("Invalid", StringComparison.OrdinalIgnoreCase))
#pragma warning restore CA1031
            {
                _log.LogError($"  ✗ Invalid branch protection configuration");
                _log.LogError($"     {ex.Message}");
                _log.LogVerbose($"     This may indicate an unsupported combination of protection settings.");
                errorCount++;
                _log.LogInformation("");
            }
#pragma warning disable CA1031 // We want to continue processing other branches even if one fails
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
#pragma warning restore CA1031
            {
                _log.LogError($"  ✗ Rate limit exceeded");
                _log.LogError($"     GitHub API rate limit reached. Please wait and try again later.");
                _log.LogVerbose($"     Error details: {ex.Message}");
                errorCount++;
                _log.LogInformation("");
            }
#pragma warning disable CA1031 // We want to continue processing other branches even if one fails
            catch (HttpRequestException ex)
#pragma warning restore CA1031
            {
                _log.LogError($"  ✗ Network or HTTP error");
                _log.LogError($"     Failed to communicate with GitHub API: {ex.Message}");
                _log.LogVerbose($"     Status code: {ex.StatusCode}");
                _log.LogVerbose($"     Full error: {ex}");
                errorCount++;
                _log.LogInformation("");
            }
#pragma warning disable CA1031 // We want to continue processing other branches even if one fails
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _log.LogError($"  ✗ Unexpected error migrating branch '{branchName}'");
                _log.LogError($"     {ex.Message}");
                _log.LogVerbose($"     Exception type: {ex.GetType().Name}");
                _log.LogVerbose($"     Stack trace: {ex.StackTrace}");
                errorCount++;
                _log.LogInformation("");
            }
        }

        // Summary
        _log.LogInformation("=".PadRight(60, '='));
        _log.LogInformation("Migration Summary:");
        _log.LogSuccess($"  ✓ Successfully migrated: {successCount} branch(es)");

        if (skipCount > 0)
        {
            _log.LogWarning($"  ⚠ Skipped: {skipCount} branch(es)");
        }

        if (errorCount > 0)
        {
            _log.LogError($"  ✗ Failed: {errorCount} branch(es)");
        }

        _log.LogInformation("=".PadRight(60, '='));
        _log.LogInformation("");
        _log.LogInformation("Note: Some Azure DevOps policies cannot be directly mapped to GitHub:");
        _log.LogInformation("  • Work item linking - No GitHub equivalent");
        _log.LogInformation("  • Merge strategies - Configure in GitHub repository settings");
        _log.LogInformation("  • Wildcard branch patterns - Use GitHub repository rulesets");
        _log.LogInformation("");

        if (successCount > 0 && errorCount == 0)
        {
            _log.LogSuccess("Branch policy migration completed successfully!");
        }
        else if (successCount > 0 && errorCount > 0)
        {
            _log.LogWarning("Branch policy migration completed with errors. Some branches were migrated successfully.");
            throw new OctoshiftCliException($"Migration partially completed: {successCount} succeeded, {errorCount} failed");
        }
        else if (errorCount > 0)
        {
            if (planLimitationError)
            {
                throw new OctoshiftCliException("Branch policy migration failed due to GitHub plan limitations. See solutions above.");
            }
            throw new OctoshiftCliException("Branch policy migration failed - see errors above");
        }
        else if (skipCount > 0)
        {
            _log.LogWarning("No policies were migrated. All branches were skipped (see reasons above).");
        }
    }
}
