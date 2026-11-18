<#
.SYNOPSIS
    REA self-audit of CodeBench's own build artefacts (build plan Appendix D, checks RA-01..RA-09).
    Exits non-zero on any violation.

.DESCRIPTION
    Companion to check-rules.ps1: that script checks source, this one checks what we ship, using the
    REA CLI (npm i -g rea-agents) for static inspection. Evidence JSON is written to artifacts/rea/
    (git-ignored). Checks whose artefact doesn't exist yet report "skip"; with -Ci a missing artefact
    for an active check is a failure instead.

    Only ever point this at CodeBench artefacts (ADR 0006).

    Windows PowerShell 5.1 and pwsh compatible.

.PARAMETER Build
    Run `dotnet build CodeBench.slnx -c <Configuration>` first.
.PARAMETER Ci
    Missing artefacts for active checks fail instead of skip.
.PARAMETER UpdateBaseline
    After a green run, copy the evidence to artifacts/rea/baseline/ (used by RA-08 drift reports).

.EXAMPLE
    powershell -File scripts/rea-audit.ps1 -Build
    powershell -File scripts/rea-audit.ps1 -UpdateBaseline
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [switch]$Build,
    [switch]$Ci,
    [switch]$UpdateBaseline
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$evidenceDir = Join-Path $root 'artifacts\rea'
$baselineDir = Join-Path $evidenceDir 'baseline'
$failed = 0
$utf8 = New-Object System.Text.UTF8Encoding $false

# Accepted deviations: rule id -> list of hit prefixes (each needs a KD-xx row in code_rules.md).
$Baseline = @{}

# ---------------------------------------------------------------- policy
# Packages/assemblies that give database access — forbidden in the Worker (AR-02) and anything it loads.
$DbAccess = '^(Npgsql|Microsoft\.EntityFrameworkCore|Microsoft\.Data\.SqlClient|System\.Data\.SqlClient|Dapper|Web\.Api)(\b|$)'
# Sandbox.Runner allow-list (AR-04). Phase 5 adds EF Core + Npgsql for the database image only.
$RunnerAllowed = '^(System(\.|$)|netstandard$|mscorlib$|CodeBench\.Contracts$|Microsoft\.CodeAnalysis(\.|$))'
$RunnerPackagesAllowed = '^(Microsoft\.CodeAnalysis|System\.|Microsoft\.NETCore\.|Humanizer)'

# ---------------------------------------------------------------- reporting (same format as check-rules.ps1)
function Test-Baselined([string]$id, [string]$hit) {
    if (-not $Baseline.ContainsKey($id)) { return $false }
    foreach ($entry in $Baseline[$id]) { if ($hit.StartsWith($entry)) { return $true } }
    return $false
}

function Report([string]$id, [string]$title, $hits) {
    $real = @(); $based = @()
    foreach ($h in @($hits)) {
        if (-not $h) { continue }
        if (Test-Baselined $id $h) { $based += $h } else { $real += $h }
    }
    if ($real.Count -gt 0) {
        Write-Host ("FAIL {0,-6} {1}" -f $id, $title) -ForegroundColor Red
        $real | Select-Object -First 8 | ForEach-Object { Write-Host "       $_" }
        $script:failed++
    }
    elseif ($based.Count -gt 0) {
        Write-Host ("base {0,-6} {1} ({2} accepted)" -f $id, $title, $based.Count) -ForegroundColor Yellow
    }
    else {
        Write-Host ("ok   {0,-6} {1}" -f $id, $title) -ForegroundColor Green
    }
}

function Skip([string]$id, [string]$title, [string]$why) {
    Write-Host ("skip {0,-6} {1} ({2})" -f $id, $title, $why) -ForegroundColor DarkGray
}

function Info([string]$id, [string]$text) {
    Write-Host ("info {0,-6} {1}" -f $id, $text) -ForegroundColor Cyan
}

