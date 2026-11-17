<#
.SYNOPSIS
    Automated checks for context/code_rules.md. Exits non-zero on any violation.

.DESCRIPTION
    Grep-level checks for the rules that can be checked statically. Accepted deviations live in
    $Baseline, pinned to File:Line, each with a KD-xx row in code_rules.md. A baselined hit reports
    as "base"; any other hit fails. Checks for parts that don't exist yet (frontend, sandbox runner)
    report "skip" until the code lands.

    Windows PowerShell 5.1 compatible.  Usage:  powershell -File scripts/check-rules.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$failed = 0

# Accepted deviations: rule id -> list of "File.cs:Line" (see Known Deviations in code_rules.md)
$Baseline = @{
    # KD-01: template auth slices are anonymous by design; deleted in Phase 1 (Clerk).
    'EP-04' = @('Login.cs', 'Register.cs', 'Refresh.cs')
    # KD-01: template JWT issuing; TokenProvider is deleted in Phase 1 (Clerk).
    'DM-04' = @('TokenProvider.cs:28')
}

function Get-SourceFiles([string]$path, [string]$filter = '*.cs') {
    if (-not (Test-Path $path)) { return @() }
    Get-ChildItem $path -Recurse -Filter $filter -File |
        Where-Object { $_.FullName -notmatch '\\(bin|obj|node_modules|dist|Migrations)\\' }
}

function Test-Baselined([string]$id, [string]$location) {
    if (-not $Baseline.ContainsKey($id)) { return $false }
    foreach ($entry in $Baseline[$id]) {
        if ($location.StartsWith($entry)) { return $true }
    }
    return $false
}

# $hits: array of "File:Line  detail" strings
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

function Find([object[]]$files, [string]$pattern, [switch]$CaseSensitive) {
    if (-not $files -or $files.Count -eq 0) { return @() }
    $files | Select-String -Pattern $pattern -CaseSensitive:$CaseSensitive |
        Where-Object { $_.Line -notmatch '^\s*(<!--|//|#)' } |
        ForEach-Object { "$($_.Filename):$($_.LineNumber)  $($_.Line.Trim())" }
}

$api      = Join-Path $root 'src\Web.Api'
$features = Join-Path $api 'Features'
$worker   = Join-Path $root 'src\Worker'
$contracts = Join-Path $root 'src\Contracts'
$runner   = Join-Path $root 'src\Sandbox.Runner'
$apiFiles = @(Get-SourceFiles $api)
$srcFiles = @(Get-SourceFiles (Join-Path $root 'src'))

# Slice files = files under Features that declare a Handler
$sliceFiles = @(Get-SourceFiles $features | Where-Object { (Get-Content $_.FullName -Raw) -match 'class Handler\b' })

function Get-EndpointRegion($file) {
    $text = Get-Content $file.FullName -Raw
    $i = $text.IndexOf('class Endpoint')
    if ($i -lt 0) { return $null }
    return $text.Substring($i)
}

Write-Host "`n== Architecture & boundaries" -ForegroundColor White

# AR-01 single API project
$layerProjects = @('Domain', 'Application', 'Infrastructure', 'SharedKernel') |
    Where-Object { Test-Path (Join-Path $root "src\$_") } | ForEach-Object { "src\$_" }
Report 'AR-01' 'No Domain/Application/Infrastructure layer projects' $layerProjects

# AR-02 worker has no DB access
$workerHits = @()
$workerHits += Find (Get-SourceFiles $worker '*.csproj') 'EntityFrameworkCore|Npgsql|Web\.Api'
$workerHits += Find (Get-SourceFiles $worker '*.cs') 'Npgsql|EntityFrameworkCore|DbContext|ConnectionStrings:Database|GetConnectionString\("Database"\)'
$workerHits += Find (Get-SourceFiles $worker '*.json') '"Database"\s*:' -CaseSensitive
Report 'AR-02' 'Worker has no database access' $workerHits

# AR-03 contracts BCL-only
Report 'AR-03' 'Contracts references no package or project' (Find (Get-SourceFiles $contracts '*.csproj') 'PackageReference|ProjectReference')

# AR-04 runner never references API / Worker / DB
Report 'AR-04' 'Sandbox.Runner references only Contracts (+ Roslyn/EF for graders)' (Find (Get-SourceFiles $runner '*.csproj') 'Web\.Api|Worker\.csproj')

