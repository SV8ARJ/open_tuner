<#
.SYNOPSIS
    Builds the win-x64 release zip for a GitHub release (fork tooling, dh1rk/open_tuner).

.DESCRIPTION
    Packages bin\Release into opentuner-<Tag>-win-x64.zip at the repo root, the same way
    beta1/beta2/beta3 were built by hand. Excludes local/personal state (settings\, logs\,
    stray *.ts recordings, Screenshots\/Videos\ contents) and the *top-level* bin\Release\win-x64\
    folder (a leftover `dotnet publish -r win-x64` self-contained output, not needed here) -
    while explicitly keeping bin\Release\libvlc\win-x64\, which looks similar but is the real
    64-bit VLC native library LibVLCSharp needs at runtime (PlatformTarget is x64). A prior
    manual robocopy /XD run excluded both by matching the bare folder name "win-x64" anywhere in
    the tree, silently shipping a beta3 zip without VLC's x64 libs - this script excludes by full
    path instead, and verifies libvlc\win-x64\libvlc.dll survived before zipping.

.PARAMETER Tag
    The release tag, e.g. "V0.B-dh1rk-beta3". Used only for the output zip's file name.

.PARAMETER Configuration
    Build configuration to package. Default: Release.

.PARAMETER SkipBuild
    Skip the `dotnet build` step and package whatever is already in bin\<Configuration>.

.PARAMETER Upload
    After zipping, upload the zip (and ReleaseNotes.txt from the repo root, if present) as
    assets to the GitHub release matching -Tag, via `gh release upload --clobber`. The release
    itself (draft, notes, etc.) is not created by this script - create/edit it with `gh release
    create`/the GitHub web UI first. Off by default: packaging is safe to run repeatedly, but
    publishing an asset is a visible action, so it needs an explicit opt-in.

.PARAMETER Repo
    "owner/repo" to upload to when -Upload is set. Default: DH1RK/open_tuner.

.PARAMETER AllowUnclean
    Skip the release guards (tag must exist, HEAD must be that tag, no modified tracked files,
    baked version must equal the tag without the "V0.B-dh1rk-" prefix). For test packaging only.

.EXAMPLE
    .\scripts\Build-ReleaseZip.ps1 -Tag "V0.B-dh1rk-beta4"

.EXAMPLE
    .\scripts\Build-ReleaseZip.ps1 -Tag "V0.B-dh1rk-beta4" -Upload
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Tag,

    [string]$Configuration = "Release",

    [switch]$SkipBuild,

    [switch]$Upload,

    [string]$Repo = "DH1RK/open_tuner",

    [switch]$AllowUnclean
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$src = Join-Path $repoRoot "bin\$Configuration"
$staging = Join-Path $repoRoot "bin\release-package-$Tag"
$zipPath = Join-Path $repoRoot "opentuner-$Tag-win-x64.zip"

# The window title shows `git describe` (baked in at build time, see opentuner.csproj). A clean
# release build must therefore be made from a checkout where HEAD *is* the tag and no tracked file
# is modified - otherwise the title reads e.g. "beta4-3-g1a2b3c4-dirty" instead of "beta4".
# Untracked files don't count. -AllowUnclean skips these checks for test packaging.
$expectedVersion = $Tag -replace '^V0\.B-dh1rk-', ''
if (-not $AllowUnclean) {
    $tagCommit = (git -C $repoRoot rev-parse -q --verify "refs/tags/$Tag^{commit}")
    if (-not $tagCommit) { throw "Tag '$Tag' does not exist. Create it first (git tag $Tag) or use -AllowUnclean for a test build." }
    $head = (git -C $repoRoot rev-parse HEAD)
    if ($tagCommit -ne $head) { throw "HEAD ($($head.Substring(0,7))) is not tag '$Tag' ($($tagCommit.Substring(0,7))). Check out the tagged commit or use -AllowUnclean." }
    $dirty = git -C $repoRoot status --porcelain --untracked-files=no
    if ($dirty) { throw "Tracked files are modified (would show '-dirty' in the title):`n$dirty`nCommit/stash them or use -AllowUnclean." }
    if ($SkipBuild) { Write-Warning "-SkipBuild: the title version comes from the last build, verifying it below." }
}