# A missing artefact for an active check: skip locally, fail in CI.
function Missing([string]$id, [string]$title, [string]$path) {
    $rel = $path.Replace($root + '\', '')
    if ($Ci) { Report $id $title @("artefact missing: $rel") }
    else { Skip $id $title "not built: $rel - run with -Build" }
}

# ---------------------------------------------------------------- REA helpers
function Invoke-Rea([string[]]$arguments, [string]$evidenceName) {
    # REA writes diagnostics to stderr; under 'Stop', PS 5.1 would turn those into terminating errors.
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { $raw = & rea @arguments --format json 2>$null | Out-String }
    finally { $ErrorActionPreference = $previous }
    $exit = $LASTEXITCODE

    $outFile = Join-Path $evidenceDir $evidenceName
    [System.IO.File]::WriteAllText($outFile, $raw, $utf8)

    try { $json = $raw | ConvertFrom-Json }
    catch { throw "REA returned non-JSON output (exit $exit), see $outFile" }
    if ($json.PSObject.Properties.Name -contains 'ok' -and -not $json.ok) {
        throw "REA error $($json.error.code): $($json.error.message)"
    }
    return $json
}

# Direct assembly references of a managed DLL (REA inspect-managed-artifact; no code is loaded).
function Get-AssemblyReferences([string]$dll, [string]$evidenceName) {
    $json = Invoke-Rea @('inspect-managed-artifact', $dll, '--filter-output',
        'normalized_result.assembly.name,normalized_result.references') $evidenceName
    return @($json.normalized_result.references | ForEach-Object { $_.name })
}

# REA lists DIRECT references only, so transitive packages come from the deps.json next to the DLL.
function Get-DepsLibraries([string]$depsJson) {
    if (-not (Test-Path $depsJson)) { return @() }
    $deps = Get-Content $depsJson -Raw | ConvertFrom-Json
    return @($deps.libraries.PSObject.Properties | ForEach-Object {
        [pscustomobject]@{ Name = ($_.Name -split '/')[0]; Version = ($_.Name -split '/')[1]; Type = $_.Value.type }
    })
}

function Get-BinPath([string]$project, [string]$assembly) {
    Join-Path $root "src\$project\bin\$Configuration\net10.0\$assembly"
}

# ---------------------------------------------------------------- preflight
if (-not (Get-Command rea -ErrorAction SilentlyContinue)) {
    Write-Host 'FAIL REA    REA CLI not found - npm i -g rea-agents' -ForegroundColor Red
    exit 1
}
$reaVersion = (& rea --version 2>$null | Out-String).Trim()

if ($Build) {
    Write-Host "== dotnet build ($Configuration)"
    & dotnet build (Join-Path $root 'CodeBench.slnx') -c $Configuration --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { Write-Host 'FAIL BUILD  dotnet build failed' -ForegroundColor Red; exit 1 }
}

New-Item -ItemType Directory -Force $evidenceDir | Out-Null
Info 'REA' "rea $reaVersion - evidence in artifacts/rea/"

# ---------------------------------------------------------------- RA-01 Worker has no database access
Write-Host "`n== Assembly boundaries"
$id = 'RA-01'; $title = 'Worker DLL + deps.json carry no database access (AR-02)'
$workerDll = Get-BinPath 'Worker' 'CodeBench.Worker.dll'
if (Test-Path $workerDll) {
    $hits = @()
    $refs = Get-AssemblyReferences $workerDll 'RA-01-worker-refs.json'
    $hits += $refs | Where-Object { $_ -match $DbAccess } | ForEach-Object { "CodeBench.Worker.dll references $_" }
    $libs = Get-DepsLibraries ([System.IO.Path]::ChangeExtension($workerDll, '.deps.json'))
    $hits += $libs | Where-Object { $_.Name -match $DbAccess } |
        ForEach-Object { "CodeBench.Worker.deps.json pulls $($_.Name) $($_.Version) ($($_.Type))" }
    Report $id $title $hits
    Info $id ("{0} direct refs, {1} deps.json libraries" -f $refs.Count, $libs.Count)
}
else { Missing $id $title $workerDll }

# ---------------------------------------------------------------- RA-02 Contracts is BCL-only
$id = 'RA-02'; $title = 'Contracts DLL references the BCL only (AR-03)'
$contractsDll = Get-BinPath 'Contracts' 'CodeBench.Contracts.dll'
if (Test-Path $contractsDll) {
    $hits = @()
    $refs = Get-AssemblyReferences $contractsDll 'RA-02-contracts-refs.json'
    $hits += $refs | Where-Object { $_ -notmatch '^(System(\.|$)|netstandard$|mscorlib$)' } |
        ForEach-Object { "CodeBench.Contracts.dll references $_" }
    $libs = Get-DepsLibraries ([System.IO.Path]::ChangeExtension($contractsDll, '.deps.json'))
    $hits += $libs | Where-Object { $_.Type -ne 'project' } |
        ForEach-Object { "CodeBench.Contracts.deps.json pulls $($_.Name) $($_.Version) ($($_.Type))" }
    Report $id $title $hits
}
else { Missing $id $title $contractsDll }

# ---------------------------------------------------------------- RA-03 Sandbox.Runner allow-list
$id = 'RA-03'; $title = 'Sandbox.Runner references only the allow-list (AR-04)'
$runnerDll = Get-BinPath 'Sandbox.Runner' 'CodeBench.Sandbox.Runner.dll'
if (Test-Path $runnerDll) {
    $hits = @()
    $refs = Get-AssemblyReferences $runnerDll 'RA-03-runner-refs.json'
    $hits += $refs | Where-Object { $_ -notmatch $RunnerAllowed } |
        ForEach-Object { "CodeBench.Sandbox.Runner.dll references $_" }
    $libs = Get-DepsLibraries ([System.IO.Path]::ChangeExtension($runnerDll, '.deps.json'))
    $hits += $libs | Where-Object { $_.Type -eq 'package' -and $_.Name -notmatch $RunnerPackagesAllowed } |
        ForEach-Object { "CodeBench.Sandbox.Runner.deps.json pulls $($_.Name) $($_.Version)" }
    $hits += $libs | Where-Object { $_.Name -match $DbAccess } |
        ForEach-Object { "CodeBench.Sandbox.Runner.deps.json pulls $($_.Name) (database grader only, Phase 5)" }
    Report $id $title $hits
}
else { Missing $id $title $runnerDll }

# ---------------------------------------------------------------- not active yet
Write-Host "`n== Shipped bundles and live app"
Skip 'RA-04' 'No secret-shaped literals in API/Worker DLLs (SC-01)' 'Phase 1.12'
Skip 'RA-05' 'Web bundle: no secrets, no source maps, admin lazy-only' 'apps/web not scaffolded - Phase 1.12'
Skip 'RA-06' 'Marketing bundle: summaries only, no hidden tests' 'apps/marketing not scaffolded - Phase 1.12'
Skip 'RA-07' 'Own-app network capture: no hidden data in responses' 'needs running app - Phase 1.12'
Skip 'RA-09' 'Visual baselines of key pages' 'needs running app - Phase 1.12'

# ---------------------------------------------------------------- RA-08 drift vs baseline (informational)
Write-Host "`n== Drift"
if (Test-Path $baselineDir) {
    foreach ($file in Get-ChildItem $evidenceDir -Filter 'RA-0*-refs.json' -File) {
        $before = Join-Path $baselineDir $file.Name
        if (-not (Test-Path $before)) { Info 'RA-08' "$($file.Name): new (no baseline)"; continue }
        $old = @((Get-Content $before -Raw | ConvertFrom-Json).normalized_result.references | ForEach-Object { $_.name })
        $new = @((Get-Content $file.FullName -Raw | ConvertFrom-Json).normalized_result.references | ForEach-Object { $_.name })
        $added = @($new | Where-Object { $old -notcontains $_ })
        $removed = @($old | Where-Object { $new -notcontains $_ })
        if ($added.Count -eq 0 -and $removed.Count -eq 0) { Info 'RA-08' "$($file.Name): unchanged" }
        else { Info 'RA-08' ("{0}: +[{1}] -[{2}]" -f $file.Name, ($added -join ', '), ($removed -join ', ')) }
    }
}
else { Skip 'RA-08' 'Reference drift vs baseline' 'no baseline - run with -UpdateBaseline' }

# ---------------------------------------------------------------- result
Write-Host ''
if ($failed -gt 0) {
    Write-Host "$failed REA audit check(s) failed." -ForegroundColor Red
    exit 1
}

if ($UpdateBaseline) {
    New-Item -ItemType Directory -Force $baselineDir | Out-Null
    Get-ChildItem $evidenceDir -Filter '*.json' -File | Copy-Item -Destination $baselineDir -Force
    $sha = (& git -C $root rev-parse --short HEAD 2>$null | Out-String).Trim()
    $manifest = [ordered]@{ created = (Get-Date).ToUniversalTime().ToString('o'); commit = $sha; rea = $reaVersion;
        configuration = $Configuration; files = @(Get-ChildItem $baselineDir -Filter 'RA-*.json' | ForEach-Object Name) }
    [System.IO.File]::WriteAllText((Join-Path $baselineDir 'manifest.json'), ($manifest | ConvertTo-Json), $utf8)
    Info 'RA-08' "baseline updated (commit $sha) - artifacts/rea/baseline/"
}

Write-Host 'All active REA audit checks pass.' -ForegroundColor Green
exit 0
