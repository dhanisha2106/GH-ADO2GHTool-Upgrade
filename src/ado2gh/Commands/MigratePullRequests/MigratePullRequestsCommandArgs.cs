using OctoshiftCLI.Commands;

namespace OctoshiftCLI.AdoToGithub.Commands.MigratePullRequests
{
    public class MigratePullRequestsCommandArgs : CommandArgs
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
        public bool IncludeAbandoned { get; set; }
        public bool IncludeCompleted { get; set; }
        public bool IncludeActive { get; set; }
        public bool SkipComments { get; set; }
        public string[] Labels { get; set; }
        public string TargetApiUrl { get; set; }
        public string AdoServerUrl { get; set; }
    }
}
