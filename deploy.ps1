param(
    [Parameter(Mandatory=$true)]
    [string]$SourcePath,

    [Parameter(Mandatory=$true)]
    [string]$PublishProfilePath
)

# Resolve to absolute path
$SourcePath = Resolve-Path $SourcePath -ErrorAction Stop

# Validate source path exists
if (-not (Test-Path $SourcePath)) {
    Write-Error "Source path '$SourcePath' does not exist"
    exit 1
}

# Validate publish profile exists
if (-not (Test-Path $PublishProfilePath)) {
    Write-Error "Publish profile '$PublishProfilePath' does not exist"
    exit 1
}

# Parse publish settings XML
[xml]$publishSettings = Get-Content $PublishProfilePath
$profile = $publishSettings.publishData.publishProfile

$publishUrl = $profile.publishUrl
$msdeploySite = $profile.msdeploySite
$userName = $profile.userName
$userPWD = $profile.userPWD

Write-Host "Deploying to: https://$publishUrl/msdeploy.axd?site=$msdeploySite"
Write-Host "Source path: $SourcePath"
Write-Host "User: $userName"

# Check for msdeploy
$msdeploy = "C:\Program Files\IIS\Microsoft Web Deploy V3\msdeploy.exe"

if (-not (Test-Path $msdeploy)) {
    Write-Error "msdeploy.exe not found at $msdeploy. Please ensure Web Deploy is installed."
    exit 1
}

# Build and execute deployment command
$destUrl = "https://${publishUrl}/msdeploy.axd?site=${msdeploySite}"
$cmd = "`"$msdeploy`" -verb:sync -source:dirPath=`"$SourcePath`" -dest:auto,computerName=`"$destUrl`",userName=`"$userName`",password=`"$userPWD`",authType=`"Basic`" -enableRule:DoNotDeleteRule"

Write-Host ""
Write-Host "Running deployment..."
cmd /c $cmd

if ($LASTEXITCODE -ne 0) {
    Write-Error "Deployment failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "✓ Deployment completed successfully"
exit 0
