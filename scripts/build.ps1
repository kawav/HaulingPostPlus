[CmdletBinding()]
param(
    [string]$GamePath,
    [string]$HarmonyPath,
    [switch]$Install,
    [string]$ModsPath = (Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'Timberborn\Mods')
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'

# Read Steam's known library locations; explicit parameters always take precedence.
$libraryRoots = [Collections.Generic.List[string]]::new()
$steamRegistry = Get-ItemProperty 'HKCU:\Software\Valve\Steam' -ErrorAction SilentlyContinue
if ($steamRegistry -and $steamRegistry.SteamPath) {
    $libraryRoots.Add($steamRegistry.SteamPath)
    $librariesFile = Join-Path $steamRegistry.SteamPath 'steamapps\libraryfolders.vdf'
    if (Test-Path -LiteralPath $librariesFile) {
        foreach ($match in [regex]::Matches((Get-Content -LiteralPath $librariesFile -Raw), '"path"\s+"([^"]+)"')) {
            $libraryRoots.Add($match.Groups[1].Value.Replace('\\', '\'))
        }
    }
}
if (!$GamePath) {
    foreach ($library in $libraryRoots) {
        $candidate = Join-Path $library 'steamapps\common\Timberborn'
        if (Test-Path -LiteralPath (Join-Path $candidate 'Timberborn_Data\Managed\Timberborn.WorkSystem.dll')) {
            $GamePath = $candidate
            break
        }
    }
}
if (!$GamePath) { throw 'Timberborn was not found. Supply -GamePath.' }
$GamePath = (Resolve-Path -LiteralPath $GamePath).Path
$managed = Join-Path $GamePath 'Timberborn_Data\Managed'
if (!$HarmonyPath) {
    $gameSteamApps = Split-Path -Parent (Split-Path -Parent $GamePath)
    $harmonyCandidates = @((Join-Path $gameSteamApps 'workshop\content\1062090\3284904751\0Harmony.dll'))
    foreach ($library in $libraryRoots) {
        $harmonyCandidates += Join-Path $library 'steamapps\workshop\content\1062090\3284904751\0Harmony.dll'
    }
    $HarmonyPath = $harmonyCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (!$HarmonyPath) { throw 'Harmony was not found. Subscribe to Harmony or supply -HarmonyPath to 0Harmony.dll.' }
$HarmonyPath = (Resolve-Path -LiteralPath $HarmonyPath).Path
$harmonyVersion = [Reflection.AssemblyName]::GetAssemblyName($HarmonyPath).Version
if ($harmonyVersion -lt [version]'2.4.1.0') { throw "Harmony 2.4.1 or newer required; found $harmonyVersion." }

$dotnet = Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
if (!(Test-Path -LiteralPath $dotnet)) {
    $dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
    if (!$dotnetCommand) { throw 'Run scripts/setup-sdk.ps1 first (requires .NET SDK 10 for offline tests).' }
    $dotnet = $dotnetCommand.Source
    if (!((& $dotnet --list-sdks) -match '^10\.')) { throw 'Run scripts/setup-sdk.ps1 to install the project-local SDK.' }
}

$manifest = Get-Content -LiteralPath (Join-Path $projectRoot 'mod\manifest.json') -Raw | ConvertFrom-Json
$buildId = "$($manifest.Version)-$(Get-Date -Format 'yyyyMMdd-HHmmss-fff')"
$artifacts = Join-Path $projectRoot "artifacts\$buildId"
New-Item -ItemType Directory -Path $artifacts | Out-Null
Push-Location $projectRoot
try {
    & $dotnet build 'src\HaulingPostPlus\HaulingPostPlus.csproj' -c Release --nologo "-p:GameManagedPath=$managed" "-p:HarmonyPath=$HarmonyPath" 2>&1 |
        Tee-Object -FilePath (Join-Path $artifacts 'build.log') | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Mod compilation failed.' }
    & $dotnet run --project 'tests\HaulingPostPlus.Tests\HaulingPostPlus.Tests.csproj' -c Release -- $projectRoot $GamePath 2>&1 |
        Tee-Object -FilePath (Join-Path $artifacts 'tests.log') | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Automated checks failed. No package was installed.' }
    foreach ($includeSecondShift in @('false', 'true')) {
        & $dotnet run --project 'tests\HaulingPostPlus.Patching.Tests\HaulingPostPlus.Patching.Tests.csproj' -c Release "-p:HarmonyPath=$HarmonyPath" "-p:IncludeSecondShiftStub=$includeSecondShift" 2>&1 |
            Tee-Object -FilePath (Join-Path $artifacts "harmony-smoke-second-shift-$includeSecondShift.log") | Out-Host
        if ($LASTEXITCODE -ne 0) { throw 'Harmony mock-panel regression checks failed. No package was installed.' }
    }
} finally { Pop-Location }

$localizationDir = Join-Path $projectRoot 'mod\Localizations'
$english = @(Import-Csv -LiteralPath (Join-Path $localizationDir 'enUS_HaulingPostPlus.csv') -Encoding utf8)
$chinese = @(Import-Csv -LiteralPath (Join-Path $localizationDir 'zhCN_HaulingPostPlus.csv') -Encoding utf8)
if ($english.Count -ne 7 -or $chinese.Count -ne 7) { throw 'Expected 7 localization keys per language.' }
if (Compare-Object ($english.ID | Sort-Object) ($chinese.ID | Sort-Object)) { throw 'Localization keys do not match.' }
foreach ($language in @($english, $chinese)) {
    if (@($language.ID | Select-Object -Unique).Count -ne 7) { throw 'Duplicate localization keys.' }
    foreach ($entry in $language) {
        if (!$entry.Text) { throw "Empty localization: $($entry.ID)" }
        # Verify format strings used by the panel.
        $null = [string]::Format($entry.Text, 1, 2, 1000)
    }
}

$thumbnail = Get-Item -LiteralPath (Join-Path $projectRoot 'mod\thumbnail.jpg')
if ($thumbnail.Length -eq 0 -or $thumbnail.Length -ge 1000000) {
    throw 'Workshop thumbnail.jpg must be nonempty and smaller than 1 MB.'
}

$outputRoot = Join-Path $projectRoot "dist\$buildId"
$package = Join-Path $outputRoot 'HaulingPostPlus'
New-Item -ItemType Directory -Path $package | Out-Null
Get-ChildItem -LiteralPath (Join-Path $projectRoot 'mod') | Copy-Item -Destination $package -Recurse
$scripts = Join-Path $package 'Scripts'
New-Item -ItemType Directory -Path $scripts | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'src\HaulingPostPlus\bin\Release\netstandard2.1\HaulingPostPlus.dll') -Destination $scripts
$expectedFiles = @(
    'manifest.json', 'thumbnail.jpg', 'Scripts/HaulingPostPlus.dll',
    'Buildings/DistrictManagement/HaulingPost/HaulingPost.Folktails.blueprint.json',
    'Buildings/DistrictManagement/HaulingPost/HaulingPost.IronTeeth.blueprint.json',
    'Localizations/enUS_HaulingPostPlus.csv', 'Localizations/zhCN_HaulingPostPlus.csv'
)
$actualFiles = @(Get-ChildItem -LiteralPath $package -File -Recurse | ForEach-Object { $_.FullName.Substring($package.Length + 1).Replace('\', '/') })
if (Compare-Object $expectedFiles $actualFiles) { throw 'Unexpected package contents; game and dependency DLLs must not be distributed.' }
$zip = Join-Path $outputRoot "HaulingPostPlus-$($manifest.Version).zip"
Compress-Archive -LiteralPath $package -DestinationPath $zip -CompressionLevel Optimal
$installedTo = $null
$backup = $null
if ($Install) {
    if (Get-Process -Name Timberborn -ErrorAction SilentlyContinue) { throw 'Close Timberborn before installing. The package is built but was not installed.' }
    $modsRoot = [IO.Path]::GetFullPath($ModsPath)
    $installedTo = Join-Path $modsRoot 'HaulingPostPlus'
    if (Test-Path -LiteralPath $installedTo) {
        $oldManifest = Join-Path $installedTo 'manifest.json'
        if (!(Test-Path -LiteralPath $oldManifest) -or
            (Get-Content -LiteralPath $oldManifest -Raw | ConvertFrom-Json).Id -ne 'HaulingPostPlus') {
            throw "Refusing to overwrite an unrelated directory: $installedTo"
        }
        $unknownDll = Get-ChildItem -LiteralPath $installedTo -Recurse -File -Filter '*.dll' |
            Where-Object { $_.FullName -ne (Join-Path $installedTo 'Scripts\HaulingPostPlus.dll') }
        if ($unknownDll) { throw 'Unexpected DLLs in the existing mod. Inspect them before updating.' }
        $backup = Join-Path $artifacts 'previous-local-mod'
        Copy-Item -LiteralPath $installedTo -Destination $backup -Recurse
    }
    New-Item -ItemType Directory -Path $installedTo -Force | Out-Null
    foreach ($relative in $expectedFiles) {
        $destination = Join-Path $installedTo $relative
        New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $package $relative) -Destination $destination -Force
        if ((Get-FileHash -LiteralPath $destination).Hash -ne (Get-FileHash -LiteralPath (Join-Path $package $relative)).Hash) {
            throw "Installed-file verification failed: $relative"
        }
    }
}
$report = [ordered]@{
    Version = $manifest.Version
    BuildId = $buildId
    CreatedUtc = [DateTime]::UtcNow.ToString('O')
    GamePath = $GamePath
    HarmonyPath = $HarmonyPath
    HarmonyVersion = $harmonyVersion.ToString()
    PackagePath = $package
    ZipPath = $zip
    ZipSHA256 = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
    DllSHA256 = (Get-FileHash -LiteralPath (Join-Path $scripts 'HaulingPostPlus.dll') -Algorithm SHA256).Hash
    InstalledTo = $installedTo
    PreviousLocalModBackup = $backup
    AutomatedChecks = 'Passed (see tests.log); localization and package checks passed'
    InGameValidation = 'Pending user testing'
}
$report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $artifacts 'build-report.json') -Encoding utf8
$report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $projectRoot 'dist\latest-build.json') -Encoding utf8
Write-Output "Package: $zip"
if ($installedTo) { Write-Output "Installed and hash-verified: $installedTo" }
Write-Output "Report: $artifacts"
