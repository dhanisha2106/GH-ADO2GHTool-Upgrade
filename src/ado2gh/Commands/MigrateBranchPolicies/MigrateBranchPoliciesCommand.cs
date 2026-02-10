using System;
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using OctoshiftCLI.AdoToGithub.Factories;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.AdoToGithub.Commands.MigrateBranchPolicies
{
    public class MigrateBranchPoliciesCommand : CommandBase<MigrateBranchPoliciesCommandArgs, MigrateBranchPoliciesCommandHandler>
    {
        public MigrateBranchPoliciesCommand() : base(
            name: "migrate-branch-policies",
            description: "Migrates Azure DevOps branch policies to GitHub branch protection rules." +
                         Environment.NewLine +
                         "Transforms ADO branch policies (reviewer requirements, build validation, etc.) to equivalent GitHub branch protection." +
                         Environment.NewLine +
                         "Note: Expects ADO_PAT and GH_PAT environment variables or --ado-pat and --github-pat options to be set.")
        {
            AddOption(AdoOrg);
            AddOption(AdoTeamProject);
            AddOption(AdoRepo);
            AddOption(GithubOrg);
            AddOption(GithubRepo);
            AddOption(AdoPat);
            AddOption(GithubPat);
            AddOption(TargetApiUrl);
            AddOption(Verbose);
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

        public Option<bool> Verbose { get; } = new("--verbose")
        {
            Description = "Display more information to the console."
        };

        public override MigrateBranchPoliciesCommandHandler BuildHandler(MigrateBranchPoliciesCommandArgs args, IServiceProvider sp)
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

            var adoApi = adoApiFactory.Create(args.AdoPat);
            var githubApi = githubApiFactory.Create(apiUrl: args.TargetApiUrl, targetPersonalAccessToken: args.GithubPat);

            var transformationService = new BranchPolicyTransformationService(log);

            return new MigrateBranchPoliciesCommandHandler(log, adoApi, githubApi, transformationService);
        }
    }
}
