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
    # (empty) KD-01 closed 2026-11-19: template JWT issuing removed, Clerk validation in place.
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
$marketingDir = Join-Path $root 'apps\marketing'
$rawPalette = '\b(bg|text|border|ring|fill|stroke|from|to|via)-(white|black|(slate|gray|zinc|neutral|stone|red|orange|amber|yellow|lime|green|emerald|teal|cyan|sky|blue|indigo|violet|purple|fuchsia|pink|rose)-\d{2,3})\b'
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
    # Raw palette colours bypass the tokens (incl. shadcn's text-white on destructive). Roles only.
    Report 'FE-11' 'Colour roles only (no raw Tailwind palette classes)' (Find $ts $rawPalette -CaseSensitive)
    $stores = @($ts | Where-Object { $_.FullName -match '\\stores\\' })
    Report 'FE-15' 'zustand stores hold no tokens or auth' (Find $stores 'getToken|\btoken\b|Authorization|useAuth')
    Report 'FE-15' 'zustand devtools only behind config.isDev' (Find ($ts | Where-Object { (Get-Content $_.FullName -Raw) -notmatch 'isDev' }) '\bdevtools\(')
    Report 'FE-17' 'lucide-react named imports only' (Find $ts "import\s+\*\s+as\s+\w+\s+from\s+['""]lucide-react['""]|from\s+['""]lucide-react/dynamic['""]|\bDynamicIcon\b")
}
else { Skip 'FE-*' 'Frontend rules' 'apps/web not scaffolded yet' }

# FE-18: one brand. Icons in both apps are byte-identical copies of docs/design/brand/, and every place that
# draws the mark uses the same geometry as docs/design/brand/mark.svg.
$brandDir = Join-Path $root 'docs\design\brand'
if (Test-Path $brandDir) {
    $brandHits = @()
    foreach ($icon in 'favicon.svg', 'apple-touch-icon.png') {
        $source = Join-Path $brandDir $icon
        if (-not (Test-Path $source)) { $brandHits += "docs/design/brand/$icon missing"; continue }
        $sourceHash = (Get-FileHash $source).Hash
        foreach ($app in 'web', 'marketing') {
            $copy = Join-Path $root "apps\$app\public\$icon"
            if (-not (Test-Path (Join-Path $root "apps\$app"))) { continue }
            if (-not (Test-Path $copy)) { $brandHits += "apps/$app/public/$icon missing (copy it from docs/design/brand/)" }
            elseif ((Get-FileHash $copy).Hash -ne $sourceHash) { $brandHits += "apps/$app/public/$icon differs from docs/design/brand/$icon" }
        }
    }
    $geometry = @('M9.5 10.5 15 16l-5.5 5.5', 'x="17.5" y="14.5" width="7" height="8" rx="1.25"', 'rx="7"')
    $markFiles = @(
        (Join-Path $brandDir 'mark.svg'),
        (Join-Path $brandDir 'favicon.svg'),
        (Join-Path $root 'apps\web\src\components\Wordmark.tsx'),
        (Join-Path $root 'apps\marketing\src\components\Wordmark.astro')
    ) | Where-Object { Test-Path $_ }
    foreach ($file in $markFiles) {
        $text = [IO.File]::ReadAllText($file)
        foreach ($g in $geometry) {
            if (-not $text.Contains($g)) { $brandHits += "$($file.Substring($root.Length + 1)): mark geometry '$g' missing (docs/design/brand/mark.svg)" }
        }
    }
    # The old text-caret logo (U+258D). Read as UTF-8 explicitly: PS 5.1's Select-String reads BOM-less UTF-8 as
    # ANSI, so a Find() here would never match the glyph.
    $caret = [string][char]0x258D
    $appSources = @(Get-SourceFiles $webSrc '*.ts*') + @(Get-SourceFiles (Join-Path $marketingDir 'src') '*.astro')
    foreach ($file in $appSources) {
        $lineNo = 0
        foreach ($line in [IO.File]::ReadAllLines($file.FullName, [Text.Encoding]::UTF8)) {
            $lineNo++
            if ($line.Contains($caret)) { $brandHits += "$($file.Name):$lineNo  text-caret logo - use <Wordmark />" }
        }
    }
    Report 'FE-18' 'One brand: icons = docs/design/brand copies, same mark geometry, no text-caret logo' $brandHits
}