if (-not $SkipBuild) {
    Write-Host "Building $Configuration..."
    dotnet build (Join-Path $repoRoot "opentuner.sln") -c $Configuration
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed with exit code $LASTEXITCODE" }
}

# Verify what actually got baked into the exe (obj\<Configuration>\gen.cs, written by opentuner.csproj).
$gen = Join-Path $repoRoot "obj\$Configuration\gen.cs"
if (Test-Path $gen) {
    $baked = [regex]::Match((Get-Content $gen -Raw), 'GitDescribe = "([^"]*)"').Groups[1].Value
    Write-Host "Version baked into the build (window title): $baked"
    if (-not $AllowUnclean -and $baked -ne $expectedVersion) {
        throw "Baked version '$baked' does not match expected '$expectedVersion' - rebuild from the tagged, clean checkout (not -SkipBuild after tagging)."
    }
}

if (-not (Test-Path $src)) { throw "Build output not found: $src" }

if (Test-Path $staging) { Remove-Item -Path $staging -Recurse -Force }
New-Item -ItemType Directory -Path $staging | Out-Null

Write-Host "Staging $src -> $staging ..."
robocopy.exe $src $staging /MIR /XF "*.ts" /NFL /NDL /NJH /NJS | Out-Null
# robocopy's own exit codes are bit flags where 0-7 all mean success (some files copied/none
# needed copying) - only 8+ is a real error.
if ($LASTEXITCODE -ge 8) { throw "robocopy failed with exit code $LASTEXITCODE" }
$global:LASTEXITCODE = 0  # robocopy's own success codes (0-7) would otherwise leak out as this script's exit code

# Local/personal state - excluded by full path, never by bare folder name (see .DESCRIPTION).
Remove-Item -Path (Join-Path $staging "settings") -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path (Join-Path $staging "logs") -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path (Join-Path $staging "win-x64") -Recurse -Force -ErrorAction SilentlyContinue
Get-ChildItem -Path (Join-Path $staging "Screenshots") -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force
Get-ChildItem -Path (Join-Path $staging "Videos") -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force

$libvlcX64 = Join-Path $staging "libvlc\win-x64\libvlc.dll"
if (-not (Test-Path $libvlcX64)) {
    throw "$libvlcX64 is missing - the x64 VLC native library did not make it into staging. " +
          "Do NOT zip this; check the Release build output first."
}
Write-Host "Verified: libvlc\win-x64\libvlc.dll present."

if (Test-Path $zipPath) { Remove-Item -Path $zipPath -Force }
Write-Host "Compressing to $zipPath ..."
Compress-Archive -Path (Join-Path $staging "*") -DestinationPath $zipPath -CompressionLevel Optimal

Remove-Item -Path $staging -Recurse -Force

$sizeMb = (Get-Item $zipPath).Length / 1MB
Write-Host ("Done: {0} ({1:N0} MB)" -f $zipPath, $sizeMb)

if ($Upload) {
    $gh = "C:\Program Files\GitHub CLI\gh.exe"
    if (-not (Test-Path $gh)) { $gh = "gh" }  # fall back to PATH if not installed at the usual spot

    Write-Host "Uploading $zipPath to $Repo release $Tag ..."
    & $gh release upload $Tag $zipPath --repo $Repo --clobber
    if ($LASTEXITCODE -ne 0) { throw "gh release upload failed with exit code $LASTEXITCODE" }

    $releaseNotes = Join-Path $repoRoot "ReleaseNotes.txt"
    if (Test-Path $releaseNotes) {
        Write-Host "Uploading ReleaseNotes.txt ..."
        & $gh release upload $Tag $releaseNotes --repo $Repo --clobber
        if ($LASTEXITCODE -ne 0) { throw "gh release upload (ReleaseNotes.txt) failed with exit code $LASTEXITCODE" }
    }

    Write-Host "Uploaded."
}