# AR-05 cross-feature slice coupling
$sliceNamesByFeature = @{}
foreach ($f in $sliceFiles) {
    $feature = Split-Path (Split-Path $f.FullName -Parent) -Leaf
    if (-not $sliceNamesByFeature.ContainsKey($feature)) { $sliceNamesByFeature[$feature] = @() }
    $sliceNamesByFeature[$feature] += $f.BaseName
}
$crossHits = @()
foreach ($file in (Get-SourceFiles $features)) {
    $feature = Split-Path (Split-Path $file.FullName -Parent) -Leaf
    foreach ($other in $sliceNamesByFeature.Keys) {
        if ($other -eq $feature) { continue }
        foreach ($slice in $sliceNamesByFeature[$other]) {
            $crossHits += Find @($file) "\b$slice\.(Command|Query|Handler|Validator|Endpoint|Response)\b"
        }
    }
}
Report 'AR-05' 'No cross-feature Command/Query/Handler/Validator/Endpoint references' $crossHits

# AR-06 API never references Worker / Runner
Report 'AR-06' 'Web.Api does not reference Worker or Sandbox.Runner' (Find (Get-SourceFiles $api '*.csproj') 'Worker\.csproj|Sandbox\.Runner')

# AR-08 NATS subjects only from Contracts.Subjects
Report 'AR-08' 'No string-literal NATS subjects outside Contracts' (Find ($srcFiles | Where-Object { $_.FullName -notlike "$contracts*" }) '"(jobs|results)\.[a-z.>]+"')

Write-Host "`n== Vertical slices" -ForegroundColor White

# VS-01 one file per use case: slice file declares public static class named like the file
$vs01 = foreach ($f in $sliceFiles) {
    if (-not (Select-String -Path $f.FullName -Pattern "public static class $($f.BaseName)\b" -Quiet)) { "$($f.Name)  missing 'public static class $($f.BaseName)'" }
}
Report 'VS-01' 'Slice file = public static class {UseCase}' $vs01

# VS-02 handler internal sealed
Report 'VS-02' 'Handlers are internal sealed' (Find $sliceFiles 'class Handler\b' | Where-Object { $_ -notmatch 'internal sealed class Handler' })

# VS-03 no manual wiring of handlers / no MediatR / no IApplicationDbContext
$vs03 = @()
$vs03 += Find $apiFiles 'Add(Scoped|Transient|Singleton)<I(Command|Query)Handler'
$vs03 += Find $apiFiles '\bMediatR\b|\bISender\b|IApplicationDbContext'
$vs03 += Find (Get-SourceFiles $api '*.csproj') 'MediatR'
Report 'VS-03' 'No manual slice wiring, MediatR or IApplicationDbContext' $vs03

# VS-05 every command has a validator
$vs05 = foreach ($f in $sliceFiles) {
    $t = Get-Content $f.FullName -Raw
    if ($t -match 'record Command\b[^;{]*:\s*ICommand' -and $t -notmatch 'class Validator\b') { "$($f.Name)  command without Validator" }
}
Report 'VS-05' 'Every Command has a nested Validator' $vs05

$commands = @($sliceFiles | Where-Object { (Get-Content $_.FullName -Raw) -match ':\s*ICommand' }).Count
$queries  = @($sliceFiles | Where-Object { (Get-Content $_.FullName -Raw) -match ':\s*IQuery<' }).Count
Info 'VS' "slices=$($sliceFiles.Count) commands=$commands queries=$queries"

Write-Host "`n== Domain & data" -ForegroundColor White

# DM-04 time from IDateTimeProvider
Report 'DM-04' 'No DateTime.Now/UtcNow outside DateTimeProvider' (Find ($srcFiles | Where-Object { $_.Name -ne 'DateTimeProvider.cs' }) 'DateTime(Offset)?\.(UtcNow|Now)\b')

# DM-05 no persistence attributes on entities
Report 'DM-05' 'No [Table]/[Column]/[Key]/[MaxLength]/[Required] in Features' (Find (Get-SourceFiles $features) '\[(Table|Column|Key|MaxLength|Required|ForeignKey)\b')

Write-Host "`n== Errors & endpoints" -ForegroundColor White

# EP-01 no exceptions for expected failures in slices
Report 'EP-01' 'No throw in slice handlers (return Result.Failure)' (Find $sliceFiles '^\s*throw new ')