if (Test-Path (Join-Path $marketingDir 'src')) {
    $site = @('*.astro', '*.ts', '*.mjs', '*.mdx', '*.css') | ForEach-Object { Get-SourceFiles (Join-Path $marketingDir 'src') $_ }
    $site = @($site | Where-Object { $_.FullName -notmatch '\\content\\problems\\' })   # generated summaries
    $siteConfig = @(Get-SourceFiles $marketingDir 'astro.config.*' | Where-Object { $_.DirectoryName -eq $marketingDir })
    Report 'FE-11' 'Marketing: colour roles only (no raw Tailwind palette classes)' (Find $site $rawPalette -CaseSensitive)
    Report 'FE-13' 'Marketing: no client framework (React, shadcn/ui, zustand, lucide-react)' (Find $site "from\s+['""](react|react-dom|@astrojs/react|zustand|lucide-react|@radix-ui/[^'""]+)['""]|client:(load|idle|visible|only|media)")
    Report 'FE-13' 'Marketing: Tailwind v4 via @tailwindcss/vite, not @astrojs/tailwind' (Find (@($site) + @($siteConfig)) '@astrojs/tailwind')
}
else { Skip 'FE-13' 'Marketing site rules' 'apps/marketing not scaffolded yet' }

Write-Host "`n== Naming" -ForegroundColor White
# NOTE: PowerShell's -match/-eq/-ne are case-INSENSITIVE. Naming checks must use -cmatch/-cnotmatch/-ceq/-cne.

$testFiles = @(Get-SourceFiles (Join-Path $root 'tests'))
$csFiles = @($srcFiles) + $testFiles
function Rel($file) { $file.FullName.Substring($root.Length + 1) }

# Top-level type declarations start at column 0 (file-scoped namespaces); nested types are indented.
$topLevelType = '(?m)^(?:(?:public|internal|file|static|sealed|abstract|partial|readonly)\s+)*(?:class|record(?:\s+struct|\s+class)?|interface|enum|struct)\s+(\w+)'
$csTyped = @($csFiles | Where-Object { $_.Name -notin @('Program.cs', 'GlobalUsings.cs') })

# NM-01 one top-level type per file, file name = type name (generic-arity twins share a name)
$nm01 = foreach ($f in $csTyped) {
    $names = @([regex]::Matches((Get-Content $f.FullName -Raw), $topLevelType) | ForEach-Object { $_.Groups[1].Value } | Select-Object -Unique)
    if ($names.Count -gt 1) { "$(Rel $f)  declares $($names -join ', ')" }
    elseif ($names.Count -eq 1 -and $names[0] -cne $f.BaseName) { "$(Rel $f)  declares $($names[0])" }
}
Report 'NM-01' 'One top-level type per file, named like the file' $nm01

