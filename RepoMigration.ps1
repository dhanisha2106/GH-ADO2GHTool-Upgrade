#!/usr/bin/env pwsh

# =========== Azure DevOps to GitHub Migration Script ===========
# This script automatically discovers and migrates all repositories from an ADO team project
# Customize the configuration variables below to match your environment

# =========== Created with CLI version 1.26.0 ===========

function Exec {
    param (
        [scriptblock]$ScriptBlock
    )
    & @ScriptBlock
    if ($lastexitcode -ne 0) {
        exit $lastexitcode
    }
}

function ExecAndGetMigrationID {
    param (
        [scriptblock]$ScriptBlock
    )
    $MigrationID = & @ScriptBlock | ForEach-Object {
        Write-Host $_
        $_
    } | Select-String -Pattern "\(ID: (.+)\)" | ForEach-Object { $_.matches.groups[1] }
    return $MigrationID
}

function ExecBatch {
    param (
        [scriptblock[]]$ScriptBlocks
    )
    $Global:LastBatchFailures = 0
    foreach ($ScriptBlock in $ScriptBlocks)
    {
        & @ScriptBlock
        if ($lastexitcode -ne 0) {
            $Global:LastBatchFailures++
        }
    }
}

if (-not $env:ADO_PAT) {
    Write-Error "ADO_PAT environment variable must be set to a valid Azure DevOps Personal Access Token with the appropriate scopes. For more information see https://docs.github.com/en/migrations/using-github-enterprise-importer/preparing-to-migrate-with-github-enterprise-importer/managing-access-for-github-enterprise-importer#personal-access-tokens-for-azure-devops"
    exit 1
} else {
    Write-Host "ADO_PAT environment variable is set and will be used to authenticate to Azure DevOps."
}

if (-not $env:GH_PAT) {
    Write-Error "GH_PAT environment variable must be set to a valid GitHub Personal Access Token with the appropriate scopes. For more information see https://docs.github.com/en/migrations/using-github-enterprise-importer/preparing-to-migrate-with-github-enterprise-importer/managing-access-for-github-enterprise-importer#creating-a-personal-access-token-for-github-enterprise-importer"
    exit 1
} else {
    Write-Host "GH_PAT environment variable is set and will be used to authenticate to GitHub."
}

# =========== Migration Configuration ===========
# Modify these variables to match your migration needs
$AdoOrg = "your-ado-org"
$AdoTeamProject = "your-ado-project"
$GithubOrg = "your-gitHub-Org"
$TargetRepoVisibility = "private"  # Options: public, private, internal

# =========== Migration Start ===========
$MigrationStartTime = Get-Date
Write-Host ""
Write-Host "=============== MIGRATION STARTED ===============" -ForegroundColor Cyan
Write-Host "Start Time: $($MigrationStartTime.ToString(""yyyy-MM-dd HH:mm:ss""))" -ForegroundColor Cyan
Write-Host "===============================================" -ForegroundColor Cyan
Write-Host ""

$Succeeded = 0
$Failed = 0
$RepoMigrations = [ordered]@{}

# =========== Discover Repositories ===========
Write-Host "Discovering repositories in $AdoOrg/$AdoTeamProject..." -ForegroundColor Yellow

# Generate inventory report to get all repos
& "c:\GH_A2D_ExtensionTool - Copy\src\ado2gh\bin\Debug\net8.0\ado2gh.exe" inventory-report --ado-org "$AdoOrg" --minimal | Out-Null

# Read repos from the generated CSV
$ReposCsvPath = "repos.csv"
if (-not (Test-Path $ReposCsvPath)) {
    Write-Error "Failed to generate repository inventory. repos.csv not found."
    exit 1
}

$AllRepos = Import-Csv $ReposCsvPath | Where-Object { $_.teamproject -eq $AdoTeamProject }
Write-Host "Found $($AllRepos.Count) repository/repositories in team project '$AdoTeamProject'" -ForegroundColor Green
Write-Host ""

# =========== Queueing Migrations ===========
Write-Host "Queueing migrations for all repositories..." -ForegroundColor Cyan

