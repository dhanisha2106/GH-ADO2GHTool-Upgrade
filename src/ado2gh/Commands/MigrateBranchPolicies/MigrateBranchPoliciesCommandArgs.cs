using OctoshiftCLI.Commands;

namespace OctoshiftCLI.AdoToGithub.Commands.MigrateBranchPolicies
{
    public class MigrateBranchPoliciesCommandArgs : CommandArgs
    {
        public string AdoOrg { get; set; }
        public string AdoTeamProject { get; set; }
        public string AdoRepo { get; set; }
        public string GithubOrg { get; set; }
        public string GithubRepo { get; set; }
        [Secret]
        public string AdoPat { get; set; }
        [Secret]
        public string GithubPat { get; set; }
        public string TargetApiUrl { get; set; }
    }
}
