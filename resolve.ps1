[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$base = $PSScriptRoot
if (-not $base) { $base = (Get-Location).Path }
$pkgRoot = Join-Path $base 'pkg'
$outDir  = Join-Path $base 'lib'
$toolsDir = Join-Path $base 'tools'
New-Item -ItemType Directory -Force $pkgRoot, $outDir, $toolsDir | Out-Null

# Intel PresentMon 2.4.1 (MIT) — official CLI used by K2 for frame timing.
$pmName = 'PresentMon-2.4.1-x64.exe'
$pmPath = Join-Path $toolsDir $pmName
$pmSha  = 'D74183E7AE630F72CD3690BE0373ECBFDC6CBB86578148AAB8FA2A7166068F34'
if (-not (Test-Path -LiteralPath $pmPath)) {
    $pmUrl = 'https://github.com/GameTechDev/PresentMon/releases/download/v2.4.1/PresentMon-2.4.1-x64.exe'
    $pmTmp = "$pmPath.part"
    Write-Host 'download Intel PresentMon 2.4.1'
    try {
        Invoke-WebRequest $pmUrl -OutFile $pmTmp -UseBasicParsing
        Move-Item -LiteralPath $pmTmp -Destination $pmPath -Force
    }
    catch {
        Remove-Item -LiteralPath $pmTmp -Force -ErrorAction SilentlyContinue
        Write-Warning "PresentMon download failed: $($_.Exception.Message)"
    }
}
if (Test-Path -LiteralPath $pmPath) {
    $actual = (Get-FileHash -LiteralPath $pmPath -Algorithm SHA256).Hash
    if ($actual -ne $pmSha) {
        Write-Warning "PresentMon SHA256 mismatch: $actual"
    }
}

$tfmPref = @('net472','net471','net47','net462','net461','net46','net452','net451','net45','net40','net35','net20','netstandard2.0')
Add-Type -AssemblyName System.IO.Compression.FileSystem

function CleanVersion([string]$v) {
    if ([string]::IsNullOrWhiteSpace($v)) { return '' }
    # поддерживает 1.2.3, [1.2.3], [1.0.0, 2.0.0), (>= 1.0.0)
    $m = [regex]::Match($v, '\d+(\.\d+){0,3}(-[0-9A-Za-z\.\-]+)?')
    if ($m.Success) { return $m.Value }
    return ''
}

$visited = @{}
$bestTfm = @{}   # fname -> tfm priority index currently in $outDir
$queue = New-Object System.Collections.Queue
$queue.Enqueue(@('LibreHardwareMonitorLib','0.9.6'))
$queue.Enqueue(@('SharpDX','4.2.0'))
$queue.Enqueue(@('SharpDX.DXGI','4.2.0'))
$queue.Enqueue(@('SharpDX.Direct3D11','4.2.0'))
$warnings = @()

while ($queue.Count -gt 0) {
    $item = $queue.Dequeue()
    $id = "$($item[0])".Trim(); $ver = CleanVersion $item[1]
    if ([string]::IsNullOrWhiteSpace($id) -or [string]::IsNullOrWhiteSpace($ver)) {
        $warnings += "SKIP invalid package id/version: $($item[0]) / $($item[1])"
        continue
    }
    $key = "$id|$ver"
    if ($visited.ContainsKey($key)) { continue }
    $visited[$key] = $true
    $nupkg = Join-Path $pkgRoot ("$id.$ver.nupkg")
    if (-not (Test-Path $nupkg)) {
        $url = "https://api.nuget.org/v3-flatcontainer/$($id.ToLowerInvariant())/$ver/$($id.ToLowerInvariant()).$ver.nupkg"
        $tmp = "$nupkg.part"
        Write-Host "download $id $ver"
        try {
            Invoke-WebRequest $url -OutFile $tmp -UseBasicParsing
            Move-Item -LiteralPath $tmp -Destination $nupkg -Force
        }
        catch {
            Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue
            $warnings += "FAILED download $id $ver : $($_.Exception.Message)"
            continue
        }
    }
    try { $zip = [IO.Compression.ZipFile]::OpenRead($nupkg) }
    catch {
        Remove-Item -LiteralPath $nupkg -Force -ErrorAction SilentlyContinue
        $warnings += "BROKEN nupkg removed $id $ver : $($_.Exception.Message)"
        continue
    }
    try {

    $nuspecEntry = $zip.Entries | Where-Object { $_.FullName -match '^[^/]+\.nuspec$' } | Select-Object -First 1
    if ($nuspecEntry) {
        $sr = New-Object IO.StreamReader($nuspecEntry.Open())
        [xml]$ns = $sr.ReadToEnd(); $sr.Close()
        $deps = @()
        $groups = @($ns.package.metadata.dependencies.group)
        $netGroups = @($groups | Where-Object { $_.targetFramework -match 'net4|\.NETFramework' })
        $nsGroups = @($groups | Where-Object { $_.targetFramework -match 'netstandard2\.0' })
        $chosen = if ($netGroups.Count -gt 0) { $netGroups } elseif ($nsGroups.Count -gt 0) { $nsGroups } else { @() }
        foreach ($g in $chosen) { $deps += @($g.dependency) }
        if ($groups.Count -eq 0) { $deps += @($ns.package.metadata.dependencies.dependency) }
        foreach ($d in $deps) {
            if ($null -eq $d -or -not $d.id) { continue }
            $queue.Enqueue(@($d.id, $d.version))
        }
    }

    foreach ($e in $zip.Entries) {
        if ($e.FullName -match '^(?:runtimes/win-x64/)?lib/([^/]+)/(.+\.dll)$') {
            $tfm = $Matches[1].ToLowerInvariant(); $fname = $Matches[2]
            $idx = [Array]::IndexOf($tfmPref, $tfm)
            if ($idx -lt 0) { continue }
            $dest = Join-Path $outDir $fname
            $cur = if ($bestTfm.ContainsKey($fname)) { $bestTfm[$fname] } elseif (Test-Path $dest) { 999 } else { -1 }
            if ($cur -eq -1 -or $idx -lt $cur) {
                [IO.Compression.ZipFileExtensions]::ExtractToFile($e, $dest, $true)
                $bestTfm[$fname] = $idx
                Write-Host "  lib: $fname ($tfm)"
            }
        }
    }
    }
    finally { $zip.Dispose() }
}
Write-Host '--- warnings:'; $warnings | ForEach-Object { Write-Host $_ }
Write-Host '--- lib:'; Get-ChildItem $outDir -Filter *.dll | Select-Object -ExpandProperty Name
