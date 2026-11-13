param(
    [Parameter(Mandatory)][string] $CandidateDirectory,
    [Parameter(Mandatory)][string] $Version,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{40}$')][string] $Commit,
    [string] $ExpectedSha512
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$packageName = "cslightdbgen.sqlitegen.$Version.nupkg"
$packagePath = Join-Path $CandidateDirectory $packageName
$packages = @(Get-ChildItem -LiteralPath $CandidateDirectory -File -Filter '*.nupkg')
if ($packages.Count -ne 1 -or $packages[0].Name -cne $packageName)
{
    throw "Expected exactly one package: '$packageName'."
}
$hash = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA512).Hash.ToLowerInvariant()
if ($ExpectedSha512 -and $hash -cne $ExpectedSha512)
{
    throw 'Candidate bytes do not match the qualified SHA-512.'
}

$archive = [System.IO.Compression.ZipFile]::OpenRead($packagePath)
try
{
    $names = @($archive.Entries.FullName)
    foreach ($required in @('cslightdbgen.sqlitegen.nuspec', 'analyzers/dotnet/cs/cslightdbgen.sqlitegen.dll', 'README.md', 'LICENSE'))
    {
        if (@($names | Where-Object { $_ -ceq $required }).Count -ne 1)
        {
            throw "Package must contain exactly one '$required'."
        }
    }
    if (@($names | Where-Object { $_ -match '^(lib|ref|runtimes)/' -or ($_ -match '\.dll$' -and $_ -cne 'analyzers/dotnet/cs/cslightdbgen.sqlitegen.dll') }).Count -ne 0)
    {
        throw 'Analyzer package must not ship runtime assets or dependency assemblies.'
    }
    $reader = [System.IO.StreamReader]::new($archive.GetEntry('cslightdbgen.sqlitegen.nuspec').Open())
    try { [xml] $nuspec = $reader.ReadToEnd() }
    finally { $reader.Dispose() }
    $metadata = $nuspec.SelectSingleNode("/*[local-name()='package']/*[local-name()='metadata']")
    if ($metadata.id -cne 'cslightdbgen.sqlitegen' -or $metadata.version -cne $Version)
    {
        throw 'Nuspec package identity does not match the release.'
    }
    if ($metadata.repository.commit -cne $Commit -or
        $metadata.repository.url -ine 'https://github.com/ginocanessa/CSharpLightDbGen')
    {
        throw 'Nuspec repository provenance does not match the release.'
    }
    if ($nuspec.SelectNodes("//*[local-name()='dependency']").Count -ne 0)
    {
        throw 'Analyzer package must have no NuGet dependencies.'
    }
    $assemblyPath = [System.IO.Path]::GetTempFileName()
    try
    {
        [System.IO.Compression.ZipFileExtensions]::ExtractToFile(
            $archive.GetEntry('analyzers/dotnet/cs/cslightdbgen.sqlitegen.dll'), $assemblyPath, $true)
        $assembly = [System.Reflection.AssemblyName]::GetAssemblyName($assemblyPath)
        $fileVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($assemblyPath).FileVersion
        if ($assembly.Name -cne 'cslightdbgen.sqlitegen' -or
            $assembly.Version.ToString() -cne "0.$Version" -or $fileVersion -cne "0.$Version")
        {
            throw 'Analyzer assembly and file versions must match the release version.'
        }
    }
    finally
    {
        Remove-Item -LiteralPath $assemblyPath
    }
}
finally
{
    $archive.Dispose()
}

$hash
