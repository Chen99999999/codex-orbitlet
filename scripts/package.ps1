$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$bundle = Join-Path $repo 'dist/package'
& (Join-Path $PSScriptRoot 'build.ps1') -OutputDirectory $bundle
$version = (Get-Content -LiteralPath (Join-Path $repo 'VERSION') -Raw).Trim()
$name = 'Orbitlet-v' + $version + '-windows.zip'
$output = Join-Path $repo 'dist'
$files = @('Orbitlet.exe','preview.html','Launch.cmd','Launch-Standalone.cmd','Enable-Follow.cmd','Disable-Follow.cmd','Enable-Codex-Follow.cmd','Disable-Codex-Follow.cmd','README.md','README.zh-CN.md','LICENSE','PRIVACY.md','SECURITY.md','CONTRIBUTING.md','docs','assets') | ForEach-Object { Join-Path $bundle $_ }
$archivePath = Join-Path $output $name
Compress-Archive -LiteralPath $files -DestinationPath $archivePath -Force
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::Open($archivePath, [IO.Compression.ZipArchiveMode]::Update)
try {
    foreach ($adapter in @('qoder-usage.mjs','Enable-QoderQuota.ps1')) {
        $entry = 'integrations/' + $adapter
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, (Join-Path $bundle $entry), $entry) | Out-Null
    }
} finally { $archive.Dispose() }
$hash = (Get-FileHash -LiteralPath (Join-Path $output $name) -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $output 'SHA256SUMS.txt'),$hash + '  ' + $name + "`n",[Text.Encoding]::ASCII)
Write-Output ('Packaged: ' + (Join-Path $output $name))