# EP-03 / EP-04 / EP-05 per endpoint region
$ep03 = @(); $ep04 = @(); $ep05 = @()
foreach ($f in $sliceFiles) {
    $region = Get-EndpointRegion $f
    if (-not $region) { continue }
    if ($region -match 'ApplicationDbContext|SaveChangesAsync|\.Users\.|\.Problems\.|\.Submissions\.') { $ep03 += "$($f.Name)  data access in Endpoint" }
    if ($region -notmatch 'RequireAuthorization|HasPermission|AllowAnonymous') { $ep04 += "$($f.Name)  no explicit auth decision" }
    if ($region -notmatch 'WithTags\(Tags\.') { $ep05 += "$($f.Name)  no .WithTags(Tags.X)" }
}
Report 'EP-03' 'Endpoints contain no data access' $ep03
Report 'EP-04' 'Every endpoint makes an explicit auth decision' $ep04
Report 'EP-05' 'Every endpoint tagged via Tags constants' $ep05

Write-Host "`n== Sandbox & secrets" -ForegroundColor White

$sandboxRunner = Get-SourceFiles $worker | Where-Object { $_.Name -eq 'SandboxRunner.cs' } | Select-Object -First 1
if ($sandboxRunner) {
    $t = Get-Content $sandboxRunner.FullName -Raw
    $sb01 = @()
    foreach ($flag in @('NetworkMode\s*=\s*"none"', 'ReadonlyRootfs\s*=\s*true', 'CapDrop', 'no-new-privileges', 'PidsLimit', 'MemorySwap')) {
        if ($t -notmatch $flag) { $sb01 += "SandboxRunner.cs  missing $flag" }
    }
    Report 'SB-01' 'Sandbox containers fully hardened' $sb01
}
else { Skip 'SB-01' 'Sandbox containers fully hardened' 'no SandboxRunner yet' }

Report 'SB-02' 'Host Docker socket never used by Worker' (Find (Get-SourceFiles $worker '*.*') 'docker\.sock')

$webSrc = Join-Path $root 'apps\web\src'
$secretPattern = 'sk_(test|live)_[A-Za-z0-9]{8,}|whsec_[A-Za-z0-9]{8,}|pdl_ntfset_|ghp_[A-Za-z0-9]{20,}|github_pat_'
Report 'SC-01' 'No secret values in source' (Find (@($srcFiles) + @(Get-SourceFiles $webSrc '*.ts*') + @(Get-SourceFiles (Join-Path $root 'deploy') '*.yml')) $secretPattern)

Write-Host "`n== Frontend" -ForegroundColor White
if (Test-Path $webSrc) {
    $ts = @(Get-SourceFiles $webSrc '*.ts') + @(Get-SourceFiles $webSrc '*.tsx')
    $ts = @($ts | Where-Object { $_.Name -notmatch '\.(test|spec)\.tsx?$' })
    Report 'FE-01' 'No explicit any' (Find $ts ':\s*any\b|as any\b|<any>')
    Report 'FE-04' 'fetch only in lib/api.ts' (Find ($ts | Where-Object { $_.FullName -notmatch '\\lib\\api\.ts$' }) '\bfetch\(')
    Report 'FE-05' 'import.meta.env only in config.ts' (Find ($ts | Where-Object { $_.Name -ne 'config.ts' }) 'import\.meta\.env')
    Report 'FE-06' 'No secret-looking config names in the web app' (Find $ts '(SECRET|PASSWORD|API_KEY)\b')
}
else { Skip 'FE-*' 'Frontend rules' 'apps/web not scaffolded yet' }

Write-Host "`n== Cross-cutting" -ForegroundColor White
Report 'XC-01' 'Central Package Management: no Version= in csproj' (Find (@(Get-SourceFiles (Join-Path $root 'src') '*.csproj') + @(Get-SourceFiles (Join-Path $root 'tests') '*.csproj')) 'PackageReference[^>]*Version=')
Report 'XC-02' 'Every #pragma warning disable has a justification comment' (Find $srcFiles '#pragma warning disable\s+\S+\s*$')
Report 'XC-03' 'No sync-over-async' (Find $srcFiles '\.Result\b(?!\s*<)|\.Wait\(\)|GetAwaiter\(\)\.GetResult\(\)' | Where-Object { $_ -notmatch 'Result\.(Success|Failure)|IsSuccess|result\.Result' })

Write-Host ''
if ($failed -gt 0) { Write-Host "$failed rule(s) failed." -ForegroundColor Red; exit 1 }
Write-Host 'All checked rules pass.' -ForegroundColor Green
exit 0
