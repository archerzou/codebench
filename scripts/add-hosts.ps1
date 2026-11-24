<#
.SYNOPSIS
    Maps the CodeBench *.codebench.localhost names to 127.0.0.1 in the Windows hosts file (build plan 1.10.3).

.DESCRIPTION
    Browsers resolve *.localhost on their own, and so does curl.exe 7.85+, but Windows PowerShell 5.1
    (Invoke-WebRequest), .NET's DNS resolver and some tools don't. This writes one marked block to the hosts file
    and replaces it on re-runs, so running the script twice changes nothing. Run from an elevated PowerShell.

    Remove the entries with -Remove.

.EXAMPLE
    # elevated PowerShell, from the repo root
    powershell -ExecutionPolicy Bypass -File scripts/add-hosts.ps1
    powershell -ExecutionPolicy Bypass -File scripts/add-hosts.ps1 -Remove
#>
[CmdletBinding()]
param(
    [string]$HostsPath = "$env:windir\System32\drivers\etc\hosts",
    [switch]$Remove
)

$ErrorActionPreference = 'Stop'

$names = @(
    'codebench.localhost',
    'app.codebench.localhost',
    'api.codebench.localhost',
    'grafana.codebench.localhost',
    'traefik.codebench.localhost'
)
$begin = '# >>> codebench (scripts/add-hosts.ps1) >>>'
$end = '# <<< codebench <<<'

$isDefaultPath = $HostsPath -eq "$env:windir\System32\drivers\etc\hosts"
if ($isDefaultPath) {
    $principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        # Not elevated: relaunch this script as administrator (Windows shows a UAC prompt) and wait for it.
        Write-Host 'hosts: administrator rights needed - accept the UAC prompt to continue.'
        $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"")
        if ($Remove) { $arguments += '-Remove' }
        try {
            $child = Start-Process -FilePath 'powershell.exe' -ArgumentList $arguments -Verb RunAs -Wait -PassThru
        }
        catch {
            Write-Error 'Elevation was cancelled. Run the script from an elevated PowerShell (Run as administrator).'
        }
        if ($child.ExitCode -ne 0) { Write-Error "The elevated run failed (exit code $($child.ExitCode))." }
        $resolved = try { [Net.Dns]::GetHostAddresses('app.codebench.localhost') | Select-Object -First 1 } catch { $null }
        Write-Host ("hosts: done in the elevated window - app.codebench.localhost -> {0}" -f $(if ($resolved) { $resolved } else { 'not resolved (removed)' }))
        exit 0
    }
}

$content = if (Test-Path $HostsPath) { [IO.File]::ReadAllText($HostsPath) } else { '' }

# Drop any previous CodeBench block (idempotent), keep everything else byte-for-byte.
$pattern = '(?ms)\r?\n?' + [regex]::Escape($begin) + '.*?' + [regex]::Escape($end) + '\r?\n?'
$stripped = [regex]::Replace($content, $pattern, "`r`n").TrimEnd("`r", "`n")

if ($Remove) {
    $new = $stripped + "`r`n"
    $message = 'removed'
}
else {
    $block = @($begin) + ($names | ForEach-Object { "127.0.0.1`t$_" }) + @($end)
    $new = $stripped + "`r`n`r`n" + ($block -join "`r`n") + "`r`n"
    $message = "mapped to 127.0.0.1: $($names -join ', ')"
}

if ($new -ne $content) {
    [IO.File]::WriteAllText($HostsPath, $new, (New-Object Text.UTF8Encoding($false)))
    if ($isDefaultPath) { ipconfig /flushdns | Out-Null }
    Write-Host "hosts: $message"
}
else {
    Write-Host 'hosts: already up to date'
}
