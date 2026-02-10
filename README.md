#  ADO2GHTool-Upgrade

 


The [GitHub Enterprise Importer](https://docs.github.com/en/migrations/using-github-enterprise-importer) (GEI, formerly Octoshift) is a highly customizable API-first migration offering designed to help you move your enterprise to GitHub Enterprise Cloud. The GEI-CLI wraps the GEI APIs as a cross-platform console application to simplify customizing your migration experience. Also added the Abandom PR and Branch policy migration 

> GEI is generally available for repository migrations originating from Azure DevOps or GitHub that target GitHub Enterprise Cloud. It is in public beta for repository migrations from BitBucket Server and Data Center to GitHub Enterprise Cloud.
>
>
>    Discover repos
      ->
   Queue migration (server-side)
      ->
   Wait for completion
      ->
  Apply branch policies (client-side)
      ->
  Recreate abandoned PRs (client-side)


## Using the GEI CLI
 
- `gh ado2gh` - Run migrations from Azure DevOps to GitHub 

To use `gh ado2gh` first install the latest [GitHub CLI](https://github.com/cli/cli#installation), then run the command
>`gh extension install github/gh-ado2gh` 

To see the available commands and options run: 

>`gh ado2gh --help` 
 

### Azure DevOps to GitHub Usage
1. Create Personal Access Tokens with access to the Azure DevOps org, and the GitHub org (for more details on scopes needed refer to our [official documentation](https://docs.github.com/en/migrations/using-github-enterprise-importer/preparing-to-migrate-with-github-enterprise-importer/managing-access-for-github-enterprise-importer)).

2. Set the `ADO_PAT` and `GH_PAT` environment variables.
3. Set the values for  variables in `RepoMigration.ps1`
   $AdoOrg = ""
  $AdoTeamProject = ""
  $GithubOrg = ""
  $TargetRepoVisibility = ""  

5. Run the `RepoMigration.ps1`      

7. The `RepoMigration.ps1` script requires PowerShell to run. If not already installed see the [install instructions](https://docs.microsoft.com/en-us/powershell/scripting/install/installing-powershell?view=powershell-7.2) to install PowerShell on Windows, Linux, or Mac. Then run the script.

 