# NM-02 namespace = root namespace + folder path, file-scoped
$nsRoots = [ordered]@{
    'src\Web.Api' = 'Web.Api'; 'src\Contracts' = 'CodeBench.Contracts'; 'src\Worker' = 'CodeBench.Worker'
    'src\Sandbox.Runner' = 'CodeBench.Sandbox.Runner'; 'tests\Web.Api.UnitTests' = 'Web.Api.UnitTests'
    'tests\IntegrationTests' = 'IntegrationTests'; 'tests\ArchitectureTests' = 'ArchitectureTests'
}
$nm02 = foreach ($f in $csTyped) {
    $rel = Rel $f
    $project = $nsRoots.Keys | Where-Object { $rel.StartsWith("$_\") } | Select-Object -First 1
    if (-not $project) { continue }
    $text = Get-Content $f.FullName -Raw
    if ($text -cmatch '(?m)^namespace\s+[\w.]+\s*(\{|$)') { "$rel  block-scoped namespace (use 'namespace X;')"; continue }
    if ($text -cnotmatch '(?m)^namespace\s+([\w.]+)\s*;') { continue }
    $actual = $Matches[1]
    $folder = Split-Path $rel.Substring($project.Length + 1) -Parent
    $expected = if ($folder) { "$($nsRoots[$project]).$($folder.Replace('\', '.'))" } else { $nsRoots[$project] }
    if ($actual -cne $expected) { "$rel  namespace $actual, expected $expected" }
}
Report 'NM-02' 'Namespace = root namespace + folder path (file-scoped)' $nm02

# NM-04 Task-returning methods end in Async (except the CQRS contract method Handle); nothing else does
$modifiers = '(?:(?:public|private|protected|internal|static|async|override|virtual|sealed|abstract|new|extern)\s+)'
$nm04 = @()
foreach ($f in $srcFiles) {
    $n = 0
    foreach ($line in Get-Content $f.FullName) {
        $n++
        if ($line -cmatch "^\s*$modifiers*(?:Task|ValueTask)(?:<.+?>)?\s+(\w+)\s*[\(<]" -and $Matches[1] -cnotmatch 'Async$' -and $Matches[1] -cne 'Handle') {
            $nm04 += "$(Rel $f):$n  $($Matches[1]) returns a Task but lacks 'Async'"
        }
        elseif ($line -cmatch "^\s*$modifiers+([\w.]+(?:<.+?>)?\??)\s+(\w+Async)\s*\(" -and $Matches[1] -cnotmatch '^(Task|ValueTask|IAsyncEnumerable)\b') {
            $nm04 += "$(Rel $f):$n  $($Matches[2]) ends in 'Async' but returns $($Matches[1])"
        }
    }
}
Report 'NM-04' 'Async suffix exactly on Task-returning methods (Handle excepted)' $nm04

# NM-05 middleware entry point is InvokeAsync
Report 'NM-05' 'Middleware exposes InvokeAsync, not Invoke' (Find ($srcFiles | Where-Object { $_.Name -like '*Middleware.cs' }) '\bTask\s+Invoke\s*\(' -CaseSensitive)

# NM-07 error codes "{FeaturePlural}.{Reason}" starting with the entity name; cache keys lowercase ':'-separated
$nm07 = @()
foreach ($f in ($srcFiles | Where-Object { $_.Name -like '*Errors.cs' })) {
    $entity = $f.BaseName -creplace 'Errors$', ''
    foreach ($m in [regex]::Matches((Get-Content $f.FullName -Raw), 'Error\.\w+\(\s*"([^"]*)"')) {
        $code = $m.Groups[1].Value
        if ($code -cnotmatch '^([A-Z][A-Za-z0-9]*)\.[A-Z][A-Za-z0-9]*$' -or -not $Matches[1].StartsWith($entity, [StringComparison]::Ordinal)) {
            $nm07 += "$($f.Name)  error code '$code'"
        }
    }
}
foreach ($f in ($srcFiles | Where-Object { $_.Name -like '*CacheKeys.cs' })) {
    # Code only: comment lines (incl. /// <see cref="Tag"/>) are not cache keys.
    $code = (Get-Content $f.FullName | Where-Object { $_ -notmatch '^\s*//' }) -join "
"
    foreach ($m in [regex]::Matches($code, '\$?"([^"]*)"')) {
        $key = $m.Groups[1].Value -creplace '\{[^}]*\}', ''
        if ($key -cnotmatch '^[a-z][a-z0-9-]*(:[a-z0-9-]*)*$') { $nm07 += "$($f.Name)  cache key '$($m.Groups[1].Value)'" }
    }
}
Report 'NM-07' 'Error codes Feature.Reason; cache keys lowercase a:b:{c}' $nm07

# NM-08 routes: lowercase kebab segments, {camelCase[:constraint]} parameters
$routeSegment = '^([a-z0-9]+(-[a-z0-9]+)*|\{[a-z][A-Za-z0-9]*(:[a-z]+)?\??\})$'
$nm08 = foreach ($f in $srcFiles) {
    foreach ($m in [regex]::Matches((Get-Content $f.FullName -Raw), 'Map(?:Get|Post|Put|Delete|Patch|Group|HealthChecks)\(\s*"([^"]*)"')) {
        $route = $m.Groups[1].Value.Trim('/')
        if (-not $route) { continue }
        $bad = @($route -split '/' | Where-Object { $_ -cnotmatch $routeSegment })
        if ($bad.Count -gt 0) { "$($f.Name)  route '$($m.Groups[1].Value)'" }
    }
}
Report 'NM-08' 'Routes are lowercase kebab-case with {camelCase} parameters' $nm08

# NM-09 migration names
$migrationDir = Join-Path $api 'Database\Migrations'
$nm09 = Get-ChildItem $migrationDir -Filter '*.cs' -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -cmatch '^\d{14}_(.+)\.cs$' -and $_.Name -cnotmatch '\.Designer\.cs$' } |
    ForEach-Object { $null = $_.Name -cmatch '^\d{14}_(.+)\.cs$'; $Matches[1] } |
    Where-Object { $_ -cne 'Initial' -and $_ -cnotmatch '^Phase\d+A?_[A-Z][A-Za-z0-9]*(_[A-Z][A-Za-z0-9]*)*$' } |
    ForEach-Object { "migration '$_'  expected Phase{N}[A]_{Words_With_Underscores}" }
Report 'NM-09' 'Migrations named Phase{N}_{Words}' $nm09

# NM-10 test classes contain tests; unit test classes point at something real, in the mirrored folder
$unitRoot = Join-Path $root 'tests\Web.Api.UnitTests'
$nm10 = @()
foreach ($f in ($testFiles | Where-Object { $_.Name -clike '*Tests.cs' })) {
    if (-not (Select-String -Path $f.FullName -Pattern '^\s*\[(Fact|Theory)\b' -CaseSensitive -Quiet)) { $nm10 += "$(Rel $f)  has no [Fact]/[Theory]" }
    if (-not $f.FullName.StartsWith("$unitRoot\")) { continue }
    $target = $f.BaseName -creplace 'Tests$', ''
    $folder = Split-Path (Split-Path $f.FullName -Parent) -Leaf
    $slice = $target -creplace '(Handler|Validator)$', ''
    $matchesType = @(Get-SourceFiles $api | Where-Object { $_.BaseName -ceq $target -and (Split-Path (Split-Path $_.FullName -Parent) -Leaf) -ceq $folder }).Count -gt 0
    $matchesSlice = ($target -cmatch '(Handler|Validator)$') -and (Test-Path (Join-Path $features "$folder\$slice.cs"))
    if (-not ($matchesType -or $matchesSlice)) { $nm10 += "$(Rel $f)  no '$target' (type) or '$slice' (slice) in a src folder named '$folder'" }
}
Report 'NM-10' 'Test files mirror what they test' $nm10

# NM-11 test method names {Subject}_Should_{Outcome}[_When_{Condition}]
$nm11 = @()
foreach ($f in $testFiles) {
    $pending = $false; $n = 0
    foreach ($line in Get-Content $f.FullName) {
        $n++
        if ($line -cmatch '^\s*\[(Fact|Theory)\b') { $pending = $true; continue }
        if ($pending -and $line -cmatch '^\s*public\s+(?:async\s+)?(?:Task|void)\s+(\w+)\s*\(') {
            $pending = $false
            if ($Matches[1] -cnotmatch '^[A-Z][A-Za-z0-9]*_Should_[A-Z][A-Za-z0-9]*(_When_[A-Z][A-Za-z0-9]*)?$') { $nm11 += "$(Rel $f):$n  $($Matches[1])" }
        }
    }
}
Report 'NM-11' 'Test methods named {Subject}_Should_{Outcome}[_When_{Condition}]' $nm11

# NM-12 kebab-case file and folder names outside C# (context/ and well-known names excepted)
$wellKnown = @('README.md', 'CLAUDE.md', 'SKILL.md', 'Dockerfile', '.env', '.env.example', '.env.ci', 'LICENSE', '.gitkeep')
$kebab = '^[a-z0-9]+(-[a-z0-9]+)*(\.[a-z0-9]+)*$'
$nm12 = @()
foreach ($dir in @('docs', 'scripts', 'deploy', 'sandbox-images', '.github', '.claude\skills', 'problems')) {
    $path = Join-Path $root $dir
    if (-not (Test-Path $path)) { continue }
    foreach ($item in Get-ChildItem $path -Recurse -Force | Where-Object { $_.FullName -notmatch '\\docs\\designs(\\|$)' }) {
        $name = $item.Name
        if ($wellKnown -ccontains $name) { continue }
        if (-not $item.PSIsContainer -and $name -cmatch '^([a-z0-9]+(-[a-z0-9]+)*)\.Dockerfile$') { continue }
        if ($name -cnotmatch $kebab) { $nm12 += "$(Rel $item)" }
    }
}
Report 'NM-12' 'kebab-case names in docs/, scripts/, deploy/, problems/, skills, CI' $nm12

Write-Host "`n== Cross-cutting" -ForegroundColor White
Report 'XC-01' 'Central Package Management: no Version= in csproj' (Find (@(Get-SourceFiles (Join-Path $root 'src') '*.csproj') + @(Get-SourceFiles (Join-Path $root 'tests') '*.csproj')) 'PackageReference[^>]*Version=')

# XC-02 every '#pragma warning disable' carries a justification: '// why' on the same line or the line above.
# (Find skips lines starting with '#', so pragmas need their own scan.)
$xc02 = @()
foreach ($f in $csFiles) {
    $lines = @(Get-Content $f.FullName)
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -notmatch '^\s*#pragma warning disable\b') { continue }
        $sameLine = $lines[$i] -match '//\s*\S'
        $above = $i -gt 0 -and $lines[$i - 1] -match '^\s*//\s*\S'
        if (-not ($sameLine -or $above)) { $xc02 += "$(Rel $f):$($i + 1)  $($lines[$i].Trim())" }
    }
}
Report 'XC-02' 'Every #pragma warning disable has a justification comment' $xc02
Report 'XC-03' 'No sync-over-async' (Find $srcFiles '\.Result\b(?!\s*<)|\.Wait\(\)|GetAwaiter\(\)\.GetResult\(\)' | Where-Object { $_ -notmatch 'Result\.(Success|Failure)|IsSuccess|result\.Result' })

Write-Host ''
if ($failed -gt 0) { Write-Host "$failed rule(s) failed." -ForegroundColor Red; exit 1 }
Write-Host 'All checked rules pass.' -ForegroundColor Green
exit 0
