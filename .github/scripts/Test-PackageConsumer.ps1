param(
    [Parameter(Mandatory)][string] $CandidateDirectory,
    [Parameter(Mandatory)][string] $Version,
    [Parameter(Mandatory)][ValidateSet('net11.0', 'net10.0')][string] $Framework,
    [Parameter(Mandatory)][string] $WorkingDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$CandidateDirectory = (Resolve-Path -LiteralPath $CandidateDirectory).Path
$WorkingDirectory = [System.IO.Path]::GetFullPath($WorkingDirectory)
if (Test-Path -LiteralPath $WorkingDirectory)
{
    throw "Consumer working directory '$WorkingDirectory' must not already exist."
}
$projects = @(
    'cslightdbgen.sqlitegen.integration',
    'cslightdbgen.sqlitegen.integration.host-a',
    'cslightdbgen.sqlitegen.integration.host-b'
)
$sourceRoot = (git rev-parse --show-toplevel).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve the source repository.' }
foreach ($project in $projects)
{
    $files = git ls-files -- "tests/$project"
    if ($LASTEXITCODE -ne 0 -or !$files) { throw "Cannot enumerate '$project' consumer files." }
    foreach ($file in $files)
    {
        $destination = Join-Path $WorkingDirectory $file
        New-Item -ItemType Directory -Force -Path (Split-Path $destination) | Out-Null
        Copy-Item -LiteralPath (Join-Path $sourceRoot $file) -Destination $destination
    }
    $projectPath = Join-Path (Join-Path (Join-Path $WorkingDirectory 'tests') $project) "$project.csproj"
    [xml] $projectXml = Get-Content -LiteralPath $projectPath -Raw
    $reference = $projectXml.SelectSingleNode("//ProjectReference[contains(@Include, 'cslightdbgen.sqlitegen.csproj')]")
    if (!$reference) { throw "No analyzer project reference found in '$project'." }
    $packageReference = $projectXml.CreateElement('PackageReference')
    $packageReference.SetAttribute('Include', 'cslightdbgen.sqlitegen')
    $packageReference.SetAttribute('Version', $Version)
    $packageReference.SetAttribute('PrivateAssets', 'all')
    $reference.ParentNode.ReplaceChild($packageReference, $reference) | Out-Null
    $projectXml.Save($projectPath)
}

# Pin the compiler host to .NET 11 for Roslyn 5.9, including .NET 10 consumers.
@{ sdk = @{ version = '11.0.100'; rollForward = 'latestFeature' } } |
    ConvertTo-Json | Set-Content (Join-Path $WorkingDirectory 'global.json')
[xml] $configuration = @'
<configuration>
  <packageSources>
    <clear />
    <add key="candidate" value="" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <clear />
    <packageSource key="candidate"><package pattern="cslightdbgen.sqlitegen" /></packageSource>
    <packageSource key="nuget.org"><package pattern="*" /></packageSource>
  </packageSourceMapping>
</configuration>
'@
$configuration.configuration.packageSources.add[0].SetAttribute('value', $CandidateDirectory)
$configurationPath = Join-Path $WorkingDirectory 'NuGet.Config'
$configuration.Save($configurationPath)
$packagesDirectory = Join-Path $WorkingDirectory 'packages'
$testProject = Join-Path (Join-Path (Join-Path $WorkingDirectory 'tests') $projects[0]) "$($projects[0]).csproj"

Push-Location $WorkingDirectory
try
{
    dotnet restore $testProject "--configfile=$configurationPath" "--packages=$packagesDirectory" "-p:TargetFramework=$Framework"
    if ($LASTEXITCODE -ne 0) { throw 'Package consumer restore failed.' }
    $restoredPackage = Join-Path (Join-Path $packagesDirectory 'cslightdbgen.sqlitegen') $Version
    $archiveName = "cslightdbgen.sqlitegen.$Version.nupkg"
    $candidateHash = (Get-FileHash (Join-Path $CandidateDirectory $archiveName) -Algorithm SHA512).Hash
    $restoredHash = (Get-FileHash (Join-Path $restoredPackage $archiveName) -Algorithm SHA512).Hash
    if ($candidateHash -cne $restoredHash)
    {
        throw 'Consumer did not restore the exact candidate package.'
    }
    dotnet test $testProject --configuration Release --framework $Framework --no-restore `
        --logger trx --results-directory (Join-Path $WorkingDirectory 'results')
    if ($LASTEXITCODE -ne 0) { throw 'Package consumer integration tests failed.' }
    $reports = @(Get-ChildItem -LiteralPath (Join-Path $WorkingDirectory 'results') -Filter '*.trx' -File)
    if ($reports.Count -ne 1) { throw 'Expected exactly one consumer test result report.' }
    [xml] $report = Get-Content -LiteralPath $reports[0].FullName -Raw
    $counters = $report.TestRun.ResultSummary.Counters
    if ([int] $counters.total -le 0 -or [int] $counters.passed -ne [int] $counters.total)
    {
        throw 'Consumer qualification requires discovered tests and every test passing.'
    }
}
finally
{
    Pop-Location
}
