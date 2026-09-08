[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$projectRoot = Split-Path -Parent $PSScriptRoot
$toolRoot = Join-Path $projectRoot '.tools'
$sdkRoot = Join-Path $toolRoot 'dotnet'
$dotnetPath = Join-Path $sdkRoot 'dotnet.exe'
$sdkVersion = '10.0.400'
$expectedHash = '9b8b88590e4da131bfd0da7aa089d0fc04d5418d5f8607ec13d55dc5a17b4399afd54d496c12657fa05c6c6546dc5eab930f26ac6c50f2d3a7712c0fb378c366'
if (Test-Path -LiteralPath $dotnetPath) {
    $installedVersion = & $dotnetPath --version
    if ($LASTEXITCODE -eq 0 -and $installedVersion -eq $sdkVersion) {
        Write-Output "Portable SDK ready: $installedVersion"
        return
    }
    throw "A different SDK already exists at $sdkRoot. Keep it intact; configure or relocate it manually."
}
New-Item -ItemType Directory -Path $toolRoot -Force | Out-Null
$archive = Join-Path $toolRoot "dotnet-sdk-$sdkVersion-win-x64.zip"
if (!(Test-Path -LiteralPath $archive)) {
    Invoke-WebRequest -Uri "https://builds.dotnet.microsoft.com/dotnet/Sdk/$sdkVersion/dotnet-sdk-$sdkVersion-win-x64.zip" -OutFile $archive
}
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA512).Hash -ne $expectedHash) {
    throw "SDK archive SHA512 mismatch. The archive was not extracted: $archive"
}
Expand-Archive -LiteralPath $archive -DestinationPath $sdkRoot
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
& $dotnetPath --version
if ($LASTEXITCODE -ne 0) { throw 'Portable SDK did not start.' }