foreach ($repo in $AllRepos) {
    $repoName = $repo.repo
    Write-Host "[Queue] $repoName"
    
    $MigrationID = ExecAndGetMigrationID { 
        & "c:\GH_A2D_ExtensionTool - Copy\src\ado2gh\bin\Debug\net8.0\ado2gh.exe" migrate-repo `
            --ado-org "$AdoOrg" `
            --ado-team-project "$AdoTeamProject" `
            --ado-repo "$repoName" `
            --github-org "$GithubOrg" `
            --github-repo "$repoName" `
            --queue-only `
            --target-repo-visibility "$TargetRepoVisibility"
    }
    
    $RepoMigrations["$AdoOrg/$AdoTeamProject-$repoName"] = $MigrationID
}

Write-Host ""
Write-Host "All migrations queued. Waiting for completion..." -ForegroundColor Cyan
Write-Host ""

# =========== Wait and Post-Migration Steps ===========

# =========== Wait and Post-Migration Steps ===========

foreach ($repo in $AllRepos) {
    $repoName = $repo.repo
    $migrationKey = "$AdoOrg/$AdoTeamProject-$repoName"
    
    Write-Host "============================================================" -ForegroundColor Cyan
    $RepoStartTime = Get-Date
    Write-Host "[$(Get-Date -Format ""HH:mm:ss"")] Processing repository: $repoName" -ForegroundColor Yellow
    
    $CanExecuteBatch = $false
    if ($null -ne $RepoMigrations[$migrationKey]) {
        & "c:\GH_A2D_ExtensionTool - Copy\src\ado2gh\bin\Debug\net8.0\ado2gh.exe" wait-for-migration --migration-id "$($RepoMigrations[$migrationKey])"
        $CanExecuteBatch = ($lastexitcode -eq 0)
    }
    
    if ($CanExecuteBatch) {
        # Migrate branch policies
        Write-Host "  > Migrating branch policies for $repoName..."
        & "c:\GH_A2D_ExtensionTool - Copy\src\ado2gh\bin\Debug\net8.0\ado2gh.exe" migrate-branch-policies `
            --ado-org "$AdoOrg" `
            --ado-team-project "$AdoTeamProject" `
            --ado-repo "$repoName" `
            --github-org "$GithubOrg" `
            --github-repo "$repoName"
        
        if ($lastexitcode -eq 0) {
            Write-Host "  [OK] Branch policies migrated successfully" -ForegroundColor Green
        } else {
            Write-Warning "  [WARNING] Branch policy migration failed, but continuing..."
        }
        
        # Migrate abandoned pull requests
        Write-Host "  > Migrating abandoned pull requests for $repoName..."
        & "c:\GH_A2D_ExtensionTool - Copy\src\ado2gh\bin\Debug\net8.0\ado2gh.exe" migrate-pull-requests `
            --ado-org "$AdoOrg" `
            --ado-team-project "$AdoTeamProject" `
            --ado-repo "$repoName" `
            --github-org "$GithubOrg" `
            --github-repo "$repoName" `
            --label "abandoned" `
            --label "migrated"
        
        if ($lastexitcode -eq 0) {
            Write-Host "  [OK] Abandoned pull requests migrated successfully" -ForegroundColor Green
        } else {
            Write-Warning "  [WARNING] Pull request migration failed, but continuing..."
        }
        
        $Succeeded++
        $RepoEndTime = Get-Date
        $RepoElapsed = $RepoEndTime - $RepoStartTime
        Write-Host "[$(Get-Date -Format ""HH:mm:ss"")] $repoName completed in $($RepoElapsed.Hours)h $($RepoElapsed.Minutes)m $($RepoElapsed.Seconds)s" -ForegroundColor Green
    } else {
        $Failed++
        Write-Host "  [FAILED] Migration failed for $repoName" -ForegroundColor Red
    }
    Write-Host ""
}

Write-Host =============== Summary ===============
Write-Host Total number of successful migrations: $Succeeded
Write-Host Total number of failed migrations: $Failed

# =========== Migration End ===========
$MigrationEndTime = Get-Date
$ElapsedTime = $MigrationEndTime - $MigrationStartTime

Write-Host ""
Write-Host "=============== MIGRATION COMPLETED ===============" -ForegroundColor Cyan
Write-Host "End Time: $($MigrationEndTime.ToString(""yyyy-MM-dd HH:mm:ss""))" -ForegroundColor Cyan
Write-Host "Total Elapsed Time: $($ElapsedTime.Hours)h $($ElapsedTime.Minutes)m $($ElapsedTime.Seconds)s" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host ""

if ($Failed -ne 0) {
    exit 1
}


