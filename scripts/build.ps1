param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repo 'dist/windows' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$OutputDirectory = (Resolve-Path -LiteralPath $OutputDirectory).Path
$framework = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Windows .NET Framework compiler was not found.' }
$references = @('PresentationCore.dll','PresentationFramework.dll','WindowsBase.dll') | ForEach-Object { '/reference:' + (Join-Path $framework ('WPF/' + $_)) }
$sources = Get-ChildItem -LiteralPath (Join-Path $repo 'src/Orbitlet') -Filter '*.cs' | ForEach-Object FullName
$manifest = Join-Path $repo 'src/Orbitlet/app.manifest'
& $compiler /nologo /utf8output /codepage:65001 /target:winexe /platform:anycpu /optimize+ /warnaserror+ ('/win32manifest:' + $manifest) ('/out:' + (Join-Path $OutputDirectory 'Orbitlet.exe')) /reference:System.dll /reference:System.Core.dll /reference:System.Xaml.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll $references $sources
if ($LASTEXITCODE -ne 0) { throw 'Orbitlet build failed.' }
Copy-Item -LiteralPath (Join-Path $repo 'web/preview.html') -Destination (Join-Path $OutputDirectory 'preview.html') -Force
foreach ($name in @('README.md','README.zh-CN.md','LICENSE','PRIVACY.md','SECURITY.md','CONTRIBUTING.md')) { if (Test-Path -LiteralPath (Join-Path $repo $name)) { Copy-Item -LiteralPath (Join-Path $repo $name) -Destination $OutputDirectory -Force } }
New-Item -ItemType Directory -Path (Join-Path $OutputDirectory 'docs/assets') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $repo 'docs/assets/orbitlet.svg') -Destination (Join-Path $OutputDirectory 'docs/assets/orbitlet.svg') -Force
$launchers = @{ 'Launch.cmd'=''; 'Launch-Standalone.cmd'=' --standalone'; 'Enable-Codex-Follow.cmd'=' --enable-follow'; 'Disable-Codex-Follow.cmd'=' --disable-follow' }
foreach ($name in $launchers.Keys) { [IO.File]::WriteAllText((Join-Path $OutputDirectory $name),"@echo off`r`nstart `"`" `"%~dp0Orbitlet.exe`"" + $launchers[$name] + "`r`n",[Text.Encoding]::ASCII) }
Write-Output ('Built: ' + (Join-Path $OutputDirectory 'Orbitlet.exe'))
