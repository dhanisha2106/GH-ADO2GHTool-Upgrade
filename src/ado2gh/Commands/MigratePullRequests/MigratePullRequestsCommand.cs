using System;
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using OctoshiftCLI.AdoToGithub.Factories;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.AdoToGithub.Commands.MigratePullRequests
{
    public class MigratePullRequestsCommand : CommandBase<MigratePullRequestsCommandArgs, MigratePullRequestsCommandHandler>
    {
        public MigratePullRequestsCommand() : base(
            name: "migrate-pull-requests",
            description: "Migrates abandoned Azure DevOps Pull Requests to GitHub." +
                         Environment.NewLine +
                         "GitHub's migration API migrates active and completed PRs but excludes abandoned PRs." +
                         Environment.NewLine +
                         "This command fills that gap by migrating abandoned PRs after the main repo migration completes." +
                         Environment.NewLine +
                         "Note: PRs will have new timestamps and numbers. Expects ADO_PAT and GH_PAT environment variables or --ado-pat and --github-pat options to be set.")
        {
            AddOption(AdoOrg);
            AddOption(AdoTeamProject);
            AddOption(AdoRepo);
            AddOption(GithubOrg);
            AddOption(GithubRepo);
            AddOption(IncludeAbandoned);
            AddOption(IncludeCompleted);
            AddOption(IncludeActive);
            AddOption(SkipComments);
            AddOption(Labels);
            AddOption(AdoPat);
            AddOption(GithubPat);
            AddOption(TargetApiUrl);
            AddOption(AdoServerUrl);
            AddOption(Verbose);
            
            IncludeAbandoned.SetDefaultValue(true);
        }

        public Option<string> AdoOrg { get; } = new("--ado-org")
        {
            IsRequired = true,
            Description = "The Azure DevOps organization name"
        };

        public Option<string> AdoTeamProject { get; } = new("--ado-team-project")
        {
            IsRequired = true,
            Description = "The Azure DevOps team project name"
        };

        public Option<string> AdoRepo { get; } = new("--ado-repo")
        {
            IsRequired = true,
            Description = "The Azure DevOps repository name"
        };

        public Option<string> GithubOrg { get; } = new("--github-org")
        {
            IsRequired = true,
            Description = "The GitHub organization name"
        };

        public Option<string> GithubRepo { get; } = new("--github-repo")
        {
            IsRequired = true,
            Description = "The GitHub repository name"
        };

        public Option<bool> IncludeAbandoned { get; } = new("--include-abandoned")
        {
            Description = "Include abandoned (closed without merging) pull requests. Default: true (GitHub's migration API excludes these)"
        };

        public Option<bool> IncludeCompleted { get; } = new("--include-completed")
        {
            Description = "Include completed (merged) pull requests. Default: false (GitHub's migration API already migrates these)"
        };

        public Option<bool> IncludeActive { get; } = new("--include-active")
        {
            Description = "Include active (open) pull requests. Default: false (GitHub's migration API already migrates these)"
        };

        public Option<bool> SkipComments { get; } = new("--skip-comments")
        {
            Description = "Skip migrating PR comments and discussion threads. Default: false"
        };

        public Option<string[]> Labels { get; } = new("--label")
        {
            Description = "Label(s) to add to migrated pull requests. Can be specified multiple times (e.g., --label abandoned --label migrated)"
        };

        public Option<string> AdoPat { get; } = new("--ado-pat")
        {
            Description = "Personal access token for Azure DevOps. Overrides ADO_PAT environment variable."
        };

        public Option<string> GithubPat { get; } = new("--github-pat")
        {
            Description = "Personal access token for GitHub. Overrides GH_PAT environment variable."
        };

        public Option<string> TargetApiUrl { get; } = new("--target-api-url")
        {
            Description = "The URL of the target API, if not migrating to github.com. Defaults to https://api.github.com"
        };

        public Option<string> AdoServerUrl { get; } = new("--ado-server-url")
        {
            Description = "The URL of the Azure DevOps Server, if not using Azure DevOps Services. Defaults to https://dev.azure.com"
        };

        public Option<bool> Verbose { get; } = new("--verbose")
        {
            Description = "Display more information to the console."
        };

        public override MigratePullRequestsCommandHandler BuildHandler(MigratePullRequestsCommandArgs args, IServiceProvider sp)
        {
            if (args is null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            if (sp is null)
            {
                throw new ArgumentNullException(nameof(sp));
            }

            var log = sp.GetRequiredService<OctoLogger>();
            var adoApiFactory = sp.GetRequiredService<AdoApiFactory>();
            var githubApiFactory = sp.GetRequiredService<ITargetGithubApiFactory>();
            var environmentVariableProvider = sp.GetRequiredService<EnvironmentVariableProvider>();

            args.AdoPat ??= environmentVariableProvider.AdoPersonalAccessToken();
            args.GithubPat ??= environmentVariableProvider.TargetGithubPersonalAccessToken();

            var adoApi = adoApiFactory.Create(args.AdoServerUrl, args.AdoPat);
            var githubApi = githubApiFactory.Create(apiUrl: args.TargetApiUrl, targetPersonalAccessToken: args.GithubPat);

            var prService = new PullRequestMigrationService(log);

            return new MigratePullRequestsCommandHandler(log, adoApi, githubApi, prService);
        }
    }
}
