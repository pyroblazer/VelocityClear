param(
    [Parameter(Mandatory=$true)]
    [string]$ServerName,

    [Parameter(Mandatory=$true)]
    [string]$SiteName,

    [Parameter(Mandatory=$true)]
    [string]$Username,

    [Parameter(Mandatory=$true)]
    [string]$Password,

    [Parameter(Mandatory=$true)]
    [string]$SourcePath,

    [int]$Port = 8172
)

# Resolve to absolute path
$SourcePath = Resolve-Path $SourcePath -ErrorAction Stop

# Validate source path exists
if (-not (Test-Path $SourcePath)) {
    Write-Error "Source path '$SourcePath' does not exist"
    exit 1
}

# Build destination URL
$destUrl = "https://${ServerName}:${Port}/msdeploy.axd?site=${SiteName}"

Write-Host "Deploying to: $destUrl"
Write-Host "Source path: $SourcePath"
Write-Host "Site: $SiteName"

# Run msdeploy
$msdeploy = "C:\Program Files\IIS\Microsoft Web Deploy V3\msdeploy.exe"

if (-not (Test-Path $msdeploy)) {
    Write-Error "msdeploy.exe not found at $msdeploy. Please ensure Web Deploy is installed."
    exit 1
}

# Build command string
$cmd = "`"$msdeploy`" -verb:sync -source:dirPath=`"$SourcePath`" -dest:auto,computerName=`"$destUrl`",userName=`"$Username`",password=`"$Password`",authType=`"Basic`" -enableRule:DoNotDeleteRule"

Write-Host "Running deployment..."
Write-Host $cmd
cmd /c $cmd

if ($LASTEXITCODE -ne 0) {
    Write-Error "Deployment failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

Write-Host "Deployment completed successfully"
exit 0
