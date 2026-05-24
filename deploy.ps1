param(
    [Parameter(Mandatory=$true)]
    [string]$SourcePath,

    [Parameter(Mandatory=$false)]
    [string]$PublishProfilePath
)

# Resolve to absolute path
$SourcePath = Resolve-Path $SourcePath -ErrorAction Stop

# Validate source path exists
if (-not (Test-Path $SourcePath)) {
    Write-Error "Source path '$SourcePath' does not exist"
    exit 1
}

# Check for msdeploy
$msdeploy = "C:\Program Files\IIS\Microsoft Web Deploy V3\msdeploy.exe"

if (-not (Test-Path $msdeploy)) {
    Write-Error "msdeploy.exe not found at $msdeploy. Please ensure Web Deploy is installed."
    exit 1
}

# Deployment using publish settings file
if ($PublishProfilePath) {
    if (-not (Test-Path $PublishProfilePath)) {
        Write-Error "Publish profile '$PublishProfilePath' does not exist"
        exit 1
    }

    Write-Host "Deploying using publish profile: $PublishProfilePath"
    Write-Host "Source path: $SourcePath"

    $cmd = "`"$msdeploy`" -verb:sync -source:dirPath=`"$SourcePath`" -dest:auto -publishSettings=`"$PublishProfilePath`" -enableRule:DoNotDeleteRule"

    Write-Host "Running deployment..."
    cmd /c $cmd

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Deployment failed with exit code $LASTEXITCODE"
        exit $LASTEXITCODE
    }

    Write-Host "Deployment completed successfully"
    exit 0
}

Write-Error "Either PublishProfilePath must be provided or MONSTERASP_PUBLISH_PROFILE_PATH environment variable must be set"
Write-Host ""
Write-Host "Usage:"
Write-Host "  1. Download the publish profile from MonsterASP.net control panel"
Write-Host "  2. Save it as 'monsterasp.publishSettings' in the repository root"
Write-Host "  3. Or set the MONSTERASP_PUBLISH_PROFILE_PATH environment variable"
exit 1
